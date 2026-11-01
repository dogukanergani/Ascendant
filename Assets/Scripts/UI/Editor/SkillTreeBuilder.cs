using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the skill tree prefabs, applies the tier/reward tuning to the data assets and wires the panel into the
// level select and score screens (not the game). Run via Tools/UI/Build Skill Tree.
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
        BuildPointsHudPrefab();
        BuildScoreScreen();
        BuildPlayerTest();
        BuildLevelSelect();
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

        // The screen itself, hidden until opened from the level select or score screen.
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

        TMP_Text hint = NewText("Hint", frame.transform, "Ok tuşları: Gezin     Enter: Yükselt     Esc: Kapat", 26);
        hint.color = new Color(0.7f, 0.72f, 0.8f, 1f);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(-120, 16), new Vector2(1100, 36));
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);

        Button close = NewButton("CloseButton", frame.transform, "Kapat", new Vector2(200, 56));
        Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(-30, 14), new Vector2(200, 56));
        ((RectTransform)close.transform).pivot = new Vector2(1f, 0f);

        SkillTreePanel panel = root.gameObject.AddComponent<SkillTreePanel>();
        SetRefs(panel, ("content", content.gameObject), ("cardContainer", grid), ("cardPrefab", cardPrefab),
            ("balanceText", balance), ("closeButton", close));
        SavePrefab<SkillTreePanel>(root.gameObject, "SkillTreePanel");
    }

    // Balance text (top right) and the "+N" toast under it.
    static void BuildPointsHudPrefab()
    {
        RectTransform root = NewRect("SkillPointsHud", null);
        Stretch(root);
        TMP_Text balance = NewText("Balance", root, "Puan: 0", 40, TextAlignmentOptions.TopRight);
        balance.color = Gold;
        Place(balance.rectTransform, new Vector2(1f, 1f), new Vector2(-32, -100), new Vector2(360, 50));
        balance.rectTransform.pivot = new Vector2(1f, 1f);
        TMP_Text toast = NewText("Toast", root, "+1", 44, TextAlignmentOptions.TopRight);
        toast.color = Gold;
        Place(toast.rectTransform, new Vector2(1f, 1f), new Vector2(-32, -150), new Vector2(360, 56));
        toast.rectTransform.pivot = new Vector2(1f, 1f);

        SkillPointsHud hud = root.gameObject.AddComponent<SkillPointsHud>();
        SetRefs(hud, ("balanceText", balance), ("toastText", toast));
        SavePrefab<SkillPointsHud>(root.gameObject, "SkillPointsHud");
    }

    // ---- scenes ----

    static GameObject InstantiateUnder(string prefabName, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetAsLastSibling();
        return instance;
    }

    static void SetDebugKey(GameObject pointsHud, bool value)
    {
        SerializedObject so = new(pointsHud.GetComponent<SkillPointsHud>());
        so.FindProperty("debugKey").boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // In-game: no skill tree, just the point balance and toast in the HUD.
    static void BuildPlayerTest()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

        foreach (SkillPointsHud old in hud.GetComponentsInChildren<SkillPointsHud>(true))
        {
            Object.DestroyImmediate(old.gameObject);
        }

        SetDebugKey(InstantiateUnder("SkillPointsHud", hud.transform), true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    // Level select: skill tree button, balance on top, and a menu-mode SkillManager that holds tiers for the panel.
    static void BuildLevelSelect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LevelSelect.unity", OpenSceneMode.Single);
        LevelSelectController controller = Object.FindFirstObjectByType<LevelSelectController>();
        Transform canvas = controller.transform;
        foreach (string old in new[] { "SkillTreeButton", "SkillTreePanel", "SkillPointsHud" })
        {
            Transform t = canvas.Find(old);
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        SkillManager existing = Object.FindFirstObjectByType<SkillManager>();
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject managerObject = new("SkillManager");
        SkillManager manager = managerObject.AddComponent<SkillManager>();
        SerializedObject managerSo = new(manager);
        managerSo.FindProperty("menuMode").boolValue = true;
        SerializedProperty catalog = managerSo.FindProperty("catalog");
        string[] skills = { "Skill_Staff", "Skill_Dash", "Skill_SpellBolt" };
        catalog.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
        {
            catalog.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/Scripts/Skills/Data/{skills[i]}.asset");
        }

        managerSo.ApplyModifiedPropertiesWithoutUndo();

        Button button = NewButton("SkillTreeButton", canvas, "Skill Ağacı", new Vector2(420, 100));
        Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(-60, 60), new Vector2(420, 100));
        ((RectTransform)button.transform).pivot = new Vector2(1f, 0f);
        SetDebugKey(InstantiateUnder("SkillPointsHud", canvas), true);
        GameObject panel = InstantiateUnder("SkillTreePanel", canvas);
        SetRefs(panel.GetComponent<SkillTreePanel>(), ("manager", manager));
        SetRefs(controller, ("skillTreeButton", button), ("skillTreePanel", panel.GetComponent<SkillTreePanel>()));
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    // Score screen: "Skill Ağacı" button, balance on top and a nested panel. The panel finds the level's SkillManager.
    static void BuildScoreScreen()
    {
        string path = $"{PrefabDir}/ScoreScreen.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ScoreScreen screen = root.GetComponent<ScoreScreen>();
        Transform panelRoot = root.transform.Find("Panel");
        Transform box = panelRoot.Find("Box");
        Transform oldButton = box.Find("SkillTreeButton");
        if (oldButton != null)
        {
            Object.DestroyImmediate(oldButton.gameObject);
        }

        foreach (string old in new[] { "SkillTreePanel", "SkillPointsHud" })
        {
            Transform t = root.transform.Find(old);
            if (t == null)
            {
                t = panelRoot.Find(old);
            }

            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // Tekrar / Levellar / Sonraki Level share the bottom row; Skill Ağacı sits above them.
        Button skill = NewButton("SkillTreeButton", box, "Skill Ağacı", new Vector2(520, 90));
        Place((RectTransform)skill.transform, new Vector2(0.5f, 0f), new Vector2(0, 160), new Vector2(520, 90));
        Transform next = box.Find("NextButton");
        next.GetComponentInChildren<TMP_Text>().text = "Sonraki Level";
        ((RectTransform)next).sizeDelta = new Vector2(300, 90);
        ((RectTransform)next).anchoredPosition = new Vector2(300, 50);
        ((RectTransform)box.Find("RetryButton")).anchoredPosition = new Vector2(-300, 50);

        // Inside Panel so the balance is only visible while the score screen is.
        GameObject pointsHud = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/SkillPointsHud.prefab"), panelRoot);
        SetDebugKey(pointsHud, false);
        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/SkillTreePanel.prefab"), root.transform);
        panel.transform.SetAsLastSibling();
        SetRefs(screen, ("skillTreeButton", skill), ("skillTreePanel", panel.GetComponent<SkillTreePanel>()));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Button NewButton(string name, Transform parent, string label, Vector2 size)
    {
        Image bg = NewImage(name, parent, ButtonColor);
        bg.rectTransform.sizeDelta = size;
        bg.raycastTarget = true;
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;
        TMP_Text text = NewText("Label", bg.transform, label, 40);
        Stretch(text.rectTransform);
        return button;
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
