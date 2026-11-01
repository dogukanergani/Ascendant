using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Regenerates the menu scenes and UI prefabs. Run via Tools/UI/Build All.
public static class UIBuilder
{
    const string PrefabDir = "Assets/Prefabs/UI";
    const string SpriteDir = PrefabDir + "/Sprites";
    const string FontPath = PrefabDir + "/PixelFont.asset";

    static readonly Color Background = new(0.08f, 0.09f, 0.14f, 1f);
    static readonly Color Panel = new(0.14f, 0.16f, 0.24f, 1f);
    static readonly Color ButtonColor = new(0.25f, 0.45f, 0.8f, 1f);
    static readonly Color TextColor = new(0.95f, 0.95f, 0.9f, 1f);

    static TMP_FontAsset font;
    static Sprite star, lockSprite, heartFull, heartEmpty, square;

    [MenuItem("Tools/UI/Build All")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(SpriteDir);
        font = CreateFont();
        CreateSprites();

        var scratch = BeginScene();
        LevelButtonView levelButton = BuildLevelButtonPrefab();
        SkillSlotView slot = BuildSkillSlotPrefab();
        BuildHudPrefab(slot);
        BuildScorePrefab();
        EditorSceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(scratch, true);
        BuildMainMenu();
        BuildLevelSelect(levelButton);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("UIBuilder: done.");
    }

    // ---- assets ----

    static TMP_FontAsset CreateFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null)
        {
            return existing;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "PixelFont";
        AssetDatabase.CreateAsset(asset, FontPath);
        asset.atlasTexture.name = "PixelFont Atlas";
        asset.material.name = "PixelFont Material";
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.TryAddCharacters("ÇĞİÖŞÜçğıöşü0123456789:/ ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void CreateSprites()
    {
        square = MakeSprite("square", new[] { "#" }, 1);
        star = MakeSprite("star", new[]
        {
            "....#....",
            "....#....",
            "...###...",
            "#########",
            ".#######.",
            "..#####..",
            "..##.##..",
            ".##...##.",
            ".#.....#.",
        }, 9);
        lockSprite = MakeSprite("lock", new[]
        {
            "..#####..",
            ".##...##.",
            ".#.....#.",
            ".#.....#.",
            "#########",
            "#########",
            "####.####",
            "####.####",
            "#########",
        }, 9);
        string[] heart =
        {
            ".##...##.",
            "#########",
            "#########",
            "#########",
            ".#######.",
            "..#####..",
            "...###...",
            "....#....",
        };
        heartFull = MakeSprite("heart_full", heart, 8, new Color(0.9f, 0.2f, 0.25f));
        heartEmpty = MakeSprite("heart_empty", heart, 8, new Color(0.3f, 0.12f, 0.15f));
    }

    static Sprite MakeSprite(string name, string[] rows, int size, Color? tint = null)
    {
        string path = $"{SpriteDir}/{name}.png";
        int width = rows[0].Length;
        int height = rows.Length;
        Texture2D tex = new(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool on = rows[height - 1 - y][x] == '#';
                tex.SetPixel(x, y, on ? (tint ?? Color.white) : Color.clear);
            }
        }

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 1;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- ui helpers ----

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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

    static Button NewButton(string name, Transform parent, string label, Vector2 size)
    {
        Image bg = NewImage(name, parent, square, ButtonColor);
        bg.raycastTarget = true;
        ((RectTransform)bg.transform).sizeDelta = size;
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;
        TMP_Text text = NewText("Label", bg.transform, label, 48);
        Stretch(text.rectTransform);
        return button;
    }

    static Canvas NewCanvas(string name, Transform parent = null)
    {
        GameObject go = new(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static StarRow NewStarRow(string name, Transform parent, float starSize, float spacing)
    {
        RectTransform rt = NewRect(name, parent);
        HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        Image[] images = new Image[ProgressService.MaxStars];
        for (int i = 0; i < images.Length; i++)
        {
            images[i] = NewImage($"Star{i + 1}", rt, star, Color.white);
            ((RectTransform)images[i].transform).sizeDelta = new Vector2(starSize, starSize);
        }

        StarRow row = rt.gameObject.AddComponent<StarRow>();
        SetRefs(row, ("stars", images));
        return row;
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

    static void AddEventSystem()
    {
        GameObject es = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static void AddCamera()
    {
        GameObject go = new("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        Camera cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.orthographic = true;
    }

    // ---- prefabs ----

    static LevelButtonView BuildLevelButtonPrefab()
    {
        Button button = NewButton("LevelButton", null, string.Empty, new Vector2(220, 220));
        Image bg = button.GetComponent<Image>();
        Object.DestroyImmediate(button.transform.Find("Label").gameObject);

        TMP_Text number = NewText("Number", button.transform, "1", 96);
        Place(number.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 120));

        Image lockIcon = NewImage("Lock", button.transform, lockSprite, TextColor);
        Place(lockIcon.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));

        StarRow row = NewStarRow("Stars", button.transform, 40, 8);
        Place((RectTransform)row.transform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 44));

        LevelButtonView view = button.gameObject.AddComponent<LevelButtonView>();
        SetRefs(view, ("button", button), ("numberText", number), ("lockIcon", lockIcon.gameObject), ("starRow", row), ("background", bg));
        return SavePrefab<LevelButtonView>(button.gameObject, "LevelButton");
    }

    static SkillSlotView BuildSkillSlotPrefab()
    {
        RectTransform root = NewRect("SkillSlot", null);
        root.sizeDelta = new Vector2(88, 88);
        Image frame = NewImage("Frame", root, square, Panel);
        Stretch(frame.rectTransform);
        Image icon = NewImage("Icon", root, null, Color.white);
        Stretch(icon.rectTransform);
        icon.rectTransform.offsetMin = new Vector2(8, 8);
        icon.rectTransform.offsetMax = new Vector2(-8, -8);
        Image overlay = NewImage("Cooldown", root, square, new Color(0f, 0f, 0f, 0.65f));
        Stretch(overlay.rectTransform);
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;

        SkillSlotView view = root.gameObject.AddComponent<SkillSlotView>();
        SetRefs(view, ("icon", icon), ("cooldownOverlay", overlay));
        return SavePrefab<SkillSlotView>(root.gameObject, "SkillSlot");
    }

    static void BuildHudPrefab(SkillSlotView slotPrefab)
    {
        Canvas canvas = NewCanvas("HUD");
        RectTransform root = (RectTransform)canvas.transform;

        RectTransform heartRow = NewRect("Hearts", root);
        Place(heartRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32), new Vector2(400, 64));
        HorizontalLayoutGroup hearts = heartRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hearts.spacing = 12;
        hearts.childControlWidth = hearts.childControlHeight = false;
        hearts.childForceExpandWidth = hearts.childForceExpandHeight = false;
        Image[] heartImages = new Image[3];
        for (int i = 0; i < heartImages.Length; i++)
        {
            heartImages[i] = NewImage($"Heart{i + 1}", heartRow, heartFull, Color.white);
            ((RectTransform)heartImages[i].transform).sizeDelta = new Vector2(64, 64);
        }

        RectTransform slotRow = NewRect("Skills", root);
        Place(slotRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -112), new Vector2(600, 88));
        HorizontalLayoutGroup slots = slotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        slots.spacing = 12;
        slots.childControlWidth = slots.childControlHeight = false;
        slots.childForceExpandWidth = slots.childForceExpandHeight = false;

        TMP_Text timerText = NewText("Timer", root, "00:00", 56, TextAlignmentOptions.TopRight);
        Place(timerText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -24), new Vector2(300, 80));

        LevelTimer timer = root.gameObject.AddComponent<LevelTimer>();
        HudController hud = root.gameObject.AddComponent<HudController>();
        SetRefs(hud, ("hearts", heartImages), ("heartFull", heartFull), ("heartEmpty", heartEmpty), ("slotPrefab", slotPrefab),
            ("slotContainer", slotRow), ("timerText", timerText), ("timer", timer));
        SavePrefab<HudController>(root.gameObject, "HUD");
    }

    static void BuildScorePrefab()
    {
        Canvas canvas = NewCanvas("ScoreScreen");
        canvas.sortingOrder = 10;
        RectTransform root = (RectTransform)canvas.transform;

        RectTransform panel = NewRect("Panel", root);
        Stretch(panel);
        Image dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);

        Image box = NewImage("Box", panel, square, Panel);
        Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 760));

        TMP_Text title = NewText("Title", box.transform, "Level Tamam!", 80);
        Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 110));
        StarRow stars = NewStarRow("Stars", box.transform, 120, 24);
        Place((RectTransform)stars.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(480, 130));
        TMP_Text time = NewText("Time", box.transform, "Süre 00:00", 56);
        Place(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(800, 80));
        TMP_Text collected = NewText("Collected", box.transform, "Toplanan 0/0", 56);
        Place(collected.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(800, 80));

        Button retry = NewButton("RetryButton", box.transform, "Tekrar", new Vector2(240, 90));
        Place((RectTransform)retry.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-270, 50), new Vector2(240, 90));
        Button menu = NewButton("MenuButton", box.transform, "Levellar", new Vector2(240, 90));
        Place((RectTransform)menu.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(240, 90));
        Button next = NewButton("NextButton", box.transform, "Sonraki", new Vector2(240, 90));
        Place((RectTransform)next.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(270, 50), new Vector2(240, 90));

        ScoreScreen screen = root.gameObject.AddComponent<ScoreScreen>();
        SetRefs(screen, ("panel", panel.gameObject), ("timeText", time), ("collectedText", collected), ("starRow", stars),
            ("nextButton", next), ("retryButton", retry), ("menuButton", menu));
        SavePrefab<ScoreScreen>(root.gameObject, "ScoreScreen");
    }

    // ---- scenes ----

    // Scenes are built additively and closed again so whatever the user has open stays open.
    static UnityEngine.SceneManagement.Scene previousActive;

    static UnityEngine.SceneManagement.Scene BeginScene()
    {
        previousActive = EditorSceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);
        return scene;
    }

    static void EndScene(UnityEngine.SceneManagement.Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(scene, true);
    }

    static void BuildMainMenu()
    {
        var scene = BeginScene();
        AddCamera();
        AddEventSystem();
        Canvas canvas = NewCanvas("Canvas");
        RectTransform root = (RectTransform)canvas.transform;

        TMP_Text title = NewText("Title", root, "PLATFORMER", 140);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1400, 200));
        Button start = NewButton("StartButton", root, "Başla", new Vector2(520, 110));
        Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(520, 110));
        Button select = NewButton("LevelSelectButton", root, "Level Seç", new Vector2(520, 110));
        Place((RectTransform)select.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(520, 110));
        Button quit = NewButton("QuitButton", root, "Çıkış", new Vector2(520, 110));
        Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(520, 110));
        TMP_Text message = NewText("Message", root, string.Empty, 40);
        Place(message.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1000, 70));

        MainMenuController controller = root.gameObject.AddComponent<MainMenuController>();
        SetRefs(controller, ("startButton", start), ("levelSelectButton", select), ("quitButton", quit), ("messageText", message));
        EndScene(scene, "Assets/Scenes/MainMenu.unity");
    }

    static void BuildLevelSelect(LevelButtonView levelButton)
    {
        var scene = BeginScene();
        AddCamera();
        AddEventSystem();
        Canvas canvas = NewCanvas("Canvas");
        RectTransform root = (RectTransform)canvas.transform;

        TMP_Text title = NewText("Title", root, "Level Seç", 100);
        Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 140));

        RectTransform grid = NewRect("Levels", root);
        Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1500, 260));
        HorizontalLayoutGroup layout = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        Button back = NewButton("BackButton", root, "Geri", new Vector2(320, 100));
        Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 60), new Vector2(320, 100));
        TMP_Text message = NewText("Message", root, string.Empty, 40);
        Place(message.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1000, 70));

        LevelSelectController controller = root.gameObject.AddComponent<LevelSelectController>();
        SetRefs(controller, ("buttonPrefab", levelButton), ("container", grid), ("backButton", back), ("messageText", message));
        EndScene(scene, "Assets/Scenes/LevelSelect.unity");
    }

    static void AddToBuildSettings()
    {
        string[] ours = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/LevelSelect.unity" };
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
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
