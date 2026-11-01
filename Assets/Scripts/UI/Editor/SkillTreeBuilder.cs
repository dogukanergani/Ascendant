using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the skill tree prefabs, applies the tier/reward tuning to the data assets and adds the panel to the
// PlayerTest HUD. Run via Tools/UI/Build Skill Tree.
public static class SkillTreeBuilder
{
    const string PrefabDir = "Assets/Prefabs/UI";
    const string ScenePath = "Assets/Scenes/PlayerTest.unity";

    static readonly Color Background = new(0.08f, 0.09f, 0.14f, 1f);
    static readonly Color Panel = new(0.14f, 0.16f, 0.24f, 1f);
    static readonly Color CardColor = new(0.1f, 0.11f, 0.18f, 1f);
    static readonly Color ButtonColor = new(0.25f, 0.45f, 0.8f, 1f);
    static readonly Color Gold = new(1f, 0.85f, 0.3f, 1f);
    static readonly Color TextColor = new(0.95f, 0.95f, 0.9f, 1f);

    static TMP_FontAsset font;
    static Sprite square;

    [MenuItem("Tools/UI/Build Skill Tree")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{PrefabDir}/PixelFont.asset");
        square = AssetDatabase.LoadAssetAtPath<Sprite>($"{PrefabDir}/Sprites/square.png");

        ApplyTuning();
        SkillCardView card = BuildCardPrefab();
        BuildPanelPrefab(card);
        AddToScene();
        AssetDatabase.SaveAssets();
        Debug.Log("SkillTreeBuilder: done.");
    }

    // ---- data ----

    static void ApplyTuning()
    {
        SetSkill("Skill_Dash", new[] { 20f, 15f, 10f }, new[] { 1f, 1.3f, 1.6f },
            new[] { "Mesafe x1.0, stamina 20", "Mesafe +%30, stamina 15", "Mesafe +%60, stamina 10" });
        SetSkill("Skill_SpellBolt", null, null, new[] { "Alan yarıçapı 1.5", "Alan yarıçapı 2.0", "Alan yarıçapı 2.6" });
        SetSkill("Skill_Staff", null, null, null);
        SetReward("Orc1Stats", 1);
        SetReward("Orc2Stats", 1);
        SetReward("Orc3Stats", 2);
    }

    static void SetSkill(string assetName, float[] stamina, float[] reach, string[] notes)
    {
        SkillDefinition asset = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/Scripts/Skills/Data/{assetName}.asset");
        SerializedObject so = new(asset);
        SerializedProperty costs = so.FindProperty("upgradeCosts");
        costs.arraySize = 2;
        costs.GetArrayElementAtIndex(0).intValue = 5;
        costs.GetArrayElementAtIndex(1).intValue = 12;
        SetFloats(so.FindProperty("tierStaminaCost"), stamina);
        SetFloats(so.FindProperty("tierReachMultiplier"), reach);
        SerializedProperty noteProp = so.FindProperty("tierNotes");
        noteProp.arraySize = notes?.Length ?? 0;
        for (int i = 0; i < noteProp.arraySize; i++)
        {
            noteProp.GetArrayElementAtIndex(i).stringValue = notes[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    static void SetFloats(SerializedProperty prop, float[] values)
    {
        prop.arraySize = values?.Length ?? 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            prop.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }

    static void SetReward(string assetName, int reward)
    {
        EnemyStats asset = AssetDatabase.LoadAssetAtPath<EnemyStats>($"Assets/Scripts/Enemies/Data/{assetName}.asset");
        asset.skillPointReward = reward;
        EditorUtility.SetDirty(asset);
    }

    // ---- prefabs ----

    static SkillCardView BuildCardPrefab()
    {
        RectTransform root = NewRect("SkillTreeCard", null);
        root.sizeDelta = new Vector2(320, 330);

        Image selection = NewImage("Selection", root, Gold);
        Stretch(selection.rectTransform);
        selection.rectTransform.offsetMin = new Vector2(-5, -5);
        selection.rectTransform.offsetMax = new Vector2(5, 5);
        selection.enabled = false;

        Image bg = NewImage("Background", root, CardColor);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        Image icon = NewImage("Icon", root, Color.white);
        icon.sprite = null;
        Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(88, 88));

        TMP_Text nameText = NewText("Name", root, "Skill", 36);
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(300, 44));

        RectTransform pipRow = NewRect("Pips", root);
        Place(pipRow, new Vector2(0.5f, 1f), new Vector2(0, -158), new Vector2(120, 28));
        HorizontalLayoutGroup pipLayout = pipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        pipLayout.spacing = 12;
        pipLayout.childAlignment = TextAnchor.MiddleCenter;
        pipLayout.childControlWidth = pipLayout.childControlHeight = false;
        pipLayout.childForceExpandWidth = pipLayout.childForceExpandHeight = false;
        Image[] pips = new Image[3];
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i] = NewImage($"Pip{i + 1}", pipRow, Color.gray);
            pips[i].rectTransform.sizeDelta = new Vector2(28, 28);
        }

        TMP_Text cost = NewText("Cost", root, "Maliyet: 5", 28);
        cost.color = Gold;
        Place(cost.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -194), new Vector2(300, 34));

        TMP_Text effect = NewText("Effect", root, "Hasar 22-28", 26);
        effect.alignment = TextAlignmentOptions.Top;
        Place(effect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -232), new Vector2(300, 80));

        Image buttonBg = NewImage("UpgradeButton", root, ButtonColor);
        buttonBg.raycastTarget = true;
        Place(buttonBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(240, 48));
        // Sits at the bottom edge: pivot at its bottom centre.
        buttonBg.rectTransform.pivot = new Vector2(0.5f, 0f);
        Button button = buttonBg.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonBg;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.32f, 0.33f, 0.38f, 1f);
        button.colors = colors;
        TMP_Text label = NewText("Label", buttonBg.transform, "Yükselt", 30);
        Stretch(label.rectTransform);

        SkillCardView view = root.gameObject.AddComponent<SkillCardView>();
        SetRefs(view, ("selection", selection), ("icon", icon), ("nameText", nameText), ("pips", pips), ("costText", cost),
            ("effectText", effect), ("upgradeButton", button), ("upgradeLabel", label));
        return SavePrefab<SkillCardView>(root.gameObject, "SkillTreeCard");
    }

    static void BuildPanelPrefab(SkillCardView cardPrefab)
    {
        RectTransform root = NewRect("SkillTreePanel", null);
        Stretch(root);

        // Always-visible part: point balance under the timer and the "+N" toast.
        TMP_Text hudBalance = NewText("PointsHud", root, "Puan: 0", 40, TextAlignmentOptions.TopRight);
        hudBalance.color = Gold;
        Place(hudBalance.rectTransform, new Vector2(1f, 1f), new Vector2(-32, -100), new Vector2(360, 50));
        hudBalance.rectTransform.pivot = new Vector2(1f, 1f);
        TMP_Text toast = NewText("PointsToast", root, "+1", 44, TextAlignmentOptions.TopRight);
        toast.color = Gold;
        Place(toast.rectTransform, new Vector2(1f, 1f), new Vector2(-32, -150), new Vector2(360, 56));
        toast.rectTransform.pivot = new Vector2(1f, 1f);

        // The screen itself, hidden until opened.
        RectTransform content = NewRect("Content", root);
        Stretch(content);
        Image dim = NewImage("Dim", content, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image frame = NewImage("Frame", content, Panel);
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1420, 860));

        TMP_Text title = NewText("Title", frame.transform, "BECERİLER", 56, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40, -20), new Vector2(700, 70));
        title.rectTransform.pivot = new Vector2(0f, 1f);
        TMP_Text balance = NewText("Balance", frame.transform, "Puan: 0", 48, TextAlignmentOptions.Right);
        balance.color = Gold;
        Place(balance.rectTransform, new Vector2(1f, 1f), new Vector2(-40, -26), new Vector2(500, 60));
        balance.rectTransform.pivot = new Vector2(1f, 1f);

        RectTransform grid = NewRect("Cards", frame.transform);
        Place(grid, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1340, 680));
        grid.pivot = new Vector2(0.5f, 1f);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(320, 330);
        layout.spacing = new Vector2(20, 20);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        layout.childAlignment = TextAnchor.UpperCenter;

        TMP_Text hint = NewText("Hint", frame.transform, "Ok tuşları: Gezin     Enter: Yükselt     K / Esc: Kapat", 26);
        hint.color = new Color(0.7f, 0.72f, 0.8f, 1f);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(1300, 36));
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);

        SkillTreePanel panel = root.gameObject.AddComponent<SkillTreePanel>();
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        SetRefs(panel, ("actions", actions), ("content", content.gameObject), ("cardContainer", grid), ("cardPrefab", cardPrefab),
            ("balanceText", balance), ("hudBalanceText", hudBalance), ("toastText", toast));
        SavePrefab<SkillTreePanel>(root.gameObject, "SkillTreePanel");
    }

    // ---- scene ----

    static void AddToScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/SkillTreePanel.prefab");

        Canvas hud = null;
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.name == "HUD")
            {
                hud = canvas;
            }
        }

        if (hud == null)
        {
            Debug.LogError("SkillTreeBuilder: no HUD canvas in PlayerTest.");
            return;
        }

        foreach (SkillTreePanel old in hud.GetComponentsInChildren<SkillTreePanel>(true))
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, hud.transform);
        instance.transform.SetAsLastSibling();

        // Mouse clicks on the panel need an EventSystem.
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    // ---- helpers ----

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = square;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = TextColor;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Anchors and pivot at the same point (x, y of anchor), offset by position.
    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, anchor.y);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void SetRefs(Object target, params (string field, object value)[] refs)
    {
        SerializedObject so = new(target);
        foreach ((string field, object value) in refs)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (value is System.Array array)
            {
                prop.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = (Object)array.GetValue(i);
                }
            }
            else
            {
                prop.objectReferenceValue = (Object)value;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T SavePrefab<T>(GameObject root, string name) where T : Component
    {
        string path = $"{PrefabDir}/{name}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved.GetComponent<T>();
    }
}
