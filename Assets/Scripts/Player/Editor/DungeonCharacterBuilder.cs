using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Characters from 0x72's DungeonTileset II (Assets/Art/DungeonTilesetII/frames, CC0): builds clips and controllers
// from the per-frame PNGs and points the player, the enemy prefabs and the pickups at them. The pack draws every
// character facing right with idle / run (/ hit) frames only, so: sprites are mirrored by facing, the player's staff
// swing is code (StaffVisual), deaths are DeathFade, dashes reuse the run frames.
// Tools/Art/DungeonTilesetII/Build Character Animations, then Apply To Player (open scene) and Apply To Enemies + Pickups.
public static class DungeonCharacterBuilder
{
    const string Frames = DungeonTilesetIIImporter.Folder + "frames/";
    const string PlayerOut = "Assets/Art/Player/Animations/Wizard";
    const string EnemyOut = "Assets/Art/Enemies/Animations/DTS2";
    const string EnemyPrefabs = "Assets/Prefabs/Enemies";
    const string WizardController = PlayerOut + "/Wizard.controller";
    const string WizardFOverride = PlayerOut + "/Wizard_F.overrideController";

    // Enemy prefab -> pack character, visual scale of the prefab root (collider and sprite together).
    static readonly (string prefab, string character, float scale, string copyFrom)[] Enemies =
    {
        ("Orc1", "goblin", 1f, null),
        ("Orc2", "orc_warrior", 1f, null),
        ("Orc3", "masked_orc", 1f, null),
        ("Orc3Boss", "big_demon", 1.5f, null),
        ("Slime", "slug", 1f, null),
        ("Bat", "imp", 1f, null),
        // Extra looks for later rooms: visual only, they reuse the stats of the prefab they are copied from.
        ("Skelet", "skelet", 1f, "Orc1"),
        ("Zombie", "zombie", 1f, "Orc1"),
        ("OrcShaman", "orc_shaman", 1f, "Orc2"),
    };

    // ---- frames and clips ----

    // All frames of an animation, e.g. "goblin_run_anim" -> goblin_run_anim_f0..f3 (numeric order).
    static List<Sprite> FramesOf(string animation)
    {
        var found = new List<(int, Sprite)>();
        var pattern = new Regex("^" + Regex.Escape(animation) + @"_f(\d+)$");
        foreach (string file in Directory.GetFiles(Frames, animation + "_f*.png"))
        {
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(file));
            if (!m.Success) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
            if (sprite != null) found.Add((int.Parse(m.Groups[1].Value), sprite));
        }

        found.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        var list = new List<Sprite>();
        foreach (var (_, s) in found) list.Add(s);
        return list;
    }

    // Idle / run frames of a character; characters with a single animation ("slug_anim") use it for both.
    static (List<Sprite> idle, List<Sprite> run, List<Sprite> hit) CharacterFrames(string character)
    {
        List<Sprite> idle = FramesOf(character + "_idle_anim");
        List<Sprite> run = FramesOf(character + "_run_anim");
        List<Sprite> hit = FramesOf(character + "_hit_anim");
        if (idle.Count == 0) idle = FramesOf(character + "_anim");
        if (run.Count == 0) run = idle;
        if (hit.Count == 0 && idle.Count > 0) hit = new List<Sprite> { idle[0] };
        if (idle.Count == 0) throw new System.Exception($"DungeonCharacterBuilder: no frames for {character} in {Frames}");
        return (idle, run, hit);
    }

    static AnimationClip Clip(string path, List<Sprite> frames, float fps, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        // One extra key repeats the last frame so it is shown for a full frame before the clip ends or loops.
        var keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int i = 0; i <= frames.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[Mathf.Min(i, frames.Count - 1)] };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/Art/DungeonTilesetII/Build Character Animations")]
    public static void BuildAll()
    {
        EnsureFolder(PlayerOut);
        EnsureFolder(EnemyOut);
        AnimatorController wizard = BuildPlayerController("wizzard_m", WizardController);
        BuildPlayerOverride(wizard, "wizzard_f", WizardFOverride);
        foreach (var e in Enemies) BuildEnemyController(e.character);
        AssetDatabase.SaveAssets();
        Debug.Log("DungeonCharacterBuilder: player and enemy animations built.");
    }

    // ---- player ----

    static Dictionary<string, AnimationClip> PlayerClips(string character)
    {
        var (idle, run, hit) = CharacterFrames(character);
        return new Dictionary<string, AnimationClip>
        {
            ["idle"] = Clip($"{PlayerOut}/{character}_idle.anim", idle, 8f, true),
            ["walk"] = Clip($"{PlayerOut}/{character}_run.anim", run, 12f, true),
            // The swing itself is the staff (StaffVisual); the body holds its idle pose for the swing.
            ["attack"] = Clip($"{PlayerOut}/{character}_attack.anim", new List<Sprite> { idle[0] }, 6f, false),
            ["dash"] = Clip($"{PlayerOut}/{character}_dash.anim", run, 28f, true),
            ["hurt"] = Clip($"{PlayerOut}/{character}_hurt.anim", hit, 5f, false),
            ["death"] = Clip($"{PlayerOut}/{character}_death.anim", hit, 2f, false),
        };
    }

    static AnimatorController BuildPlayerController(string character, string path)
    {
        Dictionary<string, AnimationClip> clips = PlayerClips(character);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState("Idle");
        idle.motion = clips["idle"];
        AnimatorState walk = machine.AddState("Walk");
        walk.motion = clips["walk"];
        AnimatorState attack = machine.AddState("Attack");
        attack.motion = clips["attack"];
        AnimatorState dash = machine.AddState("Dash");
        dash.motion = clips["dash"];
        AnimatorState hurt = machine.AddState("Hurt");
        hurt.motion = clips["hurt"];
        AnimatorState death = machine.AddState("Death");
        death.motion = clips["death"];
        machine.defaultState = idle;

        Instant(idle.AddTransition(walk)).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        Instant(walk.AddTransition(idle)).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        AddAnyTrigger(machine, attack, "Attack", true);
        AddAnyTrigger(machine, dash, "Dash", true);
        AddAnyTrigger(machine, hurt, "Hurt", true);
        AddAnyTrigger(machine, death, "Death", false);
        foreach (AnimatorState oneShot in new[] { attack, dash, hurt })
        {
            AnimatorStateTransition back = oneShot.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;
        }

        // A dash lasts about one run cycle at 28 fps.
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void BuildPlayerOverride(AnimatorController baseController, string character, string path)
    {
        Dictionary<string, AnimationClip> clips = PlayerClips(character);
        var over = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (over == null)
        {
            over = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(over, path);
        }

        over.runtimeAnimatorController = baseController;
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        over.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            string key = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(pairs[i].Key)).Replace("wizzard_m_", "");
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clips.TryGetValue(key, out AnimationClip c) ? c : null);
        }

        over.ApplyOverrides(pairs);
        EditorUtility.SetDirty(over);
    }

    // ---- enemies (EnemyAI parameters: MoveX/MoveY, IsMoving, Attack, Hurt triggers, Dead bool) ----

    static string EnemyControllerPath(string character) => $"{EnemyOut}/{character}.controller";

    static void BuildEnemyController(string character)
    {
        var (idleFrames, runFrames, _) = CharacterFrames(character);
        AnimationClip idleClip = Clip($"{EnemyOut}/{character}_idle.anim", idleFrames, 8f, true);
        AnimationClip runClip = Clip($"{EnemyOut}/{character}_run.anim", runFrames, 10f, true);
        AnimationClip attackClip = Clip($"{EnemyOut}/{character}_attack.anim", runFrames, 16f, false);
        AnimationClip deadClip = Clip($"{EnemyOut}/{character}_dead.anim", new List<Sprite> { idleFrames[0] }, 4f, false);

        string path = EnemyControllerPath(character);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState("Idle");
        idle.motion = idleClip;
        AnimatorState run = machine.AddState("Run");
        run.motion = runClip;
        AnimatorState attack = machine.AddState("Attack");
        attack.motion = attackClip;
        AnimatorState hurt = machine.AddState("Hurt");
        hurt.motion = idleClip;
        AnimatorState dead = machine.AddState("Dead");
        dead.motion = deadClip;
        machine.defaultState = idle;

        Instant(idle.AddTransition(run)).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        Instant(run.AddTransition(idle)).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        foreach (var (state, trigger) in new[] { (attack, "Attack"), (hurt, "Hurt") })
        {
            AnimatorStateTransition t = AddAnyTrigger(machine, state, trigger, true);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = state == hurt ? 0.3f : 1f;
            back.duration = 0f;
        }

        AnimatorStateTransition die = machine.AddAnyStateTransition(dead);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        die.canTransitionToSelf = false;
        Instant(die);
        EditorUtility.SetDirty(controller);
    }

    static AnimatorStateTransition AddAnyTrigger(AnimatorStateMachine machine, AnimatorState target, string trigger, bool selfTransition)
    {
        AnimatorStateTransition t = machine.AddAnyStateTransition(target);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.canTransitionToSelf = selfTransition;
        return Instant(t);
    }

    static AnimatorStateTransition Instant(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.duration = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        return t;
    }

    static Sprite Frame(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Frames}{name}.png");

    // ---- applying ----

    [MenuItem("Tools/Art/DungeonTilesetII/Apply To Player (open scene)")]
    public static void ApplyToPlayer()
    {
        var driver = Object.FindFirstObjectByType<PlayerAnimationDriver>();
        if (driver == null)
        {
            Debug.LogWarning("DungeonCharacterBuilder: no Player in the open scene.");
            return;
        }

        GameObject player = driver.gameObject;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WizardController);
        var animator = player.GetComponentInChildren<Animator>();
        GameObject visual = animator.gameObject;
        var body = visual.GetComponent<SpriteRenderer>();

        // Feet on the bottom of the movement collider (box 0.8 x 0.4 centred 0.6 below the root).
        visual.transform.localPosition = new Vector3(0f, -0.8f, 0f);
        animator.runtimeAnimatorController = controller;
        body.sprite = Frame("wizzard_m_idle_anim_f0");
        body.flipX = false;
        body.color = Color.white;
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        body.sortingOrder = 0;

        // Body and staff sort as one by the feet (the group sits on the feet), at the characters' order 10.
        var group = Get<SortingGroup>(visual);
        group.sortingOrder = 10;

        Transform staffT = visual.transform.Find("Staff");
        if (staffT == null)
        {
            staffT = new GameObject("Staff").transform;
            staffT.SetParent(visual.transform, false);
        }

        var staff = Get<SpriteRenderer>(staffT.gameObject);
        staff.sprite = Frame("weapon_red_magic_staff");
        staff.sortingOrder = 1;
        // The pack's staff (30 px) is taller than the wizard (about 20 px drawn): held at 3/4 size from its foot.
        // Rest pose; StaffVisual takes over at runtime.
        staffT.localScale = new Vector3(0.75f, 0.75f, 1f);
        staffT.localPosition = new Vector3(0.34f, 0.06f, 0f);
        staffT.localRotation = Quaternion.Euler(0f, 0f, -8f);

        SetField(driver, "mirrorWhenFacingLeft", p => p.boolValue = true);

        var staffVisual = Get<StaffVisual>(player);
        SetField(staffVisual, "body", p => p.objectReferenceValue = body);
        SetField(staffVisual, "staff", p => p.objectReferenceValue = staff);
        SetField(staffVisual, "handOffset", p => p.vector2Value = new Vector2(0.34f, 0.06f));

        // Tier look: 1 = wizard with the red staff, 2 = green staff, 3 = the robed wizzard_f with the green staff.
        var skin = Get<PlayerTierSkin>(player);
        var over = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(WizardFOverride);
        var so = new SerializedObject(skin);
        so.FindProperty("animator").objectReferenceValue = animator;
        SerializedProperty tiers = so.FindProperty("tierControllers");
        tiers.arraySize = 3;
        tiers.GetArrayElementAtIndex(0).objectReferenceValue = controller;
        tiers.GetArrayElementAtIndex(1).objectReferenceValue = controller;
        tiers.GetArrayElementAtIndex(2).objectReferenceValue = over;
        so.FindProperty("staff").objectReferenceValue = staff;
        SerializedProperty staffs = so.FindProperty("tierStaffSprites");
        staffs.arraySize = 3;
        staffs.GetArrayElementAtIndex(0).objectReferenceValue = Frame("weapon_red_magic_staff");
        staffs.GetArrayElementAtIndex(1).objectReferenceValue = Frame("weapon_green_magic_staff");
        staffs.GetArrayElementAtIndex(2).objectReferenceValue = Frame("weapon_green_magic_staff");
        so.ApplyModifiedPropertiesWithoutUndo();

        var fade = Get<DeathFade>(visual);
        fade.Duration = 0.9f;

        var afterimage = player.GetComponent<DashAfterimage>();
        if (afterimage != null) SetField(afterimage, "source", p => p.objectReferenceValue = body);

        // 16 px art: same 8.4 unit view as before (135 art px at 16 PPU = 270 px at 32 PPU), still pixel perfect.
        var rig = Object.FindFirstObjectByType<CameraFollow>();
        if (rig != null)
        {
            SetField(rig, "pixelsPerUnit", p => p.floatValue = DungeonTilesetIIImporter.Ppu);
            SetField(rig, "referenceHeight", p => p.floatValue = 135f);
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("DungeonCharacterBuilder: Player switched to wizzard_m in " + player.scene.name);
    }

    [MenuItem("Tools/Art/DungeonTilesetII/Apply To Enemies + Pickups")]
    public static void ApplyToEnemies()
    {
        foreach (var e in Enemies)
        {
            string path = $"{EnemyPrefabs}/{e.prefab}.prefab";
            if (e.copyFrom != null && !File.Exists(path))
            {
                AssetDatabase.CopyAsset($"{EnemyPrefabs}/{e.copyFrom}.prefab", path);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            ApplyEnemy(root, e.character, e.scale);
            if (e.copyFrom != null) root.name = e.prefab;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        SetPickupSprite("Assets/Prefabs/Combat/HeartPickup.prefab", "ui_heart_full");
        SetPickupSprite("Assets/Prefabs/Combat/StaminaOrb.prefab", "flask_blue");
        AssetDatabase.SaveAssets();
        Debug.Log("DungeonCharacterBuilder: enemy prefabs and pickups updated.");
    }

    static void ApplyEnemy(GameObject root, string character, float scale)
    {
        var (idle, _, _) = CharacterFrames(character);
        root.transform.localScale = new Vector3(scale, scale, 1f);

        // Feet circle: the pack's characters stand on the sprite's bottom edge.
        var circle = root.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = 0.28f;
            circle.offset = new Vector2(0f, 0.28f);
        }

        var box = root.GetComponent<BoxCollider2D>();
        if (box != null && circle == null)
        {
            float w = idle[0].rect.width / DungeonTilesetIIImporter.Ppu;
            box.size = new Vector2(w * 0.7f, 0.4f);
            box.offset = new Vector2(0f, 0.2f);
        }

        var animator = root.GetComponentInChildren<Animator>();
        GameObject visual = animator.gameObject;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one;
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath(character));
        var sr = visual.GetComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.flipX = false;
        sr.color = Color.white;
        sr.sortingOrder = 10; // same order as the player and props, so the feet decide (Y sort)
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (visual.GetComponent<SpriteFacing>() == null) visual.AddComponent<SpriteFacing>();

        if (root.GetComponent<Health>() != null)
        {
            var fade = Get<DeathFade>(visual);
            float duration = 0.8f;
            var ai = root.GetComponent<EnemyAI>();
            if (ai != null)
            {
                var stats = new SerializedObject(ai).FindProperty("stats")?.objectReferenceValue as EnemyStats;
                if (stats != null) duration = stats.deathDuration;
            }

            fade.Duration = duration;
        }
    }

    static void SetPickupSprite(string path, string frame)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var sr = root.GetComponentInChildren<SpriteRenderer>();
        sr.sprite = Frame(frame);
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // GetComponent returns a fake null in the editor, so ?? cannot be used for get-or-add.
    static T Get<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void SetField(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning($"DungeonCharacterBuilder: {target.GetType().Name} has no field {field}");
            return;
        }

        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
