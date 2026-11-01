using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// One-shot tooling for the shield, bomb, bow and lantern skills: draws their pixel art, builds prefabs and
// SkillDefinition assets, and adds the skill components and sample secrets to the scenes.
// Run in order: Generate Art (wait for the import), Build Assets, Wire Scenes.
public static class NewSkillsBuilder
{
    const string IconFolder = "Assets/Art/UI/SkillIcons";
    const string CombatArt = "Assets/Art/Combat";
    const string DataFolder = "Assets/Scripts/Skills/Data";
    const string PrefabFolder = "Assets/Prefabs/Combat";
    const int WallLayer = 11;

    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/PlayerTest.unity",
        "Assets/Scenes/LevelSelect.unity",
    };

    // ---- 1. art ----

    [MenuItem("Tools/Skills/1 Generate Art")]
    public static void GenerateArt()
    {
        Directory.CreateDirectory(IconFolder);
        Directory.CreateDirectory(CombatArt);

        WritePng($"{IconFolder}/icon_shield_sheet.png", Scale(DrawShield(), 4));
        WritePng($"{IconFolder}/icon_bomb_sheet.png", Scale(DrawBomb(), 4));
        WritePng($"{IconFolder}/icon_bow_sheet.png", Scale(DrawBow(), 4));
        WritePng($"{IconFolder}/icon_lantern_sheet.png", Scale(DrawLantern(), 4));
        WritePng($"{CombatArt}/bomb_sheet.png", Scale(DrawBomb(), 4));
        WritePng($"{CombatArt}/arrow_sheet.png", Scale(DrawArrow(), 4));
        WritePng($"{CombatArt}/tileset_crackedwall.png", DrawWall(true));
        WritePng($"{CombatArt}/tileset_illusionwall.png", DrawWall(false));
        AssetDatabase.Refresh();
        Debug.Log("NewSkillsBuilder: art written. Wait for the import to finish, then run Tools/Skills/2 Build Assets.");
    }

    sealed class Canvas
    {
        public readonly int Size;
        public readonly Color32[] Pixels;

        public Canvas(int size)
        {
            Size = size;
            Pixels = new Color32[size * size];
        }

        public bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && Pixels[y * Size + x].a > 0;

        public void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size)
            {
                Pixels[y * Size + x] = c;
            }
        }

        public void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r)
                    {
                        Set(x, y, c);
                    }
                }
            }
        }

        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Set(x, y, c);
                }
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Set(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), c);
            }
        }

        // One pixel of colour around everything drawn so far.
        public void Outline(Color32 c)
        {
            List<Vector2Int> edge = new();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!Opaque(x, y) && (Opaque(x + 1, y) || Opaque(x - 1, y) || Opaque(x, y + 1) || Opaque(x, y - 1)))
                    {
                        edge.Add(new Vector2Int(x, y));
                    }
                }
            }

            foreach (Vector2Int p in edge)
            {
                Set(p.x, p.y, c);
            }
        }
    }

    static readonly Color32 Dark = new(24, 20, 32, 255);
    static readonly Color32 Gold = new(240, 190, 60, 255);
    static readonly Color32 Blue = new(60, 110, 220, 255);
    static readonly Color32 LightBlue = new(130, 180, 255, 255);
    static readonly Color32 Brown = new(130, 80, 40, 255);
    static readonly Color32 Gray = new(70, 72, 84, 255);
    static readonly Color32 LightGray = new(190, 195, 205, 255);
    static readonly Color32 Yellow = new(255, 230, 90, 255);
    static readonly Color32 Orange = new(255, 140, 40, 255);
    static readonly Color32 Red = new(210, 60, 50, 255);

    static Canvas DrawShield()
    {
        Canvas c = new Canvas(16);
        for (int y = 1; y <= 14; y++)
        {
            int half = y > 6 ? 6 : Mathf.RoundToInt(6f * (y - 0.5f) / 6f);
            c.Rect(8 - half, y, 7 + half, y, Blue);
        }

        // Gold rim, light edge highlight and a gold cross emblem.
        List<Vector2Int> rim = new();
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (c.Opaque(x, y) && (!c.Opaque(x + 1, y) || !c.Opaque(x - 1, y) || !c.Opaque(x, y + 1) || !c.Opaque(x, y - 1)))
                {
                    rim.Add(new Vector2Int(x, y));
                }
            }
        }

        foreach (Vector2Int p in rim)
        {
            c.Set(p.x, p.y, Gold);
        }

        c.Rect(4, 8, 5, 12, LightBlue);
        c.Rect(7, 4, 8, 12, Gold);
        c.Rect(5, 9, 10, 10, Gold);
        c.Outline(Dark);
        return c;
    }

    static Canvas DrawBomb()
    {
        Canvas c = new Canvas(16);
        c.Line(10, 9, 12, 12, Brown);
        c.Disc(7f, 5.5f, 5f, Gray);
        c.Disc(6f, 7f, 1.2f, LightGray);
        c.Rect(11, 13, 12, 14, Yellow);
        c.Set(13, 14, Orange);
        c.Set(10, 14, Orange);
        c.Set(12, 15, Orange);
        c.Outline(Dark);
        return c;
    }

    static Canvas DrawBow()
    {
        Canvas c = new Canvas(16);
        // Limb arc bulging left, string on the right, arrow nocked in the middle.
        Vector2Int previous = Vector2Int.zero;
        for (int deg = 105; deg <= 255; deg += 5)
        {
            float rad = deg * Mathf.Deg2Rad;
            Vector2Int p = new(Mathf.RoundToInt(10.5f + 7.5f * Mathf.Cos(rad)), Mathf.RoundToInt(7.5f + 7.5f * Mathf.Sin(rad)));
            if (deg > 105)
            {
                c.Line(previous.x, previous.y, p.x, p.y, Brown);
            }

            previous = p;
        }

        float top = 105 * Mathf.Deg2Rad;
        float bottom = 255 * Mathf.Deg2Rad;
        c.Line(Mathf.RoundToInt(10.5f + 7.5f * Mathf.Cos(top)), Mathf.RoundToInt(7.5f + 7.5f * Mathf.Sin(top)),
            Mathf.RoundToInt(10.5f + 7.5f * Mathf.Cos(bottom)), Mathf.RoundToInt(7.5f + 7.5f * Mathf.Sin(bottom)), LightGray);
        c.Line(4, 7, 14, 7, LightGray);
        c.Set(14, 7, Yellow);
        c.Set(13, 8, Yellow);
        c.Set(13, 6, Yellow);
        c.Set(4, 8, Red);
        c.Set(4, 6, Red);
        c.Outline(Dark);
        return c;
    }

    static Canvas DrawLantern()
    {
        Canvas c = new Canvas(16);
        c.Rect(5, 4, 10, 11, new Color32(255, 220, 110, 255));
        c.Disc(7.5f, 7.5f, 2.2f, Orange);
        c.Disc(7.5f, 7.5f, 1.2f, Color.white);
        c.Rect(4, 12, 11, 13, Brown);
        c.Rect(4, 2, 11, 3, Brown);
        c.Rect(5, 4, 5, 11, Brown);
        c.Rect(10, 4, 10, 11, Brown);
        c.Rect(6, 14, 9, 14, Gold);
        c.Set(6, 15, Gold);
        c.Set(9, 15, Gold);
        c.Outline(Dark);
        return c;
    }

    static Canvas DrawArrow()
    {
        Canvas c = new Canvas(16);
        c.Line(2, 8, 12, 8, Brown);
        c.Line(2, 9, 12, 9, Brown);
        c.Set(13, 8, LightGray);
        c.Set(13, 9, LightGray);
        c.Set(14, 8, LightGray);
        c.Set(14, 9, LightGray);
        c.Set(12, 10, LightGray);
        c.Set(12, 7, LightGray);
        c.Set(13, 10, LightGray);
        c.Set(13, 7, LightGray);
        c.Set(1, 10, Red);
        c.Set(2, 10, Red);
        c.Set(1, 7, Red);
        c.Set(2, 7, Red);
        c.Set(3, 10, Red);
        c.Set(3, 7, Red);
        c.Outline(Dark);
        return c;
    }

    // One 32x32 tile (1 world unit): grey bricks, optionally with dark cracks.
    static Texture2D DrawWall(bool cracked)
    {
        Canvas c = new Canvas(32);
        System.Random rng = new(cracked ? 7 : 11);
        Color32 mortar = new(60, 56, 66, 255);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int row = y / 8;
                int offset = row % 2 == 0 ? 0 : 8;
                bool seam = y % 8 == 0 || (x + offset) % 16 == 0;
                int tone = 120 + rng.Next(-8, 9);
                c.Set(x, y, seam ? mortar : new Color32((byte)tone, (byte)(tone - 6), (byte)(tone - 16), 255));
            }
        }

        if (cracked)
        {
            Color32 crack = new(24, 20, 28, 255);
            int x0 = 16;
            for (int y = 31; y >= 4; y--)
            {
                x0 = Mathf.Clamp(x0 + rng.Next(-1, 2), 8, 24);
                c.Set(x0, y, crack);
                c.Set(x0 + 1, y, crack);
                if (y % 9 == 0)
                {
                    c.Line(x0, y, x0 + (rng.Next(0, 2) == 0 ? -6 : 6), y - 5, crack);
                }
            }
        }

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.SetPixels32(c.Pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D Scale(Canvas canvas, int factor)
    {
        int size = canvas.Size * factor;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, canvas.Pixels[(y / factor) * canvas.Size + x / factor]);
            }
        }

        tex.Apply();
        return tex;
    }

    static void WritePng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // ---- 2. prefabs and skill assets ----

    [MenuItem("Tools/Skills/2 Build Assets")]
    public static void BuildAssets()
    {
        Material glow = LoadOrCreateMaterial();

        Sprite bombSprite = LoadSprite("bomb_sheet", $"{CombatArt}/bomb_sheet.png");
        Sprite arrowSprite = LoadSprite("arrow_sheet", $"{CombatArt}/arrow_sheet.png");
        Sprite crackedSprite = LoadSprite("tileset_crackedwall", $"{CombatArt}/tileset_crackedwall.png");
        Sprite illusionSprite = LoadSprite("tileset_illusionwall", $"{CombatArt}/tileset_illusionwall.png");
        if (bombSprite == null || arrowSprite == null || crackedSprite == null || illusionSprite == null)
        {
            Debug.LogError("NewSkillsBuilder: sprites are not imported/sliced yet; run again in a moment.");
            return;
        }

        BuildBombPrefab(bombSprite);
        BuildArrowPrefab(arrowSprite);
        BuildWallPrefab("BreakableWall", crackedSprite, true);
        BuildWallPrefab("IllusionWall", illusionSprite, false);

        BuildDefinition("Skill_Shield", "shield", "Kalkan", "icon_shield_sheet", 0.4f, 0f, Key.L, null, null, new[]
        {
            "Ön yarım daireden gelen vuruşu engeller ve geri iter. Basılı tut (L / sağ tık): -12 stamina/sn, yavaşlatır",
            "Parry: ilk 0.2 sn'de engellenen vuruşun sahibi sersemler, +10 stamina",
            "Parry sonrası bir sonraki Asa vuruşu 2 kat hasar",
        });
        BuildDefinition("Skill_Bomb", "bomb", "Bomba", "icon_bomb_sheet", 0.8f, 25f, Key.G,
            new[] { new Vector2Int(14, 18), new Vector2Int(26, 32), new Vector2Int(44, 52) }, new[] { 25f, 25f, 25f }, new[]
            {
                "Yarıçap 2.0. Çatlak duvarları yıkar (G)",
                "Yarıçap 2.6",
                "Yarıçap 3.2. Zincirleme patlama: alandaki bombalar anında patlar",
            });
        BuildDefinition("Skill_Bow", "bow", "Yay", "icon_bow_sheet", 0.35f, 10f, Key.R,
            new[] { new Vector2Int(12, 16), new Vector2Int(26, 34), new Vector2Int(46, 56) }, new[] { 10f, 10f, 10f }, new[]
            {
                "Hızlı tek hedef oku (R)",
                "Delici ok: en fazla 3 düşmanı deler",
                "Basılı tut ve bırak: ok yağmuru, 8 ok, toplam 30 stamina",
            });
        BuildDefinition("Skill_Lantern", "lantern", "Fener", "icon_lantern_sheet", 0.3f, 0f, Key.E, null, null, new[]
        {
            "Yarıçap 4. Gizli nesneleri gösterir. Aç/kapa (E): -4 stamina/sn",
            "Yarıçap 6. Gizli duvarları açar, hafif hızlandırır",
            "Yarıçap 8. Açılırken flaş: yakındaki düşmanlar 1.5 sn sersemler (8 sn bekleme)",
        });

        // The skill tree now has real cards for these; drop the "?" placeholders.
        string panelPath = "Assets/Prefabs/UI/SkillTreePanel.prefab";
        GameObject panel = PrefabUtility.LoadPrefabContents(panelPath);
        SkillTreePanel treePanel = panel.GetComponentInChildren<SkillTreePanel>(true);
        SerializedObject so = new SerializedObject(treePanel);
        so.FindProperty("lockedSkillNames").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(panel, panelPath);
        PrefabUtility.UnloadPrefabContents(panel);

        AssetDatabase.SaveAssets();
        Debug.Log("NewSkillsBuilder: assets built. Material " + glow.name);
    }

    static Sprite LoadSprite(string baseName, string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is UnityEngine.Sprite sprite && sprite.name == baseName + "_0")
            {
                return sprite;
            }
        }

        return null;
    }

    static Material LoadOrCreateMaterial()
    {
        const string path = CombatArt + "/LanternGlow.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Ascendant/AdditiveSprite"));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    static GameObject SpriteChild(GameObject parent, Sprite sprite, Vector3 localPosition, float scale, int order)
    {
        GameObject child = new GameObject("Sprite");
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return child;
    }

    static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildBombPrefab(Sprite sprite)
    {
        GameObject root = new GameObject("Bomb");
        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;
        Bomb bomb = root.AddComponent<Bomb>();
        // Sheet sprites pivot at the bottom centre of a 64px (2 unit) cell: lift the child so the cell is centred.
        GameObject visual = SpriteChild(root, sprite, new Vector3(0f, -0.6f, 0f), 0.6f, 5);
        SetRef(bomb, "spriteRenderer", visual.GetComponent<SpriteRenderer>());
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Bomb.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildArrowPrefab(Sprite sprite)
    {
        GameObject root = new GameObject("Arrow");
        Arrow arrow = root.AddComponent<Arrow>();
        GameObject visual = SpriteChild(root, sprite, new Vector3(0f, -1f, 0f), 1f, 12);
        SetRef(arrow, "spriteRenderer", visual.GetComponent<SpriteRenderer>());
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Arrow.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildWallPrefab(string name, Sprite sprite, bool breakable)
    {
        GameObject root = new GameObject(name);
        root.layer = WallLayer;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        if (breakable)
        {
            root.AddComponent<BreakableWall>();
        }

        SpriteChild(root, sprite, Vector3.zero, 1f, 0).layer = WallLayer;
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildDefinition(string assetName, string id, string displayName, string iconName, float cooldown, float staminaCost,
        Key key, Vector2Int[] damage, float[] tierStamina, string[] notes)
    {
        string path = $"{DataFolder}/{assetName}.asset";
        SkillDefinition definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("skillId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("icon").objectReferenceValue = LoadSprite(iconName, $"{IconFolder}/{iconName}.png");
        so.FindProperty("cooldown").floatValue = cooldown;
        so.FindProperty("staminaCost").floatValue = staminaCost;
        so.FindProperty("unlockedByDefault").boolValue = true; // testing: becomes a boss reward later
        so.FindProperty("activationKey").intValue = (int)key;

        SerializedProperty damageProp = so.FindProperty("tierDamage");
        damageProp.arraySize = damage != null ? damage.Length : 0;
        for (int i = 0; damage != null && i < damage.Length; i++)
        {
            damageProp.GetArrayElementAtIndex(i).vector2IntValue = damage[i];
        }

        SerializedProperty costs = so.FindProperty("upgradeCosts");
        costs.arraySize = 2;
        costs.GetArrayElementAtIndex(0).intValue = 5;
        costs.GetArrayElementAtIndex(1).intValue = 12;

        SerializedProperty stamina = so.FindProperty("tierStaminaCost");
        stamina.arraySize = tierStamina != null ? tierStamina.Length : 0;
        for (int i = 0; tierStamina != null && i < tierStamina.Length; i++)
        {
            stamina.GetArrayElementAtIndex(i).floatValue = tierStamina[i];
        }

        SerializedProperty notesProp = so.FindProperty("tierNotes");
        notesProp.arraySize = notes.Length;
        for (int i = 0; i < notes.Length; i++)
        {
            notesProp.GetArrayElementAtIndex(i).stringValue = notes[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    // ---- 3. scenes ----

    [MenuItem("Tools/Skills/3 Wire Scenes")]
    public static void WireScenes()
    {
        string returnTo = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();

        foreach (string scenePath in ScenePaths)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            // Opening a scene unloads unused assets, so the definitions are loaded again for every scene.
            SkillDefinition[] added =
            {
                Load<SkillDefinition>("Skill_Shield"), Load<SkillDefinition>("Skill_Bomb"),
                Load<SkillDefinition>("Skill_Bow"), Load<SkillDefinition>("Skill_Lantern"),
            };
            SkillManager manager = Object.FindFirstObjectByType<SkillManager>();
            if (manager != null)
            {
                AddToCatalog(manager, added);
            }

            PlayerController2D player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                AddSkills(player.gameObject, added);
            }

            if (scenePath.EndsWith("Level_01.unity"))
            {
                AddSecrets();
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
        }

        if (!string.IsNullOrEmpty(returnTo))
        {
            EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }

        Debug.Log("NewSkillsBuilder: scenes wired.");
    }

    static T Load<T>(string name) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets(name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name && AssetDatabase.LoadAssetAtPath<T>(path) is T asset)
            {
                return asset;
            }
        }

        return null;
    }

    static void AddToCatalog(SkillManager manager, SkillDefinition[] added)
    {
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty catalog = so.FindProperty("catalog");
        for (int i = catalog.arraySize - 1; i >= 0; i--)
        {
            if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                catalog.DeleteArrayElementAtIndex(i);
            }
        }

        foreach (SkillDefinition definition in added)
        {
            bool present = false;
            for (int i = 0; i < catalog.arraySize; i++)
            {
                present |= catalog.GetArrayElementAtIndex(i).objectReferenceValue == definition;
            }

            if (!present)
            {
                catalog.arraySize++;
                catalog.GetArrayElementAtIndex(catalog.arraySize - 1).objectReferenceValue = definition;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddSkills(GameObject player, SkillDefinition[] added)
    {
        AddSkill<ShieldSkill>(player, added[0], null, null);
        AddSkill<BombSkill>(player, added[1], "bombPrefab", Load<GameObject>("Bomb")?.GetComponent<Bomb>());
        AddSkill<BowSkill>(player, added[2], "arrowPrefab", Load<GameObject>("Arrow")?.GetComponent<Arrow>());
        AddSkill<LanternSkill>(player, added[3], "lightMaterial", AssetDatabase.LoadAssetAtPath<Material>(CombatArt + "/LanternGlow.mat"));
    }

    static void AddSkill<T>(GameObject player, SkillDefinition definition, string extraField, Object extraValue) where T : SkillAbility
    {
        T skill = player.GetComponent<T>();
        if (skill == null)
        {
            skill = player.AddComponent<T>();
        }

        SerializedObject so = new SerializedObject(skill);
        so.FindProperty("definition").objectReferenceValue = definition;
        if (extraField != null)
        {
            so.FindProperty(extraField).objectReferenceValue = extraValue;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sample secrets in the rest room: a bomb pocket (bottom left), a lantern pocket behind illusory walls (top right)
    // and a heart that only appears in the lantern light (top left).
    static void AddSecrets()
    {
        GameObject old = GameObject.Find("Secrets");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        GameObject root = new GameObject("Secrets");
        GameObject breakable = Load<GameObject>("BreakableWall");
        GameObject illusion = Load<GameObject>("IllusionWall");
        GameObject heart = Load<GameObject>("HeartPickup");
        GameObject orb = Load<GameObject>("StaminaOrb");

        GameObject bombPocket = new GameObject("BombPocket");
        bombPocket.transform.SetParent(root.transform, false);
        foreach (Vector2 cell in new[] { new Vector2(-3.5f, -3.5f), new Vector2(-3.5f, -2.5f), new Vector2(-5.5f, -1.5f), new Vector2(-4.5f, -1.5f), new Vector2(-3.5f, -1.5f) })
        {
            Place(breakable, bombPocket.transform, cell);
        }

        PlaceReward(heart, bombPocket.transform, new Vector2(-5.5f, -3.5f));
        PlaceReward(orb, bombPocket.transform, new Vector2(-4.5f, -3.0f));

        GameObject lanternPocket = new GameObject("LanternPocket");
        lanternPocket.transform.SetParent(root.transform, false);
        lanternPocket.transform.position = new Vector3(3.5f, 2f, 0f);
        SerializedObject pocketSo = new SerializedObject(lanternPocket.AddComponent<HiddenReveal>());
        pocketSo.FindProperty("requiredTier").intValue = 2;
        pocketSo.FindProperty("dissolve").boolValue = true;
        pocketSo.ApplyModifiedPropertiesWithoutUndo();
        foreach (Vector2 cell in new[] { new Vector2(3.5f, 2.5f), new Vector2(3.5f, 3.5f), new Vector2(4.5f, 1.5f), new Vector2(5.5f, 1.5f), new Vector2(3.5f, 1.5f) })
        {
            Place(illusion, lanternPocket.transform, cell);
        }

        GameObject lanternReward = new GameObject("LanternPocketReward");
        lanternReward.transform.SetParent(root.transform, false);
        PlaceReward(orb, lanternReward.transform, new Vector2(4.5f, 3.0f));
        PlaceReward(heart, lanternReward.transform, new Vector2(5.5f, 2.5f));

        GameObject hidden = new GameObject("HiddenHeart");
        hidden.transform.SetParent(root.transform, false);
        hidden.transform.position = new Vector3(-5.5f, 3.0f, 0f);
        hidden.AddComponent<HiddenReveal>();
        PlaceReward(heart, hidden.transform, new Vector2(-5.5f, 3.0f));
    }

    static void Place(GameObject prefab, Transform parent, Vector2 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
    }

    // A pickup that does not expire and only reacts when the player stands next to it (it must not fly through the wall).
    static void PlaceReward(GameObject prefab, Transform parent, Vector2 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
        Pickup pickup = instance.GetComponent<Pickup>();
        if (pickup != null)
        {
            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("lifetime").floatValue = 0f;
            so.FindProperty("magnetRadius").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
