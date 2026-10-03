using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Tools -> Evergarden -> Add Read Aloud Buttons
//
// Finds the Magnifying Glass info window in the open scene and gives it a
// "Listen" button wired to ReadAloudButton. Safe to run more than once -- a window
// that already has one is skipped. Undoable.
public static class ReadAloudButtonSetup
{
    const string ButtonName = "ReadAloudButton";
    const float ButtonWidth = 190f;
    const float ButtonHeight = 76f;

    [MenuItem("Tools/Evergarden/Add Read Aloud Buttons")]
    static void AddButtons()
    {
        // Canvas -> its texts in reading order.
        var windows = new Dictionary<Canvas, TMP_Text[]>();

        foreach (MagnifyingGlassTarget glass in Object.FindObjectsOfType<MagnifyingGlassTarget>(true))
            if (glass.infoCanvas != null)
                windows[glass.infoCanvas] = new TMP_Text[] { glass.titleText, glass.bodyText };

        int added = 0;
        foreach (var pair in windows)
            if (AddButton(pair.Key, pair.Value)) added++;

        if (added > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Read Aloud: added " + added + " button(s) across " + windows.Count + " info window(s). Save the scene to keep them.");
    }

    static bool AddButton(Canvas canvas, TMP_Text[] texts)
    {
        if (canvas.GetComponentInChildren<ReadAloudButton>(true) != null)
        {
            Debug.Log("Read Aloud: " + canvas.name + " already has a button, skipped.", canvas);
            return false;
        }

        TMP_Text reference = System.Array.Find(texts, t => t != null);

        // Put it on the same panel the text lives on, so it moves/scales with
        // the window's background.
        RectTransform panel = reference != null && reference.transform.parent is RectTransform p
            ? p
            : (RectTransform)canvas.transform;

        // The XR ray can only press UI on a canvas with this raycaster.
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvas.gameObject);

        // The panel is a VerticalLayoutGroup (+ ContentSizeFitter), which
        // would stretch a direct child into a thin full-width row. Instead the
        // button goes in its own footer row that right-aligns it, so it sits
        // in the bottom-right corner and the panel grows to fit it rather
        // than the button covering body text.
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(footer, "Add Read Aloud Button");
        footer.layer = panel.gameObject.layer;
        footer.transform.SetParent(panel, false);
        footer.transform.SetAsLastSibling();

        HorizontalLayoutGroup row = footer.GetComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleRight;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        footer.GetComponent<LayoutElement>().minHeight = ButtonHeight;

        GameObject buttonGO = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonGO.layer = panel.gameObject.layer;
        buttonGO.transform.SetParent(footer.transform, false);

        LayoutElement size = buttonGO.GetComponent<LayoutElement>();
        size.preferredWidth = ButtonWidth;
        size.preferredHeight = ButtonHeight;

        Image image = buttonGO.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 0.97f, 0.9f, 1f);
        // Match the panel's material (e.g. an always-on-top UI material) so
        // the button draws the same way the window does.
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null && panelImage.material != panelImage.defaultMaterial)
            image.material = panelImage.material;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.layer = buttonGO.layer;
        labelGO.transform.SetParent(buttonGO.transform, false);
        RectTransform labelRT = (RectTransform)labelGO.transform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "A  Listen";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 34f;
        label.enableWordWrapping = false;
        label.color = new Color(0.25f, 0.18f, 0.1f);
        label.raycastTarget = false;
        if (reference != null)
        {
            label.font = reference.font;
            label.fontSharedMaterial = reference.fontSharedMaterial;
        }

        ReadAloudButton readAloud = buttonGO.AddComponent<ReadAloudButton>();
        readAloud.texts = texts;

        Debug.Log("Read Aloud: added a button to " + canvas.name + ".", buttonGO);
        return true;
    }
}
