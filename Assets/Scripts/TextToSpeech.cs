using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

// Reads info-window text aloud. Unity has no text-to-speech of its own, so
// lines come from one of two places:
//   1. ReadAloudLibrary -- every InfoData/CropData line pre-recorded in the
//      Editor (Tools -> Evergarden -> Bake Read Aloud Audio, also run before
//      each build). This is what the headset build uses: it needs no speech
//      engine, which Quest headsets generally don't have.
//   2. Otherwise, the speech engine the platform ships with, live:
//        - Windows (Editor / PC Link): Windows' built-in System.Speech voice
//        - Android: android.speech.tts.TextToSpeech, if the device has one
//      Anywhere else it silently does nothing.
//
// A live engine renders the line to a WAV file, which is then played through
// a regular Unity AudioSource rather than letting the engine play it
// directly. That way:
//   - it comes out wherever the rest of the game's audio does (over Link,
//     the engine's own playback went to the PC's default speakers instead of
//     the headset)
//   - playback position is known, so callers get told which word is being
//     spoken and exactly when the line has finished
//
// Nothing is read automatically -- the player opts in with a ReadAloudButton.
//
// Unity setup: none. It creates its own GameObject the first time it's
// needed. Volume follows the same "MasterVolume" PlayerPrefs value that
// SettingsMenu saves.
public class TextToSpeech : MonoBehaviour
{
    // Called with a (start, length) character range into the text passed to
    // Speak, as each word starts being spoken.
    public delegate void WordCallback(int start, int length);

    private const string VOLUME_KEY = "MasterVolume";

    private static TextToSpeech instance;

    public static bool IsSpeaking => instance != null && instance.current != null;

    static TextToSpeech Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("TextToSpeech");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<TextToSpeech>();
            }
            return instance;
        }
    }

    // Starts the speech engine ahead of time so the first Speak isn't held up
    // while it boots (Windows' voice takes a second or two to come up).
    public static void Prewarm()
    {
        _ = Instance;
    }

    // Speaks text, cutting off anything already playing (whose onEnded then
    // fires). onEnded fires exactly once per Speak: when the line finishes,
    // is stopped, is interrupted by another Speak, or fails.
    public static void Speak(string text, WordCallback onWord = null, Action onEnded = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onEnded?.Invoke();
            return;
        }

        Instance.StartRequest(text, onWord, onEnded);
    }

    public static void Stop()
    {
        if (instance != null) instance.EndCurrent();
    }

    static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_KEY, 1f));

    // Joins window texts into the one line that gets spoken -- shared by
    // ReadAloudButton and the bake step so a pre-recorded line's text always
    // matches what the button asks for. Empty parts are skipped (start -1);
    // a ". " goes between parts that don't already end in punctuation so the
    // voice pauses after the title.
    public static string Compose(IList<string> parts, int[] starts = null)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < parts.Count; i++)
        {
            if (starts != null) starts[i] = -1;
            if (string.IsNullOrWhiteSpace(parts[i])) continue;

            if (sb.Length > 0)
            {
                char last = sb[sb.Length - 1];
                sb.Append(".!?".IndexOf(last) >= 0 ? " " : ". ");
            }

            if (starts != null) starts[i] = sb.Length;
            sb.Append(parts[i]);
        }

        return sb.ToString();
    }

    // ---------------------------------------------
    //  REQUESTS + PLAYBACK (shared by both engines)
    // ---------------------------------------------
    class Request
    {
        public int id;
        public string text;
        public string wavPath;
        public WordCallback onWord;
        public Action onEnded;
        public readonly List<WordTiming> words = new List<WordTiming>();
        public bool playing;
        public bool ownsClip;
        public bool startedPlaying;
        public int lastWord = -1;
    }

    struct WordTiming
    {
        public float time;
        public int start;
        public int length;
    }

    private AudioSource audioSource;
    private Request current;
    private int nextId;

    // Engine callbacks arrive on background threads -- they're queued here
    // and handled on the main thread in Update.
    private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();

    private readonly Dictionary<string, ReadAloudLibrary.Line> library = new Dictionary<string, ReadAloudLibrary.Line>();

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        ReadAloudLibrary lib = Resources.Load<ReadAloudLibrary>(ReadAloudLibrary.ResourceName);
        if (lib != null)
            foreach (ReadAloudLibrary.Line line in lib.lines)
                if (line != null && line.clip != null && !string.IsNullOrEmpty(line.text))
                    library[line.text] = line;

        StartEngine();
    }

    string WavPathFor(int id) => Path.Combine(Application.temporaryCachePath, "tts_" + id + ".wav");

    void StartRequest(string text, WordCallback onWord, Action onEnded)
    {
        EndCurrent();

        int id = ++nextId;
        current = new Request
        {
            id = id,
            text = text,
            wavPath = WavPathFor(id),
            onWord = onWord,
            onEnded = onEnded,
        };

        if (library.TryGetValue(text, out ReadAloudLibrary.Line recorded))
        {
            var timings = new List<WordTiming>();
            int count = recorded.wordTimes != null ? recorded.wordTimes.Length : 0;
            for (int i = 0; i < count; i++)
                timings.Add(new WordTiming { time = recorded.wordTimes[i], start = recorded.wordStarts[i], length = recorded.wordLengths[i] });

            Play(recorded.clip, timings, false);
            return;
        }

        if (!EngineSynthesize(current)) EndCurrent();
    }

    // The engine finished writing request id's WAV file.
    void OnSynthesized(int id, List<WordTiming> timings)
    {
        if (current == null || current.id != id)
        {
            // Stopped or replaced while it was being synthesized.
            DeleteQuietly(WavPathFor(id));
            return;
        }

        AudioClip clip = WavLoader.Load(current.wavPath);
        DeleteQuietly(current.wavPath);

        if (clip == null)
        {
            Debug.LogWarning("TextToSpeech: couldn't read the synthesized audio");
            EndCurrent();
            return;
        }

        Play(clip, timings, true);
    }

    void Play(AudioClip clip, List<WordTiming> timings, bool ownsClip)
    {
        if (timings != null && timings.Count > 0) AddEngineTimings(current, timings, clip.length);
        else EstimateWordTimings(current, clip.length);

        current.ownsClip = ownsClip;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        audioSource.clip = clip;
        audioSource.volume = Volume;
        audioSource.Play();
        current.playing = true;
    }

    void OnSynthesisFailed(int id, string reason)
    {
        Debug.LogWarning("TextToSpeech: synthesis failed -- " + reason);
        if (current != null && current.id == id) EndCurrent();
    }

    void Update()
    {
        while (mainThread.TryDequeue(out Action action)) action();

        if (current == null || !current.playing) return;

        if (!audioSource.isPlaying)
        {
            // A clip whose audio data is still loading (first play of a
            // recorded line that wasn't preloaded) isn't playing *yet* --
            // only treat "not playing" as finished once it's actually loaded.
            AudioDataLoadState state = audioSource.clip != null ? audioSource.clip.loadState : AudioDataLoadState.Failed;
            if (state == AudioDataLoadState.Loading || state == AudioDataLoadState.Unloaded)
            {
                if (state == AudioDataLoadState.Unloaded) audioSource.clip.LoadAudioData();
                return;
            }

            if (!current.startedPlaying && state == AudioDataLoadState.Loaded)
            {
                // Loaded after Play() was called -- start it now.
                current.startedPlaying = true;
                audioSource.Play();
                return;
            }

            EndCurrent();
            return;
        }

        current.startedPlaying = true;

        // Latest word whose start time has been reached.
        float t = audioSource.time;
        int word = current.lastWord;
        while (word + 1 < current.words.Count && current.words[word + 1].time <= t) word++;

        if (word != current.lastWord)
        {
            current.lastWord = word;
            current.onWord?.Invoke(current.words[word].start, current.words[word].length);
        }
    }

    void EndCurrent()
    {
        if (current == null) return;

        Request ended = current;
        current = null;

        audioSource.Stop();
        if (audioSource.clip != null)
        {
            // Library clips are assets -- only destroy ones made at runtime.
            if (ended.ownsClip) Destroy(audioSource.clip);
            audioSource.clip = null;
        }

        ended.onEnded?.Invoke();
    }

    // Safety net: if the engine's word times run past the end of the clip
    // (a voice reporting positions for a different sample rate than it
    // wrote), squeeze them back inside it rather than highlighting words
    // after the audio has already finished.
    static void AddEngineTimings(Request request, List<WordTiming> timings, float duration)
    {
        float last = timings[timings.Count - 1].time;
        float scale = last > duration * 0.95f && last > 0f ? duration * 0.85f / last : 1f;

        foreach (WordTiming w in timings)
            request.words.Add(new WordTiming { time = w.time * scale, start = w.start, length = w.length });
    }

    // No per-word timing from the engine (Android) -- spread the clip's length
    // over the words by how long each one is, with a little extra after
    // punctuation where the voice pauses. Close enough to follow along.
    static readonly Regex WordPattern = new Regex(@"\S+");

    static void EstimateWordTimings(Request request, float duration)
    {
        MatchCollection matches = WordPattern.Matches(request.text);
        if (matches.Count == 0) return;

        float total = 0f;
        float[] weights = new float[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            string w = matches[i].Value;
            weights[i] = w.Length + 1f + (".!?,;:".IndexOf(w[w.Length - 1]) >= 0 ? 3f : 0f);
            total += weights[i];
        }

        float time = 0f;
        for (int i = 0; i < matches.Count; i++)
        {
            request.words.Add(new WordTiming { time = time, start = matches[i].Index, length = matches[i].Length });
            time += duration * weights[i] / total;
        }
    }

    static void DeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) { }
    }

    void OnDestroy()
    {
        EndCurrent();
        StopEngine();
        if (instance == this) instance = null;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // ---------------------------------------------
    //  ANDROID ENGINE
    // ---------------------------------------------
    private AndroidJavaObject tts;
    private bool ready;
    private Request pendingUntilReady;

    // TextToSpeech's engine binds asynchronously -- a request made before
    // onInit fires is held and synthesized as soon as it's ready.
    class InitListener : AndroidJavaProxy
    {
        private readonly TextToSpeech owner;
        public InitListener(TextToSpeech owner) : base("android.speech.tts.TextToSpeech$OnInitListener") { this.owner = owner; }

        void onInit(int status)
        {
            owner.mainThread.Enqueue(() => owner.HandleInit(status));
        }
    }

    // UtteranceProgressListener is an abstract class, which AndroidJavaProxy
    // can't implement -- this older interface still reports completion for
    // synthesizeToFile.
    class CompletedListener : AndroidJavaProxy
    {
        private readonly TextToSpeech owner;
        public CompletedListener(TextToSpeech owner) : base("android.speech.tts.TextToSpeech$OnUtteranceCompletedListener") { this.owner = owner; }

        void onUtteranceCompleted(string utteranceId)
        {
            if (int.TryParse(utteranceId, out int id))
                owner.mainThread.Enqueue(() => owner.OnSynthesized(id, null));
        }
    }

    void HandleInit(int status)
    {
        ready = status == 0; // TextToSpeech.SUCCESS
        if (!ready)
        {
            Debug.LogWarning("TextToSpeech: no speech engine available on this device (status " + status + ")");
            if (pendingUntilReady != null) OnSynthesisFailed(pendingUntilReady.id, "no engine");
            pendingUntilReady = null;
            return;
        }

        tts.Call<int>("setOnUtteranceCompletedListener", new CompletedListener(this));

        if (pendingUntilReady != null)
        {
            Request r = pendingUntilReady;
            pendingUntilReady = null;
            if (current == r && !EngineSynthesize(r)) EndCurrent();
        }
    }

    void StartEngine()
    {
        using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(this));
            }));
        }
    }

    bool EngineSynthesize(Request request)
    {
        if (!ready || tts == null)
        {
            pendingUntilReady = request;
            return true;
        }

        using (AndroidJavaObject parameters = new AndroidJavaObject("android.os.Bundle"))
        using (AndroidJavaObject file = new AndroidJavaObject("java.io.File", request.wavPath))
        {
            int result = tts.Call<int>("synthesizeToFile", request.text, parameters, file, request.id.ToString());
            if (result != 0)
            {
                Debug.LogWarning("TextToSpeech: synthesizeToFile failed (" + result + ")");
                return false;
            }
        }
        return true;
    }

    void StopEngine()
    {
        if (tts != null)
        {
            tts.Call("shutdown");
            tts.Dispose();
            tts = null;
        }
    }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // ---------------------------------------------
    //  WINDOWS ENGINE
    // ---------------------------------------------
    // Windows' built-in System.Speech voice, hosted in one hidden PowerShell
    // process that stays open. Unity's Mono runtime can't create the SAPI
    // COM object directly ("Unmanaged activation is not supported"), and a
    // fresh process per line would add a second or so of delay each time.
    //
    // Unity -> host (stdin):   <id> TAB <wav path> TAB <text>
    // host -> Unity (stdout):  W <id> <ms> <charStart> <charLength>   (per word)
    //                          D <id>                                 (wav written)
    //                          E <id> <message>                       (failed)
    //
    // The WAV is written at 16 kHz on purpose: the voices report word
    // positions as if the audio were 16 kHz, so at any other rate every word
    // time comes back stretched (x2.75 at 44.1 kHz).
    private const string HelperSource = @"
using System;
using System.Collections.Generic;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
public static class EvergardenTts {
    public static string[] Synth(string text, string path) {
        var words = new List<string>();
        var done = new ManualResetEvent(false);
        using (var s = new SpeechSynthesizer()) {
            s.SpeakProgress += (o, e) => { lock (words) words.Add(((int)e.AudioPosition.TotalMilliseconds) + "" "" + e.CharacterPosition + "" "" + e.CharacterCount); };
            s.SpeakCompleted += (o, e) => done.Set();
            s.SetOutputToWaveFile(path, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            s.SpeakAsync(text);
            done.WaitOne(30000);
            Thread.Sleep(50);
        }
        lock (words) return words.ToArray();
    }
}";

    // Public so the Editor's bake step drives the exact same voice setup.
    public const string HostScript =
        "$ErrorActionPreference = 'Stop'\n" +
        "Add-Type -ReferencedAssemblies System.Speech -TypeDefinition @'\n" + HelperSource + "\n'@\n" +
        "while (($l = [Console]::In.ReadLine()) -ne $null) {\n" +
        "  $p = $l.Split([char]9, 3)\n" +
        "  if ($p.Length -lt 3) { continue }\n" +
        "  try {\n" +
        "    foreach ($w in [EvergardenTts]::Synth($p[2], $p[1])) { [Console]::Out.WriteLine('W ' + $p[0] + ' ' + $w) }\n" +
        "    [Console]::Out.WriteLine('D ' + $p[0])\n" +
        "  } catch { [Console]::Out.WriteLine('E ' + $p[0] + ' ' + $_.Exception.Message) }\n" +
        "  [Console]::Out.Flush()\n" +
        "}\n";

    private System.Diagnostics.Process host;
    private bool failed;

    // Word timings per request, collected on the stdout thread until that
    // request's D line arrives.
    private readonly Dictionary<int, List<WordTiming>> incoming = new Dictionary<int, List<WordTiming>>();

    void StartEngine()
    {
        try
        {
            string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(HostScript));
            var info = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            host = new System.Diagnostics.Process { StartInfo = info };
            host.OutputDataReceived += (s, e) => { if (e.Data != null) HandleHostLine(e.Data); };
            host.ErrorDataReceived += (s, e) =>
            {
                // PowerShell writes progress records as CLIXML on stderr --
                // only pass on actual error text.
                if (!string.IsNullOrWhiteSpace(e.Data) && !e.Data.StartsWith("#< CLIXML") && !e.Data.StartsWith("<Objs"))
                    mainThread.Enqueue(() => Debug.LogWarning("TextToSpeech (Windows voice): " + e.Data));
            };
            host.Start();
            host.BeginOutputReadLine();
            host.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            Debug.LogWarning("TextToSpeech: couldn't start the Windows speech voice -- " + e.Message);
            host = null;
        }

        failed = host == null;
    }

    // Runs on the process's stdout thread.
    void HandleHostLine(string line)
    {
        string[] parts = line.Split(new[] { ' ' }, 3);
        if (parts.Length < 2 || !int.TryParse(parts[1], out int id)) return;

        switch (parts[0])
        {
            case "W":
                string[] nums = parts.Length > 2 ? parts[2].Split(' ') : new string[0];
                if (nums.Length == 3 &&
                    int.TryParse(nums[0], out int ms) &&
                    int.TryParse(nums[1], out int start) &&
                    int.TryParse(nums[2], out int length))
                {
                    lock (incoming)
                    {
                        if (!incoming.TryGetValue(id, out List<WordTiming> list))
                            incoming[id] = list = new List<WordTiming>();
                        list.Add(new WordTiming { time = ms / 1000f, start = start, length = length });
                    }
                }
                break;

            case "D":
                List<WordTiming> timings;
                lock (incoming)
                {
                    incoming.TryGetValue(id, out timings);
                    incoming.Remove(id);
                }
                mainThread.Enqueue(() => OnSynthesized(id, timings));
                break;

            case "E":
                lock (incoming) incoming.Remove(id);
                string message = parts.Length > 2 ? parts[2] : "unknown error";
                mainThread.Enqueue(() => OnSynthesisFailed(id, message));
                break;
        }
    }

    bool EngineSynthesize(Request request)
    {
        if (failed) return false;

        try
        {
            if (host.HasExited)
            {
                failed = true;
                Debug.LogWarning("TextToSpeech: the Windows speech process exited unexpectedly");
                return false;
            }

            // Tabs/newlines are the protocol's separators -- swap them for
            // spaces one-for-one so character positions still line up with
            // the caller's text.
            string text = request.text.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            host.StandardInput.WriteLine(request.id + "\t" + request.wavPath + "\t" + text);
            host.StandardInput.Flush();
            return true;
        }
        catch (Exception e)
        {
            failed = true;
            Debug.LogWarning("TextToSpeech: Windows speech failed -- " + e.Message);
            return false;
        }
    }

    void StopEngine()
    {
        if (host == null) return;

        try
        {
            if (!host.HasExited) host.Kill();
        }
        catch (Exception) { }

        host.Dispose();
        host = null;
    }
#else
    void StartEngine() { }
    bool EngineSynthesize(Request request) { return false; }
    void StopEngine() { }
#endif

    // ---------------------------------------------
    //  WAV -> AudioClip
    // ---------------------------------------------
    // Both engines write plain 16-bit PCM WAV files. Unity's own loader for
    // local files needs a coroutine + UnityWebRequest, so this just reads the
    // PCM samples directly.
    static class WavLoader
    {
        public static AudioClip Load(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return null;

                int channels = 1, sampleRate = 16000, bitsPerSample = 16;
                int pos = 12;
                while (pos + 8 <= bytes.Length)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                    int dataStart = pos + 8;

                    if (chunkId == "fmt ")
                    {
                        channels = BitConverter.ToInt16(bytes, dataStart + 2);
                        sampleRate = BitConverter.ToInt32(bytes, dataStart + 4);
                        bitsPerSample = BitConverter.ToInt16(bytes, dataStart + 14);
                    }
                    else if (chunkId == "data")
                    {
                        if (bitsPerSample != 16) return null;

                        // Some writers leave the size as 0/-1 if they didn't
                        // finalize the header -- fall back to "rest of file".
                        int size = chunkSize <= 0 || dataStart + chunkSize > bytes.Length ? bytes.Length - dataStart : chunkSize;
                        int sampleCount = size / 2;
                        if (sampleCount == 0) return null;

                        float[] samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                            samples[i] = BitConverter.ToInt16(bytes, dataStart + i * 2) / 32768f;

                        AudioClip clip = AudioClip.Create("TextToSpeech", sampleCount / channels, channels, sampleRate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }

                    if (chunkSize < 0) break;
                    pos = dataStart + chunkSize + (chunkSize & 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("TextToSpeech: couldn't load " + path + " -- " + e.Message);
            }

            return null;
        }
    }
}
