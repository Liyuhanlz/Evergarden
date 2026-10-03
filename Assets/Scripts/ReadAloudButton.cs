using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Listen button on the magnifying-glass info window. Pressing it (ray
// click, or right-hand A via MagnifyingGlassTarget) reads the window's text
// aloud through TextToSpeech; pressing it again stops. Nothing is spoken
// unless the player asks.
//
// While reading:
//   - the button turns Active Color and its label switches to Stop Label, so
//     it's obvious the press registered -- it goes back once the line has
//     finished, been stopped, or been cut off
//   - the word currently being spoken is tinted Word Highlight Color, so the
//     player can follow along
//
// Reads the TMP texts live at the moment of the press, so it always reads
// whatever MagnifyingGlassTarget is currently showing (summary or a fact).
// Stops on its own when the window closes (the button is disabled along with
// its Canvas) or when the text it was reading changes (paging to the next
// fact with B), so it never keeps reading a line that's no longer on screen.
//
// Unity setup:
//   1. Add a UI Button to the info window's Canvas
//   2. Add this script to the Button
//   3. Drag the window's TMP texts into Texts, in reading order (title first)
[RequireComponent(typeof(Button))]
public class ReadAloudButton : MonoBehaviour
{
    [Tooltip("The window's texts, in the order they should be read (title first)")]
    public TMP_Text[] texts;

    [Header("While reading")]
    [Tooltip("Button color while the text is being read")]
    public Color activeColor = new Color(1f, 0.82f, 0.35f, 1f);

    [Tooltip("Button label while the text is being read -- leave empty to keep the normal label")]
    public string stopLabel = "A  Stop";

    [Tooltip("Color of the word currently being spoken")]
    public Color wordHighlightColor = new Color(0.85f, 0.33f, 0.05f, 1f);

    private Image image;
    private TMP_Text label;
    private Color idleColor;
    private string idleLabel;

    // What's being read right now (null when idle), and where each text's
    // characters start within it.
    private string speaking;
    private int[] segmentStarts;

    // Current highlighted word: which text and which characters in it.
    private int highlightText = -1;
    private int highlightStart;
    private int highlightLength;
    private int dirtyText = -1;

    void Awake()
    {
        image = GetComponent<Image>();
        label = GetComponentInChildren<TMP_Text>(true);
        if (image != null) idleColor = image.color;
        if (label != null) idleLabel = label.text;

        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    // True from the press until the line finishes or is stopped.
    public bool IsReading => speaking != null;

    public void Toggle()
    {
        if (speaking != null) StopReading();
        else ReadAloud();
    }

    public void ReadAloud()
    {
        string text = BuildSpokenText(out int[] starts);
        if (string.IsNullOrWhiteSpace(text)) return;

        speaking = text;
        segmentStarts = starts;
        SetActiveLook(true);

        // Speak's onEnded fires right away for anything already playing, so
        // set state first and only clear it if this exact line is the one
        // that ended.
        string thisLine = text;
        TextToSpeech.Speak(text, OnWord, () =>
        {
            if (speaking == thisLine) Finish();
        });
    }

    void StopReading()
    {
        if (speaking == null) return;
        Finish();
        TextToSpeech.Stop();
    }

    void Finish()
    {
        speaking = null;
        segmentStarts = null;
        highlightText = -1;
        SetActiveLook(false);
    }

    void SetActiveLook(bool active)
    {
        if (image != null) image.color = active ? activeColor : idleColor;
        if (label != null) label.text = active && !string.IsNullOrEmpty(stopLabel) ? stopLabel : idleLabel;
    }

    void Update()
    {
        if (speaking != null && BuildSpokenText(out _) != speaking)
            StopReading();
    }

    void OnDisable()
    {
        StopReading();
    }

    // Same joining as the bake step (TextToSpeech.Compose), so the line
    // matches its pre-recorded audio.
    string BuildSpokenText(out int[] starts)
    {
        string[] parts = new string[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            parts[i] = t != null && t.gameObject.activeInHierarchy ? t.text : null;
        }

        starts = new int[texts.Length];
        return TextToSpeech.Compose(parts, starts);
    }

    // ---------------------------------------------
    //  WORD HIGHLIGHT
    // ---------------------------------------------
    void OnWord(int start, int length)
    {
        if (speaking == null || segmentStarts == null) return;

        // Find which text this character range falls in.
        for (int i = texts.Length - 1; i >= 0; i--)
        {
            if (segmentStarts[i] < 0 || start < segmentStarts[i]) continue;

            highlightText = i;
            highlightStart = start - segmentStarts[i];
            highlightLength = length;
            return;
        }
    }

    // Recolors vertices rather than wrapping the word in <color> tags, so the
    // text strings themselves never change (MagnifyingGlassTarget rewrites
    // them every frame, and the change check above compares them). Re-applied
    // every frame because any re-layout of the text resets vertex colors.
    void LateUpdate()
    {
        // Text that had a highlight but no longer does -- rebuild it once to
        // restore its normal colors.
        if (dirtyText >= 0 && dirtyText != highlightText)
        {
            if (texts[dirtyText] != null) texts[dirtyText].ForceMeshUpdate();
            dirtyText = -1;
        }

        if (highlightText < 0) return;

        TMP_Text t = texts[highlightText];
        if (t == null || !t.isActiveAndEnabled) return;

        t.ForceMeshUpdate();
        TMP_TextInfo info = t.textInfo;
        Color32 color = wordHighlightColor;

        int end = Mathf.Min(highlightStart + highlightLength, info.characterCount);
        for (int c = Mathf.Max(highlightStart, 0); c < end; c++)
        {
            TMP_CharacterInfo ch = info.characterInfo[c];
            if (!ch.isVisible) continue;

            Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            int v = ch.vertexIndex;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
        }

        t.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        dirtyText = highlightText;
    }
}
