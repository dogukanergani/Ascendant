using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Rebuilds every UI prefab and the two menu scenes from UITheme (Tools/UI/Rebuild All), and adds the pause menu
// to the level scenes. Prefabs are rebuilt in place: the root object and its components stay, so scene instances
// keep their overrides. Run Tools/UI/Rebuild Theme first when the theme's colors or sprites changed (Rebuild All does).
public static class UIBuilder
{
    const string PrefabDir = "Assets/Prefabs/UI";
    const string ScenesDir = "Assets/Scenes";
    static readonly string[] GameScenes = { ScenesDir + "/Level_01.unity", ScenesDir + "/PlayerTest.unity" };

    static UITheme T;
    static float Scale => T.pixelScale;
    static readonly HashSet<GameObject> loaded = new();

    [MenuItem("Tools/UI/Rebuild All")]
    public static void RebuildAll()
    {
        // Never throw away what the user has open.
        EditorSceneManager.SaveOpenScenes();
        T = UIThemeBuilder.Build();
        ApplyEnemyData();

        BuildSkillPointsHud();
        SkillSlotView slot = BuildSkillSlot();
        SkillCardView card = BuildSkillCard();
        BuildSkillTreePanel(card);
        BuildPauseMenu();
        BuildHud(slot);
        BuildScoreScreen();
        LevelButtonView levelCard = BuildLevelCard();
        AssetDatabase.SaveAssets();

        BuildMainMenu();
        BuildLevelSelect(levelCard);
        foreach (string path in GameScenes)
        {
            PatchGameScene(path);
        }

        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("UIBuilder: UI rebuilt.");
    }

    // The boss shows a health bar: mark its stats and give it a display name.
    static void ApplyEnemyData()
    {
        EnemyStats boss = AssetDatabase.LoadAssetAtPath<EnemyStats>("Assets/Scripts/Enemies/Data/Orc3BossStats.asset");
        if (boss != null && !boss.isBoss)
        {
            boss.isBoss = true;
            boss.displayName = "ORC ŞEFİ";
            EditorUtility.SetDirty(boss);
        }
    }

    // ---- element helpers ----

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 9-slice image whose art pixels are drawn Scale screen pixels big.
    static Image NewSliced(string name, Transform parent, Sprite sprite, Color? color = null)
    {
        Image image = NewImage(name, parent, sprite, color ?? Color.white);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / Scale;
        return image;
    }

    static Vector2 IconSize(Sprite sprite, float scale) => sprite.rect.size * scale;

    static Image NewIcon(string name, Transform parent, Sprite sprite, float scale)
    {
        Image image = NewImage(name, parent, sprite, Color.white);
        image.rectTransform.sizeDelta = IconSize(sprite, scale);
        return image;
    }

    static TMP_Text NewText(string name, Transform parent, string text, int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = bold ? T.fontBold : T.font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.richText = false;
        return t;
    }

    // Face text over a dark outline and a hard drop shadow, built from copies of the same text.
    static RectTransform NewTitle(string name, Transform parent, string text, int size, Color face, Color outline, Color shadow, int thickness, Color? faceBottom = null)
    {
        RectTransform root = NewRect(name, parent);
        Vector2 box = new(size * 12f, size * 1.6f);
        root.sizeDelta = box;
        Vector2[] ring =
        {
            new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1),
        };
        TMP_Text shadowText = NewText("Shadow", root, text, size, shadow, TextAlignmentOptions.Center, true);
        Stretch(shadowText.rectTransform);
        shadowText.rectTransform.anchoredPosition = new Vector2(thickness * 1.5f, -thickness * 2f);
        foreach (Vector2 dir in ring)
        {
            TMP_Text o = NewText("Outline", root, text, size, outline, TextAlignmentOptions.Center, true);
            Stretch(o.rectTransform);
            o.rectTransform.offsetMin = dir * thickness;
            o.rectTransform.offsetMax = dir * thickness;
        }

        TMP_Text main = NewText("Face", root, text, size, face, TextAlignmentOptions.Center, true);
        Stretch(main.rectTransform);
        if (faceBottom.HasValue)
        {
            main.enableVertexGradient = true;
            main.colorGradient = new VertexGradient(face, face, faceBottom.Value, faceBottom.Value);
        }

        return root;
    }

    static Button NewButton(string name, Transform parent, string label, Vector2 size, bool primary = false, bool marker = true, int fontSize = 0)
    {
        UITheme.ButtonSprites s = primary ? T.buttonPrimary : T.buttonDefault;
        Image bg = NewSliced(name, parent, s.normal);
        bg.raycastTarget = true;
        bg.rectTransform.sizeDelta = size;
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = s.hover,
            selectedSprite = s.hover,
            pressedSprite = s.pressed,
            disabledSprite = s.disabled,
        };
        int labelSize = fontSize > 0 ? fontSize : size.y >= 80f ? T.fontLarge : T.fontBody;
        TMP_Text text = NewText("Label", bg.transform, label, labelSize, primary ? T.border : T.text, TextAlignmentOptions.Center, labelSize >= T.fontLarge);
        Stretch(text.rectTransform, 8f);

        GameObject markerObject = null;
        if (marker)
        {
            markerObject = NewRect("SelectionMarker", bg.transform).gameObject;
            Stretch((RectTransform)markerObject.transform);
            Image left = NewIcon("ArrowLeft", markerObject.transform, T.arrowRight, 4f);
            Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), IconSize(T.arrowRight, 4f));
            Image right = NewIcon("ArrowRight", markerObject.transform, T.arrowLeft, 4f);
            Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), IconSize(T.arrowLeft, 4f));
            markerObject.SetActive(false);
        }

        UIButtonFx fx = bg.gameObject.AddComponent<UIButtonFx>();
        fx.Configure(T.hoverScale, T.pressedScale);
        SerializedObject so = new(fx);
        so.FindProperty("selectionMarker").objectReferenceValue = markerObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    static void SetRefs(Object target, params (string field, object value)[] refs)
    {
        SerializedObject so = new(target);
        foreach ((string field, object value) in refs)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"UIBuilder: {target.GetType().Name} has no field '{field}'.");
                continue;
            }

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
                prop.objectReferenceValue = value is Component or GameObject or Object ? (Object)value : null;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetValues(Object target, string field, string[] values)
    {
        SerializedObject so = new(target);
        SerializedProperty prop = so.FindProperty(field);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = new(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }

    static void ConfigureCanvas(GameObject root, int sortingOrder)
    {
        Canvas canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = GetOrAdd<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(root);
    }

    // ---- prefab plumbing ----

    // Opens a prefab for rebuilding with its children cleared (root object and root components kept), or creates it.
    static GameObject OpenRoot(string name)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        GameObject root;
        if (File.Exists(path))
        {
            root = PrefabUtility.LoadPrefabContents(path);
            loaded.Add(root);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
        }
        else
        {
            root = new GameObject(name, typeof(RectTransform));
        }

        return root;
    }

    static void CloseRoot(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        if (loaded.Remove(root))
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        else
        {
            Object.DestroyImmediate(root);
        }
    }

    static GameObject Nested(string prefabName, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    // ---- prefabs ----

    // [coin] 120 and a "+N" toast. Positioned by whoever nests it.
    static void BuildSkillPointsHud()
    {
        GameObject root = OpenRoot("SkillPointsHud");
        RectTransform rt = (RectTransform)root.transform;
        Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-T.safeMargin, -T.safeMargin), new Vector2(240f, 48f));
        Image coin = NewIcon("Coin", rt, T.coinIcon, 4f);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, IconSize(T.coinIcon, 4f));
        TMP_Text balance = NewText("Balance", rt, "0", T.fontLarge, T.accent, TextAlignmentOptions.MidlineLeft);
        Place(balance.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(170f, 56f));
        TMP_Text toast = NewText("Toast", rt, "+1", T.fontBody, T.success, TextAlignmentOptions.TopLeft);
        Place(toast.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(64f, -4f), new Vector2(170f, 44f));
        toast.gameObject.SetActive(false);
        SkillPointsHud hud = GetOrAdd<SkillPointsHud>(root);
        SetRefs(hud, ("balanceText", balance), ("toastText", toast));
        CloseRoot(root, "SkillPointsHud");
    }

    static SkillSlotView BuildSkillSlot()
    {
        GameObject root = OpenRoot("SkillSlot");
        RectTransform rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(84f, 84f);
        Image frame = NewSliced("Frame", rt, T.slotSprite);
        Stretch(frame.rectTransform);
        Image icon = NewImage("Icon", rt, null, Color.white);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        Image cooldown = NewImage("Cooldown", rt, T.whiteSprite, new Color(0f, 0f, 0f, 0.62f));
        Stretch(cooldown.rectTransform, 9f);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Radial360;
        cooldown.fillOrigin = (int)Image.Origin360.Top;
        cooldown.fillClockwise = false;
        cooldown.fillAmount = 0f;
        Image cover = NewImage("StaminaCover", rt, T.whiteSprite, new Color(T.staminaDark.r * 0.4f, T.staminaDark.g * 0.4f, T.staminaDark.b * 0.6f, 0.72f));
        Stretch(cover.rectTransform, 9f);
        cover.enabled = false;
        TMP_Text cdText = NewText("CooldownText", rt, "0.0", T.fontSmall, T.text);
        Stretch(cdText.rectTransform);
        Image badge = NewSliced("KeyBadge", rt, T.badgeSprite);
        Place(badge.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(8f, -8f), new Vector2(42f, 36f));
        TMP_Text key = NewText("Key", badge.transform, "F", T.fontSmall, T.accent);
        Stretch(key.rectTransform);
        cdText.gameObject.SetActive(false);
        SkillSlotView view = GetOrAdd<SkillSlotView>(root);
        SetRefs(view, ("icon", icon), ("cooldownOverlay", cooldown), ("staminaCover", cover), ("keyText", key),
            ("keyBadge", badge.gameObject), ("cooldownText", cdText));
        CloseRoot(root, "SkillSlot");
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/SkillSlot.prefab").GetComponent<SkillSlotView>();
    }

    static void BuildHud(SkillSlotView slotPrefab)
    {
        GameObject root = OpenRoot("HUD");
        ConfigureCanvas(root, 0);
        RectTransform rt = (RectTransform)root.transform;
        float m = T.safeMargin;

        // Hearts.
        RectTransform heartRow = NewRect("Hearts", rt);
        Vector2 heartSize = IconSize(T.heartFull, 4f);
        Place(heartRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, -m), new Vector2(5 * heartSize.x + 4 * 6f, heartSize.y));
        HorizontalLayoutGroup hearts = heartRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hearts.spacing = 6f;
        hearts.childControlWidth = hearts.childControlHeight = false;
        hearts.childForceExpandWidth = hearts.childForceExpandHeight = false;
        Image[] heartImages = new Image[5];
        for (int i = 0; i < heartImages.Length; i++)
        {
            heartImages[i] = NewImage($"Heart{i + 1}", heartRow, T.heartFull, Color.white);
            heartImages[i].rectTransform.sizeDelta = heartSize;
        }

        // Stamina bar under the hearts.
        float barY = -m - heartSize.y - 14f;
        Image barFrame = NewSliced("StaminaBar", rt, T.barFrameSprite);
        RectTransform bar = barFrame.rectTransform;
        Place(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, barY), new Vector2(5 * heartSize.x + 4 * 6f + 24f, 36f));
        Image fill = NewImage("Fill", bar, T.barFillSprite, T.stamina);
        Stretch(fill.rectTransform, 9f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        // Skill slots.
        RectTransform slotRow = NewRect("Skills", rt);
        Place(slotRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, barY - 36f - 20f), new Vector2(760f, 84f));
        HorizontalLayoutGroup slots = slotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        slots.spacing = 14f;
        slots.childControlWidth = slots.childControlHeight = false;
        slots.childForceExpandWidth = slots.childForceExpandHeight = false;

        // Top-right plate: timer and skill points.
        Image plate = NewSliced("InfoPanel", rt, T.panelSprite);
        Place(plate.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -m), new Vector2(300f, 132f));
        Image clock = NewIcon("Clock", plate.transform, T.clockIcon, 4f);
        Place(clock.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, -42f), IconSize(T.clockIcon, 4f));
        TMP_Text timerText = NewText("Timer", plate.transform, "00:00", T.fontLarge, T.text, TextAlignmentOptions.MidlineRight);
        Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-30f, -42f), new Vector2(190f, 56f));
        GameObject points = Nested("SkillPointsHud", plate.transform);
        Place((RectTransform)points.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(30f, 38f), new Vector2(240f, 48f));
        SetBool(points.GetComponent<SkillPointsHud>(), "debugKey", true);

        // Boss bar, top centre.
        GameObject bossRoot = NewRect("BossBar", rt).gameObject;
        RectTransform bossRect = (RectTransform)bossRoot.transform;
        Place(bossRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -m), new Vector2(920f, 100f));
        CanvasGroup bossGroup = bossRoot.AddComponent<CanvasGroup>();
        bossGroup.alpha = 0f;
        bossGroup.blocksRaycasts = false;
        bossGroup.interactable = false;
        RectTransform slide = NewRect("Slide", bossRect);
        Stretch(slide);
        TMP_Text bossName = NewText("Name", slide, "ORC ŞEFİ", T.fontBody, T.text, TextAlignmentOptions.Center, true);
        Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(920f, 48f));
        Image bossFrame = NewSliced("Frame", slide, T.barFrameSprite);
        Place(bossFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(920f, 46f));
        Image bossGhost = NewImage("Ghost", bossFrame.transform, T.barFillSprite, new Color(1f, 0.85f, 0.7f, 1f));
        Stretch(bossGhost.rectTransform, 9f);
        bossGhost.type = Image.Type.Filled;
        bossGhost.fillMethod = Image.FillMethod.Horizontal;
        Image bossFill = NewImage("Fill", bossFrame.transform, T.barFillSprite, T.health);
        Stretch(bossFill.rectTransform, 9f);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        BossHealthBar bossBar = bossRoot.AddComponent<BossHealthBar>();
        SetRefs(bossBar, ("group", bossGroup), ("slideRoot", slide), ("nameText", bossName), ("fill", bossFill), ("ghost", bossGhost));

        GetOrAdd<LevelTimer>(root);
        HudController hud = GetOrAdd<HudController>(root);
        SetRefs(hud, ("theme", T), ("hearts", heartImages), ("heartFull", T.heartFull), ("heartHalf", T.heartHalf),
            ("heartEmpty", T.heartEmpty), ("staminaFill", fill), ("staminaBar", bar), ("staminaFrame", barFrame),
            ("slotPrefab", slotPrefab), ("slotContainer", slotRow), ("timerText", timerText), ("timer", root.GetComponent<LevelTimer>()));
        CloseRoot(root, "HUD");
    }

    static SkillCardView BuildSkillCard()
    {
        GameObject root = OpenRoot("SkillTreeCard");
        RectTransform rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(340f, 376f);
        Image bg = NewSliced("Background", rt, T.cardSprite);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        Image slot = NewSliced("IconSlot", rt, T.slotSprite);
        Place(slot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(84f, 84f));
        Image icon = NewImage("Icon", slot.transform, null, Color.white);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));

        TMP_Text nameText = NewText("Name", rt, "Skill", T.fontBody, T.text);
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(320f, 44f));

        RectTransform pipRow = NewRect("Pips", rt);
        Place(pipRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(120f, 27f));
        HorizontalLayoutGroup pipLayout = pipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        pipLayout.spacing = 10f;
        pipLayout.childAlignment = TextAnchor.MiddleCenter;
        pipLayout.childControlWidth = pipLayout.childControlHeight = false;
        pipLayout.childForceExpandWidth = pipLayout.childForceExpandHeight = false;
        Image[] pips = new Image[3];
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i] = NewImage($"Pip{i + 1}", pipRow, T.pipEmpty, Color.white);
            pips[i].rectTransform.sizeDelta = IconSize(T.pipEmpty, 3f);
        }

        TMP_Text effect = NewText("Effect", rt, "Hasar 22-28", T.fontSmall, T.textDim, TextAlignmentOptions.Top);
        effect.textWrappingMode = TextWrappingModes.Normal;
        effect.overflowMode = TextOverflowModes.Truncate;
        Place(effect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(304f, 120f));

        Button button = NewButton("UpgradeButton", rt, "YÜKSELT", new Vector2(304f, 60f), true, false);
        Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(304f, 60f));
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        SerializedObject fxSo = new(button.GetComponent<UIButtonFx>());
        fxSo.FindProperty("selectOnHover").boolValue = false;
        fxSo.ApplyModifiedPropertiesWithoutUndo();
        TMP_Text label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.rectTransform.offsetMax = new Vector2(-96f, -8f);
        Image costIcon = NewIcon("CostCoin", button.transform, T.coinIcon, 3f);
        Place(costIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-62f, 0f), IconSize(T.coinIcon, 3f));
        TMP_Text cost = NewText("Cost", button.transform, "5", T.fontBody, T.border, TextAlignmentOptions.MidlineRight);
        Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(44f, 44f));

        SkillCardView view = GetOrAdd<SkillCardView>(root);
        SetRefs(view, ("theme", T), ("background", bg), ("icon", icon), ("nameText", nameText), ("pips", pips), ("effectText", effect),
            ("upgradeButton", button), ("upgradeLabel", label), ("costText", cost), ("costIcon", costIcon.gameObject));
        CloseRoot(root, "SkillTreeCard");
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/SkillTreeCard.prefab").GetComponent<SkillCardView>();
    }

    static void BuildSkillTreePanel(SkillCardView cardPrefab)
    {
        GameObject root = OpenRoot("SkillTreePanel");
        RectTransform rt = (RectTransform)root.transform;
        Stretch(rt);

        RectTransform content = NewRect("Content", rt);
        Stretch(content);
        Image dim = NewImage("Dim", content, T.whiteSprite, T.dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image frame = NewSliced("Frame", content, T.panelSprite);
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 1008f));

        TMP_Text title = NewText("Title", frame.transform, "BECERİLER", T.fontTitle, T.accent, TextAlignmentOptions.TopLeft, true);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -34f), new Vector2(800f, 90f));
        Image coin = NewIcon("Coin", frame.transform, T.coinIcon, 4f);
        Place(coin.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-236f, -62f), IconSize(T.coinIcon, 4f));
        TMP_Text balance = NewText("Balance", frame.transform, "0", T.fontLarge, T.accent, TextAlignmentOptions.MidlineLeft);
        Place(balance.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-48f, -62f), new Vector2(170f, 56f));

        Image divider = NewImage("Divider", frame.transform, T.whiteSprite, T.border);
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1464f, 6f));

        RectTransform grid = NewRect("Cards", frame.transform);
        Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1420f, 772f));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(340f, 376f);
        layout.spacing = new Vector2(20f, 20f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        layout.childAlignment = TextAnchor.UpperCenter;

        TMP_Text hint = NewText("Hint", frame.transform, "OK TUŞLARI: GEZİN    ENTER: YÜKSELT    ESC: KAPAT", T.fontSmall, T.textDim, TextAlignmentOptions.MidlineLeft);
        Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(1000f, 40f));
        Button close = NewButton("CloseButton", frame.transform, "KAPAT", new Vector2(240f, 64f), false, false);
        Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-48f, 48f), new Vector2(240f, 64f));

        SkillTreePanel panel = GetOrAdd<SkillTreePanel>(root);
        SetRefs(panel, ("content", content.gameObject), ("cardContainer", grid), ("cardPrefab", cardPrefab),
            ("balanceText", balance), ("closeButton", close));
        SetValues(panel, "lockedSkillNames", new string[0]);
        // Hidden in the editor too; SkillTreePanel.Open() shows it.
        content.gameObject.SetActive(false);
        CloseRoot(root, "SkillTreePanel");
    }

    static void BuildPauseMenu()
    {
        GameObject root = OpenRoot("PauseMenu");
        ConfigureCanvas(root, 20);
        RectTransform rt = (RectTransform)root.transform;
        RectTransform content = NewRect("Content", rt);
        Stretch(content);
        Image dim = NewImage("Dim", content, T.whiteSprite, T.dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image box = NewSliced("Panel", content, T.panelSprite);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 600f));
        TMP_Text title = NewText("Title", box.transform, "DURAKLATILDI", T.fontLarge, T.accent, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(560f, 64f));
        Image divider = NewImage("Divider", box.transform, T.whiteSprite, T.border);
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(520f, 6f));

        Vector2 size = new(460f, 92f);
        Button resume = NewButton("ResumeButton", box.transform, "DEVAM", size, true);
        Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), size);
        Button select = NewButton("LevelSelectButton", box.transform, "LEVEL SEÇ", size);
        Place((RectTransform)select.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), size);
        Button menu = NewButton("MainMenuButton", box.transform, "ANA MENÜ", size);
        Place((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -408f), size);
        TMP_Text hint = NewText("Hint", box.transform, "ESC: DEVAM", T.fontSmall, T.textDim);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(400f, 36f));

        PauseMenu pause = GetOrAdd<PauseMenu>(root);
        SetRefs(pause, ("content", content.gameObject), ("resumeButton", resume), ("levelSelectButton", select), ("mainMenuButton", menu));
        CloseRoot(root, "PauseMenu");
    }

    static void BuildScoreScreen()
    {
        GameObject root = OpenRoot("ScoreScreen");
        ConfigureCanvas(root, 10);
        RectTransform rt = (RectTransform)root.transform;

        RectTransform panel = NewRect("Panel", rt);
        Stretch(panel);
        Image dim = NewImage("Dim", panel, T.whiteSprite, T.dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image box = NewSliced("Box", panel, T.panelSprite);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 900f));

        RectTransform title = NewTitle("Title", box.transform, "LEVEL TAMAM!", T.fontTitle, T.accent, T.border, new Color(0f, 0f, 0f, 0.5f), 3, new Color(1f, 0.95f, 0.7f));
        Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(880f, 110f));

        // Stars.
        RectTransform starsRt = NewRect("Stars", box.transform);
        Place(starsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(520f, 120f));
        HorizontalLayoutGroup starLayout = starsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        starLayout.spacing = 30f;
        starLayout.childAlignment = TextAnchor.MiddleCenter;
        starLayout.childControlWidth = starLayout.childControlHeight = false;
        starLayout.childForceExpandWidth = starLayout.childForceExpandHeight = false;
        Image[] starImages = new Image[ProgressService.MaxStars];
        for (int i = 0; i < starImages.Length; i++)
        {
            starImages[i] = NewImage($"Star{i + 1}", starsRt, T.starEmpty, Color.white);
            starImages[i].rectTransform.sizeDelta = IconSize(T.starEmpty, 10f);
        }

        StarRow starRow = starsRt.gameObject.AddComponent<StarRow>();
        SetRefs(starRow, ("stars", starImages), ("filledSprite", T.starFull), ("emptySprite", T.starEmpty));

        // Result rows.
        TMP_Text time = ResultRow(box.transform, "Time", "SÜRE", -330f);
        TMP_Text kills = ResultRow(box.transform, "Collected", "DÜŞMAN", -410f);
        TMP_Text points = ResultRow(box.transform, "Points", "PUAN", -490f);

        GameObject pointsHud = Nested("SkillPointsHud", panel);
        Place((RectTransform)pointsHud.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-T.safeMargin, -T.safeMargin), new Vector2(240f, 48f));
        SetBool(pointsHud.GetComponent<SkillPointsHud>(), "debugKey", false);

        Button skill = NewButton("SkillTreeButton", box.transform, "SKILL AĞACI", new Vector2(560f, 88f));
        Place((RectTransform)skill.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(560f, 88f));
        Vector2 small = new(280f, 88f);
        Button retry = NewButton("RetryButton", box.transform, "TEKRAR", small, false, false);
        Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-300f, 60f), small);
        Button menu = NewButton("MenuButton", box.transform, "LEVELLAR", small, false, false);
        Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), small);
        Button next = NewButton("NextButton", box.transform, "SONRAKİ", small, true, false);
        Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 60f), small);

        GameObject treePanel = Nested("SkillTreePanel", rt);
        treePanel.transform.SetAsLastSibling();

        ScoreScreen screen = GetOrAdd<ScoreScreen>(root);
        SetRefs(screen, ("panel", panel.gameObject), ("timeText", time), ("collectedText", kills), ("pointsText", points),
            ("starRow", starRow), ("nextButton", next), ("retryButton", retry), ("menuButton", menu), ("skillTreeButton", skill),
            ("skillTreePanel", treePanel.GetComponent<SkillTreePanel>()));
        CloseRoot(root, "ScoreScreen");
    }

    // "LABEL ........ value" with a divider under it. Returns the value text.
    static TMP_Text ResultRow(Transform parent, string name, string label, float y)
    {
        RectTransform row = NewRect(name + "Row", parent);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 64f));
        TMP_Text left = NewText("Label", row, label, T.fontBody, T.textDim, TextAlignmentOptions.MidlineLeft);
        Stretch(left.rectTransform);
        TMP_Text value = NewText(name, row, "0", T.fontLarge, T.text, TextAlignmentOptions.MidlineRight);
        Stretch(value.rectTransform);
        Image line = NewImage("Line", row, T.whiteSprite, T.border);
        Place(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(760f, 4f));
        return value;
    }

    static LevelButtonView BuildLevelCard()
    {
        GameObject root = OpenRoot("LevelButton");
        RectTransform rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(300f, 420f);
        Image bg = GetOrAdd<Image>(root);
        bg.sprite = T.buttonDefault.normal;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1f / Scale;
        bg.color = Color.white;
        bg.raycastTarget = true;
        Button button = GetOrAdd<Button>(root);
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = T.buttonDefault.hover,
            selectedSprite = T.buttonDefault.hover,
            pressedSprite = T.buttonDefault.pressed,
            disabledSprite = T.buttonDefault.disabled,
        };
        UIButtonFx fx = GetOrAdd<UIButtonFx>(root);
        fx.Configure(T.hoverScale, T.pressedScale);

        TMP_Text number = NewText("Number", rt, "LEVEL 1", T.fontSmall, T.text);
        Place(number.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(280f, 32f));
        Image preview = NewSliced("Preview", rt, T.slotSprite);
        Place(preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(236f, 148f));
        TMP_Text bigNumber = NewText("BigNumber", preview.transform, "1", T.fontHuge, T.text, TextAlignmentOptions.Center, true);
        Stretch(bigNumber.rectTransform);
        Image lockIcon = NewIcon("Lock", preview.transform, T.lockIcon, 6f);
        Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, IconSize(T.lockIcon, 6f));
        TMP_Text nameText = NewText("Name", rt, "Level", T.fontBody, T.text);
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -238f), new Vector2(284f, 44f));

        RectTransform starsRt = NewRect("Stars", rt);
        Place(starsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(200f, 44f));
        HorizontalLayoutGroup starLayout = starsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        starLayout.spacing = 8f;
        starLayout.childAlignment = TextAnchor.MiddleCenter;
        starLayout.childControlWidth = starLayout.childControlHeight = false;
        starLayout.childForceExpandWidth = starLayout.childForceExpandHeight = false;
        Image[] starImages = new Image[ProgressService.MaxStars];
        for (int i = 0; i < starImages.Length; i++)
        {
            starImages[i] = NewImage($"Star{i + 1}", starsRt, T.starEmpty, Color.white);
            starImages[i].rectTransform.sizeDelta = IconSize(T.starEmpty, 4f);
        }

        StarRow row = starsRt.gameObject.AddComponent<StarRow>();
        SetRefs(row, ("stars", starImages), ("filledSprite", T.starFull), ("emptySprite", T.starEmpty));

        TMP_Text status = NewText("Status", rt, "YENİ", T.fontSmall, T.accent);
        Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(280f, 32f));

        LevelButtonView view = GetOrAdd<LevelButtonView>(root);
        SetRefs(view, ("button", button), ("numberText", number), ("bigNumberText", bigNumber), ("nameText", nameText),
            ("statusText", status), ("lockIcon", lockIcon.gameObject), ("starRow", row));
        CloseRoot(root, "LevelButton");
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/LevelButton.prefab").GetComponent<LevelButtonView>();
    }

    // ---- scenes ----

    static Scene previousActive;

    static Scene BeginScene()
    {
        previousActive = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);
        return scene;
    }

    static void EndScene(Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(scene, true);
    }

    static void AddCameraAndEvents()
    {
        GameObject cam = new("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        Camera camera = cam.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = T.background;
        camera.orthographic = true;
        GameObject es = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static Canvas NewSceneCanvas()
    {
        GameObject go = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ConfigureCanvas(go, 0);
        return go.GetComponent<Canvas>();
    }

    // Parallax cave backdrop with floating embers and a darkening vignette at the edges.
    static MenuBackground NewBackground(Transform canvas)
    {
        RectTransform root = NewRect("Background", canvas);
        Stretch(root);
        Image solid = NewImage("Solid", root, T.whiteSprite, T.background);
        Stretch(solid.rectTransform);

        RawImage far = NewRaw("FarLayer", root, "Assets/Art/Environment/parallax_far.png", new Color(0.62f, 0.56f, 0.82f, 1f));
        RawImage near = NewRaw("NearLayer", root, "Assets/Art/Environment/parallax_near.png", new Color(0.42f, 0.36f, 0.68f, 1f));

        RectTransform embers = NewRect("Embers", root);
        Stretch(embers);
        Image template = NewImage("EmberTemplate", embers, T.whiteSprite, Color.white);
        template.rectTransform.sizeDelta = new Vector2(6f, 6f);

        // Soft darkening towards the top and bottom keeps the text readable.
        Image top = NewImage("ShadeTop", root, T.whiteSprite, new Color(T.background.r, T.background.g, T.background.b, 0.5f));
        Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1920f, 120f));
        top.rectTransform.anchorMax = new Vector2(1f, 1f);
        top.rectTransform.sizeDelta = new Vector2(0f, 120f);
        Image bottom = NewImage("ShadeBottom", root, T.whiteSprite, new Color(T.background.r, T.background.g, T.background.b, 0.55f));
        Place(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(1920f, 140f));
        bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottom.rectTransform.sizeDelta = new Vector2(0f, 140f);

        MenuBackground bg = root.gameObject.AddComponent<MenuBackground>();
        SetRefs(bg, ("farLayer", far), ("nearLayer", near), ("emberArea", embers), ("emberTemplate", template));
        return bg;
    }

    static RawImage NewRaw(string name, Transform parent, string texturePath, Color tint)
    {
        RectTransform rt = NewRect(name, parent);
        Stretch(rt);
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        raw.color = tint;
        raw.raycastTarget = false;
        return raw;
    }

    static void BuildMainMenu()
    {
        Scene scene = BeginScene();
        AddCameraAndEvents();
        Canvas canvas = NewSceneCanvas();
        RectTransform root = (RectTransform)canvas.transform;
        NewBackground(root);

        RectTransform title = NewTitle("Title", root, "ASCENDANT", 168, T.accent, new Color(0.1f, 0.05f, 0.2f), new Color(0f, 0f, 0f, 0.55f), 8, new Color(1f, 0.93f, 0.62f));
        Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(1500f, 220f));
        title.gameObject.AddComponent<UIBob>();
        Image rule = NewImage("Rule", root, T.whiteSprite, T.accentDark);
        Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(560f, 6f));

        Vector2 size = T.buttonSize;
        Button start = NewButton("StartButton", root, "BAŞLA", size, true);
        Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), size);
        Button select = NewButton("LevelSelectButton", root, "LEVEL SEÇ", size);
        Place((RectTransform)select.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), size);
        Button quit = NewButton("QuitButton", root, "ÇIKIŞ", size);
        Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), size);
        TMP_Text message = NewText("Message", root, string.Empty, T.fontBody, T.staminaLow);
        Place(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1000f, 56f));
        TMP_Text hint = NewText("Hint", root, "GEZİN: OK TUŞLARI    SEÇ: ENTER", T.fontSmall, T.textDim);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, T.safeMargin), new Vector2(1000f, 36f));

        MainMenuController controller = root.gameObject.AddComponent<MainMenuController>();
        SetRefs(controller, ("startButton", start), ("levelSelectButton", select), ("quitButton", quit), ("messageText", message));
        EndScene(scene, $"{ScenesDir}/MainMenu.unity");
    }

    static void BuildLevelSelect(LevelButtonView levelCard)
    {
        Scene scene = BeginScene();
        AddCameraAndEvents();
        Canvas canvas = NewSceneCanvas();
        RectTransform root = (RectTransform)canvas.transform;
        NewBackground(root);

        // A menu-mode SkillManager holds the tiers the skill tree panel edits.
        GameObject managerObject = new("SkillManager");
        SkillManager manager = managerObject.AddComponent<SkillManager>();
        SerializedObject managerSo = new(manager);
        managerSo.FindProperty("menuMode").boolValue = true;
        string[] skills = { "Skill_Staff", "Skill_Dash", "Skill_SpellBolt", "Skill_Shield", "Skill_Bomb", "Skill_Bow", "Skill_Lantern" };
        SerializedProperty catalog = managerSo.FindProperty("catalog");
        catalog.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
        {
            catalog.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/Scripts/Skills/Data/{skills[i]}.asset");
        }

        managerSo.ApplyModifiedPropertiesWithoutUndo();

        RectTransform title = NewTitle("Title", root, "LEVEL SEÇ", 96, T.accent, new Color(0.1f, 0.05f, 0.2f), new Color(0f, 0f, 0f, 0.55f), 6, new Color(1f, 0.93f, 0.62f));
        Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -T.safeMargin - 10f), new Vector2(1100f, 150f));

        RectTransform grid = NewRect("Levels", root);
        Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1700f, 440f));
        HorizontalLayoutGroup layout = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 36f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        Vector2 small = new(340f, 88f);
        Button back = NewButton("BackButton", root, "GERİ", small, false, false);
        Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(T.safeMargin, T.safeMargin), small);
        Button skillTree = NewButton("SkillTreeButton", root, "SKILL AĞACI", new Vector2(420f, 88f), true, false);
        Place((RectTransform)skillTree.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-T.safeMargin, T.safeMargin), new Vector2(420f, 88f));
        TMP_Text message = NewText("Message", root, string.Empty, T.fontBody, T.staminaLow);
        Place(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 56f));

        GameObject points = Nested("SkillPointsHud", root);
        Place((RectTransform)points.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-T.safeMargin, -T.safeMargin), new Vector2(240f, 48f));
        SetBool(points.GetComponent<SkillPointsHud>(), "debugKey", true);
        GameObject panel = Nested("SkillTreePanel", root);
        panel.transform.SetAsLastSibling();
        SetRefs(panel.GetComponent<SkillTreePanel>(), ("manager", manager));

        LevelSelectController controller = root.gameObject.AddComponent<LevelSelectController>();
        SetRefs(controller, ("buttonPrefab", levelCard), ("container", grid), ("backButton", back), ("messageText", message),
            ("skillTreeButton", skillTree), ("skillTreePanel", panel.GetComponent<SkillTreePanel>()));
        EndScene(scene, $"{ScenesDir}/LevelSelect.unity");
    }

    // In the level scenes: the HUD prefab owns its points display now (drop the old scene-level copy) and the pause menu is added.
    static void PatchGameScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            opened = true;
        }

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == "HUD" && PrefabUtility.IsAnyPrefabInstanceRoot(rootObject))
            {
                foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(rootObject))
                {
                    if (added.instanceGameObject != null && added.instanceGameObject.name == "SkillPointsHud")
                    {
                        Object.DestroyImmediate(added.instanceGameObject);
                    }
                }
            }
        }

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == "PauseMenu")
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/PauseMenu.prefab");
        PrefabUtility.InstantiatePrefab(pausePrefab, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void AddToBuildSettings()
    {
        string[] ours = { ScenesDir + "/MainMenu.unity", ScenesDir + "/LevelSelect.unity" };
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in ours)
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (System.Array.IndexOf(ours, existing.path) < 0)
            {
                scenes.Add(existing);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
