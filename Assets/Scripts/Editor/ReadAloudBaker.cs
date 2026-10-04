using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Tools -> Evergarden -> Bake Read Aloud Audio
//
// Pre-records every line the magnifying-glass window can show (title + the
// summary, title + each fact, and for crops title + the Farm and Game tabs,
// for every InfoData and CropData asset)
// with Windows' built-in voice, saves them as audio clips under
// Assets/Sound/ReadAloud, and lists them in Assets/Resources/
// ReadAloudLibrary.asset along with each word's timing. The headset build
// then plays these directly -- no speech engine needed on the Quest.
//
// Clips are named after their object and line -- read_horse_summary,
// read_horse_fact1, read_watering_can_summary, ...
//
// Incremental: lines already recorded are kept (renamed if their name
// changed), only new/changed text is synthesized, and clips for text that no
// longer exists are deleted. Also
// runs automatically at the start of every build, so edited InfoData/
// CropData text can't ship with stale or missing audio.
public class ReadAloudBaker : IPreprocessBuildWithReport
{
    const string AudioFolder = "Assets/Sound/ReadAloud";
    const string LibraryFolder = "Assets/Resources";
    const string LibraryPath = LibraryFolder + "/" + ReadAloudLibrary.ResourceName + ".asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Bake(false);
    }

    [MenuItem("Tools/Evergarden/Bake Read Aloud Audio")]
    static void BakeFromMenu()
    {
        Bake(true);
    }

    // Every line the window can read, exactly as ReadAloudButton composes it,
    // mapped to the clip name it's saved under: read_<object>_summary and
    // read_<object>_fact1, _fact2, ... (e.g. read_horse_fact2), so each clip
    // is easy to find in the Project window.
    static Dictionary<string, string> CollectLines()
    {
        var lines = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddLine(string text, string name)
        {
            if (lines.ContainsKey(text)) return;

            // Two objects sharing a display name still get distinct files.
            string unique = name;
            for (int n = 2; usedNames.Contains(unique); n++) unique = name + "_" + n;

            usedNames.Add(unique);
            lines[text] = unique;
        }

        void Add(string title, string summary, List<string> facts)
        {
            string baseName = "read_" + Slug(title);

            if (!string.IsNullOrWhiteSpace(summary))
                AddLine(TextToSpeech.Compose(new[] { title, summary }), baseName + "_summary");

            if (facts == null) return;
            for (int i = 0; i < facts.Count; i++)
                if (!string.IsNullOrWhiteSpace(facts[i]))
                    AddLine(TextToSpeech.Compose(new[] { title, facts[i] }), baseName + "_fact" + (i + 1));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:InfoData"))
        {
            InfoData d = AssetDatabase.LoadAssetAtPath<InfoData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null) Add(d.displayName, d.summary, d.facts);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            CropData d = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d == null) continue;
            Add(d.cropName, d.summary, d.facts);

            // The window's Farm and Game tabs -- fixed per crop, so they can
            // be pre-recorded too. (The live Growth tab changes as the crop
            // grows, so it isn't.)
            string baseName = "read_" + Slug(d.cropName);
            string realFarm = d.RealFarmPage();
            if (realFarm.Length > 0) AddLine(TextToSpeech.Compose(new[] { d.cropName, realFarm }), baseName + "_realfarm");
            string game = d.GamePage();
            if (game.Length > 0) AddLine(TextToSpeech.Compose(new[] { d.cropName, game }), baseName + "_game");
        }

        npcScanComplete = CollectNpcLines(AddLine);

        return lines;
    }

    // False if some build scene couldn't be opened to read its NPCs -- the
    // bake then keeps every existing clip rather than deleting NPC lines it
    // simply didn't get to see this time.
    static bool npcScanComplete = true;

    // NPC dialogue (DialogueManager speaks every line): each string[] field on
    // every NPCBase in the build's scenes -- MerchantNPC.tutorialLines,
    // VillagerNPC greetings, QuestNPC lines -- one clip per line, named
    // read_<npc>_<field><n>, e.g. read_merchant_tutorial1. Spoken as-is, so
    // the text is the raw line (no title prefix like the info window has).
    static bool CollectNpcLines(Action<string, string> addLine)
    {
        bool complete = true;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled) continue;

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(buildScene.path);
            bool openedHere = false;

            if (!scene.isLoaded)
            {
                try
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(buildScene.path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    openedHere = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Read Aloud: couldn't open " + buildScene.path + " to read its NPC dialogue -- " + e.Message);
                    complete = false;
                    continue;
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NPCBase npc in root.GetComponentsInChildren<NPCBase>(true))
                {
                    foreach (var field in npc.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (field.FieldType != typeof(string[])) continue;

                        string[] dialogue = (string[])field.GetValue(npc);
                        if (dialogue == null) continue;

                        string baseName = "read_" + Slug(npc.name) + "_" + FieldSlug(field.Name);
                        for (int i = 0; i < dialogue.Length; i++)
                            if (!string.IsNullOrWhiteSpace(dialogue[i]))
                                addLine(dialogue[i], baseName + (i + 1));
                    }
                }
            }

            if (openedHere) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }

        return complete;
    }

    // "tutorialLines" -> "tutorial", "lowFriendshipGreetings" -> "low_friendship_greetings"
    static string FieldSlug(string fieldName)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in fieldName)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        string slug = sb.ToString();
        return slug.EndsWith("_lines") ? slug.Substring(0, slug.Length - "_lines".Length) : slug;
    }

    // "Watering Can" -> "watering_can"
    static string Slug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in (title ?? "").Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
        }
        string slug = sb.ToString().Trim('_');
        return slug.Length > 0 ? slug : "unnamed";
    }

    static void Bake(bool interactive)
    {
        Dictionary<string, string> wanted = CollectLines();

        EnsureFolder(AudioFolder);
        EnsureFolder(LibraryFolder);

        ReadAloudLibrary library = AssetDatabase.LoadAssetAtPath<ReadAloudLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ReadAloudLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // Some scene's NPCs couldn't be read -- keep what's already recorded
        // instead of treating those lines as deleted.
        if (!npcScanComplete)
            foreach (ReadAloudLibrary.Line line in library.lines)
                if (line != null && line.clip != null && !string.IsNullOrEmpty(line.text) && !wanted.ContainsKey(line.text))
                    wanted[line.text] = line.clip.name;

        // Keep anything already recorded whose text is still in use and
        // whose clip is still there.
        var existing = new Dictionary<string, ReadAloudLibrary.Line>(StringComparer.Ordinal);
        foreach (ReadAloudLibrary.Line line in library.lines)
            if (line != null && line.clip != null && !string.IsNullOrEmpty(line.text) && wanted.ContainsKey(line.text))
                existing[line.text] = line;

        // Clips for text that no longer exists anywhere -- deleted first so
        // their file names are free for re-recorded lines (an edited fact
        // keeps its read_<object>_factN name).
        var keepFiles = new HashSet<string>(existing.Values.Select(l => AssetDatabase.GetAssetPath(l.clip)), StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!keepFiles.Contains(path) && AssetDatabase.DeleteAsset(path)) removed++;
        }

        int renamed = RenameKeptClips(existing, wanted);

        List<string> toRecord = wanted.Keys.Where(t => !existing.ContainsKey(t)).ToList();

        var recorded = new Dictionary<string, ReadAloudLibrary.Line>(StringComparer.Ordinal);
        if (toRecord.Count > 0)
        {
#if UNITY_EDITOR_WIN
            recorded = Record(toRecord, wanted);
#else
            Debug.LogWarning("Read Aloud: " + toRecord.Count + " line(s) need recording, which uses Windows' voice -- bake on a Windows machine.");
#endif
        }

        foreach (ReadAloudLibrary.Line line in existing.Values)
            ConfigureImport(AssetDatabase.GetAssetPath(line.clip));

        // Rebuild the list, ordered by clip name so it reads like the folder.
        library.lines = new List<ReadAloudLibrary.Line>();
        int missing = 0;
        foreach (var pair in wanted.OrderBy(p => p.Value, StringComparer.Ordinal))
        {
            if (existing.TryGetValue(pair.Key, out ReadAloudLibrary.Line line) || recorded.TryGetValue(pair.Key, out line))
                library.lines.Add(line);
            else
                missing++;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        string summary = "Read Aloud: " + library.lines.Count + " line(s) ready (" + recorded.Count + " newly recorded, " + renamed + " renamed, " + removed + " stale clip(s) removed)" +
                         (missing > 0 ? ", " + missing + " MISSING -- see warnings above" : "") + ".";
        if (missing > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);

        if (interactive) EditorUtility.DisplayDialog("Bake Read Aloud Audio", summary, "OK");
    }

    // Gives kept clips their current name (facts reordered, an object
    // renamed, or clips from before names were used). AssetDatabase.RenameAsset
    // keeps each clip's GUID, so the library's references survive. Done in two
    // passes via temporary names so two clips swapping names can't collide.
    static int RenameKeptClips(Dictionary<string, ReadAloudLibrary.Line> kept, Dictionary<string, string> wanted)
    {
        var toRename = kept.Values.Where(l => l.clip.name != wanted[l.text]).ToList();

        foreach (ReadAloudLibrary.Line line in toRename)
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(line.clip), "tmp_" + Guid.NewGuid().ToString("N"));

        int renamed = 0;
        foreach (ReadAloudLibrary.Line line in toRename)
        {
            string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(line.clip), wanted[line.text]);
            if (string.IsNullOrEmpty(error)) renamed++;
            else Debug.LogWarning("Read Aloud: couldn't rename clip to " + wanted[line.text] + " -- " + error);
        }

        return renamed;
    }

#if UNITY_EDITOR_WIN
    // Runs the same hidden-PowerShell voice host TextToSpeech uses at
    // runtime, feeding it every line in one go and collecting the WAVs + word
    // timings it reports back.
    static Dictionary<string, ReadAloudLibrary.Line> Record(List<string> texts, Dictionary<string, string> names)
    {
        var result = new Dictionary<string, ReadAloudLibrary.Line>(StringComparer.Ordinal);
        var paths = new string[texts.Count];
        var words = new List<(float time, int start, int length)>[texts.Count];
        var done = new bool[texts.Count];

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        for (int i = 0; i < texts.Count; i++)
        {
            paths[i] = AudioFolder + "/" + names[texts[i]] + ".wav";
            words[i] = new List<(float, int, int)>();
        }

        try
        {
            EditorUtility.DisplayProgressBar("Bake Read Aloud Audio", "Recording " + texts.Count + " line(s) with the Windows voice...", 0.3f);

            string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(TextToSpeech.HostScript));
            var info = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var errors = new System.Text.StringBuilder();
            using (Process host = new Process { StartInfo = info })
            {
                host.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data) && !e.Data.StartsWith("#< CLIXML") && !e.Data.StartsWith("<Objs"))
                        lock (errors) errors.AppendLine(e.Data);
                };
                host.Start();
                host.BeginErrorReadLine();

                // Send the lines from a background thread while this one reads
                // the replies. Writing them all first deadlocked on big bakes:
                // the host's per-word replies filled the output pipe, so it
                // stopped reading input while we were still blocked writing it.
                var requests = new List<string>(texts.Count);
                for (int i = 0; i < texts.Count; i++)
                {
                    string full = Path.Combine(projectRoot, paths[i]).Replace('/', '\\');
                    string text = texts[i].Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
                    requests.Add(i + "\t" + full + "\t" + text);
                }
                var writer = new System.Threading.Thread(() =>
                {
                    try
                    {
                        foreach (string request in requests) host.StandardInput.WriteLine(request);
                        host.StandardInput.Close();
                    }
                    catch (IOException) { } // host died -- the read loop below ends and reports what's missing
                });
                writer.Start();

                string line;
                while ((line = host.StandardOutput.ReadLine()) != null)
                {
                    string[] parts = line.Split(new[] { ' ' }, 3);
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int id) || id < 0 || id >= texts.Count) continue;

                    if (parts[0] == "W" && parts.Length > 2)
                    {
                        string[] n = parts[2].Split(' ');
                        if (n.Length == 3 && int.TryParse(n[0], out int ms) && int.TryParse(n[1], out int start) && int.TryParse(n[2], out int length))
                            words[id].Add((ms / 1000f, start, length));
                    }
                    else if (parts[0] == "D") done[id] = true;
                    else if (parts[0] == "E") Debug.LogWarning("Read Aloud: couldn't record \"" + texts[id] + "\" -- " + (parts.Length > 2 ? parts[2] : "unknown error"));
                }

                writer.Join(10000);
                host.WaitForExit(10000);
            }

            if (errors.Length > 0) Debug.LogWarning("Read Aloud (Windows voice): " + errors);

            EditorUtility.DisplayProgressBar("Bake Read Aloud Audio", "Importing clips...", 0.8f);
            AssetDatabase.Refresh();

            for (int i = 0; i < texts.Count; i++)
            {
                if (!done[i]) continue;

                ConfigureImport(paths[i]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                if (clip == null)
                {
                    Debug.LogWarning("Read Aloud: recorded \"" + texts[i] + "\" but couldn't import " + paths[i]);
                    continue;
                }

                result[texts[i]] = new ReadAloudLibrary.Line
                {
                    text = texts[i],
                    clip = clip,
                    wordTimes = words[i].Select(w => w.time).ToArray(),
                    wordStarts = words[i].Select(w => w.start).ToArray(),
                    wordLengths = words[i].Select(w => w.length).ToArray(),
                };
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Read Aloud: recording failed -- " + e.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return result;
    }
#endif

    // Short voice lines: preloaded with the scene so the first press plays
    // instantly instead of waiting on a load, decompressed once on load
    // (cheap at these lengths), mono.
    static void ConfigureImport(string path)
    {
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        bool changed = !importer.defaultSampleSettings.preloadAudioData || settings.loadType != AudioClipLoadType.DecompressOnLoad || !importer.forceToMono;

        settings.preloadAudioData = true;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;

        if (changed) importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
