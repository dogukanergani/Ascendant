using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Renders the canvases of the open scene (or a prefab) to a PNG in edit mode, so UI can be checked without entering Play.
public static class UIPreview
{
    // Renders every root canvas whose name is in canvasNames (all of them when empty) into outPath.
    public static string CaptureScene(string outPath, int width = 1920, int height = 1080, params string[] canvasNames)
    {
        var canvases = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas)
            {
                continue;
            }

            if (canvasNames == null || canvasNames.Length == 0 || System.Array.IndexOf(canvasNames, canvas.name) >= 0)
            {
                canvases.Add(canvas);
            }
        }

        return Render(canvases, outPath, width, height);
    }

    public static string CapturePrefab(string prefabPath, string outPath, int width = 1920, int height = 1080)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        try
        {
            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas == null)
            {
                // A prefab without its own canvas gets a temporary one.
                GameObject host = new("PreviewCanvas", typeof(Canvas), typeof(CanvasScaler));
                canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                instance.transform.SetParent(host.transform, false);
                instance = host;
            }

            return Render(new List<Canvas> { canvas }, outPath, width, height);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // The skill tree panel with every skill as a card (cards are normally made at runtime). Tiers are faked per card.
    public static string CaptureSkillTree(string outPath)
    {
        GameObject host = new("PreviewCanvas", typeof(Canvas), typeof(CanvasScaler));
        host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        try
        {
            GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SkillTreePanel.prefab"), host.transform);
            SerializedObject so = new(panel.GetComponent<SkillTreePanel>());
            ((GameObject)so.FindProperty("content").objectReferenceValue).SetActive(true);
            RectTransform grid = (RectTransform)so.FindProperty("cardContainer").objectReferenceValue;
            SkillCardView prefab = (SkillCardView)so.FindProperty("cardPrefab").objectReferenceValue;
            string[] skills = { "Staff", "Dash", "SpellBolt", "Shield", "Bomb", "Bow", "Lantern" };
            for (int i = 0; i < skills.Length; i++)
            {
                SkillCardView card = (SkillCardView)PrefabUtility.InstantiatePrefab(prefab, grid);
                SkillDefinition definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/Scripts/Skills/Data/Skill_{skills[i]}.asset");
                card.BindSkill(definition);
                SerializedObject cardSo = new(card);
                ((TMP_Text)cardSo.FindProperty("effectText").objectReferenceValue).text = definition.DescribeTier(i % 3 + 1);
                ((TMP_Text)cardSo.FindProperty("costText").objectReferenceValue).text = (5 + i).ToString();
                Image[] pips = new Image[3];
                SerializedProperty pipProp = cardSo.FindProperty("pips");
                UITheme theme = (UITheme)cardSo.FindProperty("theme").objectReferenceValue;
                for (int p = 0; p < 3; p++)
                {
                    ((Image)pipProp.GetArrayElementAtIndex(p).objectReferenceValue).sprite = p <= i % 3 ? theme.pipFull : theme.pipEmpty;
                }

                if (i == 2)
                {
                    card.SetSelected(true);
                }

                if (i == 4)
                {
                    // A card without enough points: grey button.
                    Button upgrade = (Button)cardSo.FindProperty("upgradeButton").objectReferenceValue;
                    upgrade.interactable = false;
                    upgrade.GetComponent<Image>().sprite = theme.buttonPrimary.disabled;
                    ((TMP_Text)cardSo.FindProperty("upgradeLabel").objectReferenceValue).color = theme.disabledText;
                }
            }

            return Render(new List<Canvas> { host.GetComponent<Canvas>() }, outPath, 1920, 1080);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    // The HUD with sample skill slots, a half-empty heart row, low stamina and the boss bar showing.
    public static string CaptureHud(string outPath)
    {
        GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HUD.prefab"));
        try
        {
            SerializedObject so = new(hud.GetComponent<HudController>());
            UITheme theme = (UITheme)so.FindProperty("theme").objectReferenceValue;
            Transform row = (Transform)so.FindProperty("slotContainer").objectReferenceValue;
            SkillSlotView slotPrefab = (SkillSlotView)so.FindProperty("slotPrefab").objectReferenceValue;
            string[] skills = { "Dash", "SpellBolt", "Shield", "Bomb", "Bow", "Lantern" };
            for (int i = 0; i < skills.Length; i++)
            {
                SkillSlotView slot = (SkillSlotView)PrefabUtility.InstantiatePrefab(slotPrefab, row);
                SkillDefinition definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/Scripts/Skills/Data/Skill_{skills[i]}.asset");
                SerializedObject slotSo = new(slot);
                Sprite icon = definition.Icon != null ? definition.Icon : theme.FallbackSkillIcon(definition.SkillId);
                Image iconImage = (Image)slotSo.FindProperty("icon").objectReferenceValue;
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                ((TMP_Text)slotSo.FindProperty("keyText").objectReferenceValue).text = UITheme.KeyLabel(definition.ActivationKey);
                if (i == 1)
                {
                    ((Image)slotSo.FindProperty("cooldownOverlay").objectReferenceValue).fillAmount = 0.6f;
                    GameObject cd = ((TMP_Text)slotSo.FindProperty("cooldownText").objectReferenceValue).gameObject;
                    cd.SetActive(true);
                }

                if (i == 3)
                {
                    ((Image)slotSo.FindProperty("staminaCover").objectReferenceValue).enabled = true;
                }
            }

            Image[] hearts = new Image[5];
            SerializedProperty heartProp = so.FindProperty("hearts");
            ((Image)heartProp.GetArrayElementAtIndex(3).objectReferenceValue).sprite = (Sprite)so.FindProperty("heartHalf").objectReferenceValue;
            ((Image)heartProp.GetArrayElementAtIndex(4).objectReferenceValue).sprite = (Sprite)so.FindProperty("heartEmpty").objectReferenceValue;
            Image fill = (Image)so.FindProperty("staminaFill").objectReferenceValue;
            fill.fillAmount = 0.18f;
            fill.color = theme.staminaLow;
            BossHealthBar boss = hud.GetComponentInChildren<BossHealthBar>();
            SerializedObject bossSo = new(boss);
            ((CanvasGroup)bossSo.FindProperty("group").objectReferenceValue).alpha = 1f;
            ((Image)bossSo.FindProperty("fill").objectReferenceValue).fillAmount = 0.62f;
            ((Image)bossSo.FindProperty("ghost").objectReferenceValue).fillAmount = 0.8f;
            return Render(new List<Canvas> { hud.GetComponent<Canvas>() }, outPath, 1920, 1080);
        }
        finally
        {
            Object.DestroyImmediate(hud);
        }
    }

    // Captures the UI prefabs and both menu scenes into <project>/Temp/ui_*.png.
    public static string CaptureAll()
    {
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp");
        CaptureHud(Path.Combine(dir, "ui_HudSample.png"));
        CaptureSkillTree(Path.Combine(dir, "ui_SkillTree.png"));
        foreach (string name in new[] { "HUD", "ScoreScreen", "SkillTreePanel", "PauseMenu", "SkillTreeCard", "LevelButton" })
        {
            CapturePrefab($"Assets/Prefabs/UI/{name}.prefab", Path.Combine(dir, $"ui_{name}.png"));
        }

        foreach (string scene in new[] { "MainMenu", "LevelSelect" })
        {
            string path = $"Assets/Scenes/{scene}.unity";
            UnityEngine.SceneManagement.Scene loaded = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                if (scene == "LevelSelect")
                {
                    // The level cards are created at runtime; add a few with sample progress.
                    LevelSelectController controller = Object.FindFirstObjectByType<LevelSelectController>();
                    SerializedObject so = new(controller);
                    LevelButtonView prefab = (LevelButtonView)so.FindProperty("buttonPrefab").objectReferenceValue;
                    Transform container = (Transform)so.FindProperty("container").objectReferenceValue;
                    string[] names = { "Orc Mağarası", "Gölge Geçidi", "Kristal Mahzen", "Çürük Orman", "Yıldız Kulesi" };
                    for (int i = 0; i < 5; i++)
                    {
                        LevelButtonView card = (LevelButtonView)PrefabUtility.InstantiatePrefab(prefab, container);
                        card.Bind(i, names[i], _ => { });
                    }
                }

                CaptureScene(Path.Combine(dir, $"ui_{scene}.png"), 1920, 1080, "Canvas");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(loaded, true);
            }
        }

        return dir;
    }

    public static string FontTest(string outPath)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.FontPath);
        GameObject host = new("FontTestCanvas", typeof(Canvas), typeof(CanvasScaler));
        host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        try
        {
            string[] lines =
            {
                "ASCENDANT ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                "abcdefghijklmnopqrstuvwxyz 0123456789",
                "ÇĞİÖŞÜ çğıöşü  Skill Ağacı Yükselt",
                "Süre 01:23  Puan: 120  Maliyet: 5 !?.,-+/%()",
            };
            int[] sizes = { 36, 36, 48, 72 };
            float y = -40f;
            for (int i = 0; i < lines.Length; i++)
            {
                GameObject go = new($"Line{i}", typeof(RectTransform));
                go.transform.SetParent(host.transform, false);
                TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
                t.font = font;
                t.fontSize = sizes[i];
                t.text = lines[i];
                t.color = new Color(0.96f, 0.92f, 0.82f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(40f, y);
                rt.sizeDelta = new Vector2(1800f, sizes[i] * 1.6f);
                y -= sizes[i] * 1.8f;
            }

            return Render(new List<Canvas> { host.GetComponent<Canvas>() }, outPath, 1920, 1080);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    static string Render(List<Canvas> canvases, string outPath, int width, int height)
    {
        GameObject camObject = new("PreviewCamera");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.06f, 0.12f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 0.1f;
        cam.cullingMask = 1 << LayerMask.NameToLayer("UI");
        cam.farClipPlane = 100f;
        camObject.transform.position = new Vector3(0f, 0f, -10f);
        RenderTexture rt = new(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
        cam.targetTexture = rt;

        var layers = new List<(Transform t, int layer)>();
        int uiLayer = LayerMask.NameToLayer("UI");
        foreach (Canvas canvas in canvases)
        {
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                layers.Add((t, t.gameObject.layer));
                t.gameObject.layer = uiLayer;
            }
        }

        var restore = new List<(Canvas canvas, RenderMode mode, Camera camera)>();
        foreach (Canvas canvas in canvases)
        {
            restore.Add((canvas, canvas.renderMode, canvas.worldCamera));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
        }

        try
        {
            Canvas.ForceUpdateCanvases();
            foreach (Canvas canvas in canvases)
            {
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                }
            }

            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            foreach ((Canvas canvas, RenderMode mode, Camera camera) in restore)
            {
                canvas.renderMode = mode;
                canvas.worldCamera = camera;
            }

            foreach ((Transform t, int layer) in layers)
            {
                if (t != null)
                {
                    t.gameObject.layer = layer;
                }
            }

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camObject);
        }

        return outPath;
    }
}
