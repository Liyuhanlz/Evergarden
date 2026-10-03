using System.Collections.Generic;
using UnityEngine;

// Pre-recorded read-aloud audio for every info-window line, so the headset
// build can read them with no speech engine at all (Quest headsets generally
// don't ship one). TextToSpeech checks here first and only falls back to the
// platform's live engine for text that isn't in the library.
//
// Generated -- don't edit by hand. Tools -> Evergarden -> Bake Read Aloud
// Audio rebuilds it from every InfoData and CropData asset (it also runs
// automatically before each build). Lives in a Resources folder so
// TextToSpeech can load it without any scene wiring.
public class ReadAloudLibrary : ScriptableObject
{
    public const string ResourceName = "ReadAloudLibrary";

    [System.Serializable]
    public class Line
    {
        [TextArea] public string text;
        public AudioClip clip;

        // Per word, in speaking order: when it starts (seconds into the clip)
        // and which characters of text it covers.
        public float[] wordTimes;
        public int[] wordStarts;
        public int[] wordLengths;
    }

    public List<Line> lines = new List<Line>();
}
