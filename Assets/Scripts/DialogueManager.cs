using System;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

// Runs dialogue for any NPCBase. Each NPC owns and positions its own speech
// bubble (see NPCBase.ShowSpeechBubbleLine/HideSpeechBubble) -- this manager
// just tells the current speaker when to show/hide it, and separately drives
// the dialogue box that sits in front of the player. Press the right-hand
// controller's primary button (A) to advance to the next line -- matches the
// "Press A to continue" hint under the dialogue box text.
//
// Unity setup:
//   1. Put this script on an empty "DialogueManager" GameObject
//   2. Create a World Space "DialogueBoxCanvas" fixed in front of the player,
//      with one TextMeshPro text child -- drag it into Dialogue Box Canvas /
//      Dialogue Box Text
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Dialogue Box (in front of player)")]
    public Canvas dialogueBoxCanvas;
    public TMP_Text dialogueBoxText;

    NPCBase activeSpeaker;
    string[] activeLines;
    int lineIndex;
    Action onComplete;
    bool dialogueActive = false;

    InputDevice rightHandDevice;
    bool prevAPressed = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialogueBoxCanvas != null) dialogueBoxCanvas.gameObject.SetActive(false);
    }

    void Start()
    {
        GetRightHandDevice();
    }

    void Update()
    {
        if (!dialogueActive) return;

        HandleAdvanceInput();
    }

    // Called by NPCBase.StartDialogue(). onDialogueComplete fires once the
    // player has clicked through every line.
    public void StartDialogueLines(NPCBase speaker, string[] lines, Action onDialogueComplete)
    {
        if (lines == null || lines.Length == 0)
        {
            onDialogueComplete?.Invoke();
            return;
        }

        activeSpeaker = speaker;
        activeLines = lines;
        lineIndex = 0;
        onComplete = onDialogueComplete;
        dialogueActive = true;

        if (dialogueBoxCanvas != null) dialogueBoxCanvas.gameObject.SetActive(true);

        ShowLine();
    }

    void ShowLine()
    {
        string line = activeLines[lineIndex];

        if (dialogueBoxText != null) dialogueBoxText.text = line;
        activeSpeaker?.ShowSpeechBubbleLine(line);

        SpeakLine(line);
    }

    // Text-to-speech hook -- not implemented yet. Wire up a TTS service/plugin
    // here later and call it with `line`.
    void SpeakLine(string line) { }

    void AdvanceLine()
    {
        lineIndex++;

        if (lineIndex >= activeLines.Length)
        {
            EndDialogue();
            return;
        }

        ShowLine();
    }

    void EndDialogue()
    {
        dialogueActive = false;

        if (dialogueBoxCanvas != null) dialogueBoxCanvas.gameObject.SetActive(false);
        activeSpeaker?.HideSpeechBubble();

        Action callback = onComplete;
        activeSpeaker = null;
        onComplete = null;
        callback?.Invoke();
    }

    void HandleAdvanceInput()
    {
        if (!rightHandDevice.isValid)
        {
            GetRightHandDevice();
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
        {
            if (aPressed && !prevAPressed)
                AdvanceLine();

            prevAPressed = aPressed;
        }
    }

    void GetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}
