using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the clips and animator controllers for the Craftpix Mage 1-3 player (Assets/Art/ThirdParty/.../Mage1-3).
// Tier 1 gets the base controller; tiers 2 and 3 are override controllers that swap in the Mage 2/3 clips.
// Sheets: 64x64 cells, rows = down, left, right, up. The pack has no dash clip, so Dash plays the walk frames.
public static class MageAnimationBuilder
{
    const string OutFolder = "Assets/Art/Player/Animations/Mage";
    const string BasePath = OutFolder + "/MagePlayer.controller";
    const int Tiers = 3;

    static readonly string[] Directions = { "down", "left", "right", "up" };
    static readonly Vector2[] DirectionVectors = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };

    // state, sheet file part, loop, frames per second
    static readonly (string state, string sheet, bool loop, float fps)[] States =
    {
        ("idle", "Idle", true, 8f),
        ("walk", "Walk", true, 10f),
        ("attack", "attack", false, 24f),
        ("hurt", "Hurt", false, 20f),
        ("death", "Death", false, 10f),
        ("dash", "Walk", true, 40f),
    };

    static string SheetPath(int level, string sheet) =>
        $"{CraftpixImporter.MageFolder}PNG/Mage_{level}lvl/With_shadow/Mage_lvl{level}_{sheet}.png";

    static string ClipPath(int level, string state, string dir) => $"{OutFolder}/L{level}/Mage_{state}_{dir}.anim";

    [MenuItem("Tools/Player/Build Mage Animation")]
    public static void Build()
    {
        Directory.CreateDirectory(OutFolder);
        for (int level = 1; level <= Tiers; level++)
        {
            Directory.CreateDirectory($"{OutFolder}/L{level}");
        }

        AssetDatabase.Refresh();
        for (int level = 1; level <= Tiers; level++)
        {
            foreach (var s in States)
            {
                BuildClips(level, s.state, s.sheet, s.loop, s.fps);
            }
        }

        AssetDatabase.SaveAssets();

        AnimatorController baseController = BuildBaseController();
        for (int level = 2; level <= Tiers; level++)
        {
            BuildOverride(baseController, level);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("MageAnimationBuilder: clips and controllers rebuilt.");
    }

    static void BuildClips(int level, string state, string sheet, bool loop, float fps)
    {
        string path = SheetPath(level, sheet);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var byName = new Dictionary<string, Sprite>();
        foreach (Object o in assets)
        {
            if (o is Sprite sprite)
            {
                byName[sprite.name] = sprite;
            }
        }

        if (byName.Count == 0)
        {
            throw new System.Exception($"MageAnimationBuilder: no sprites in {path} (not sliced yet?)");
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        int columns = byName.Count / 4;
        for (int row = 0; row < Directions.Length; row++)
        {
            var frames = new List<Sprite>();
            for (int col = 0; col < columns; col++)
            {
                frames.Add(byName[$"{fileName}_{row * columns + col}"]);
            }

            string clipPath = ClipPath(level, state, Directions[row]);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
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
        }
    }

    static AnimatorController BuildBaseController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath) != null)
        {
            AssetDatabase.DeleteAsset(BasePath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(BasePath);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = AddTreeState(controller, machine, "Idle", "idle");
        AnimatorState walk = AddTreeState(controller, machine, "Walk", "walk");
        AnimatorState attack = AddTreeState(controller, machine, "Attack", "attack");
        AnimatorState dash = AddTreeState(controller, machine, "Dash", "dash");
        AnimatorState hurt = AddTreeState(controller, machine, "Hurt", "hurt");
        AnimatorState death = AddTreeState(controller, machine, "Death", "death");
        machine.defaultState = idle;

        AnimatorStateTransition toWalk = idle.AddTransition(walk);
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        Instant(toWalk);
        AnimatorStateTransition toIdle = walk.AddTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        Instant(toIdle);

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

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorState AddTreeState(AnimatorController controller, AnimatorStateMachine machine, string stateName, string clipState)
    {
        AnimatorState state = controller.CreateBlendTreeInController(stateName, out BlendTree tree);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        for (int i = 0; i < Directions.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(1, clipState, Directions[i]));
            tree.AddChild(clip, DirectionVectors[i]);
        }

        return state;
    }

    static void AddAnyTrigger(AnimatorStateMachine machine, AnimatorState target, string trigger, bool selfTransition)
    {
        AnimatorStateTransition t = machine.AddAnyStateTransition(target);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.canTransitionToSelf = selfTransition;
        Instant(t);
    }

    static void Instant(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.duration = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
    }

    static void BuildOverride(AnimatorController baseController, int level)
    {
        string path = $"{OutFolder}/MagePlayer_L{level}.overrideController";
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
            AnimationClip original = pairs[i].Key;
            string replacement = AssetDatabase.GetAssetPath(original).Replace("/L1/", $"/L{level}/");
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, AssetDatabase.LoadAssetAtPath<AnimationClip>(replacement));
        }

        over.ApplyOverrides(pairs);
        EditorUtility.SetDirty(over);
    }

    // Points the Player in the open scene at the Mage set and adds the tier skin / dash afterimage components.
    [MenuItem("Tools/Player/Apply Mage To Open Scene")]
    public static void ApplyToOpenScene()
    {
        var driver = Object.FindFirstObjectByType<PlayerAnimationDriver>();
        if (driver == null)
        {
            Debug.LogWarning("MageAnimationBuilder: no Player in the open scene.");
            return;
        }

        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
        var animator = driver.GetComponentInChildren<Animator>();
        var renderer = driver.GetComponentInChildren<SpriteRenderer>();
        animator.runtimeAnimatorController = baseController;
        renderer.flipX = false;
        renderer.sprite = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(1, "idle", "down")) != null
            ? FirstSprite(ClipPath(1, "idle", "down"))
            : renderer.sprite;

        var so = new SerializedObject(driver);
        so.FindProperty("mirrorWhenFacingLeft").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var skin = driver.GetComponent<PlayerTierSkin>();
        if (skin == null)
        {
            skin = driver.gameObject.AddComponent<PlayerTierSkin>();
        }

        var skinSo = new SerializedObject(skin);
        skinSo.FindProperty("animator").objectReferenceValue = animator;
        SerializedProperty tiers = skinSo.FindProperty("tierControllers");
        tiers.arraySize = Tiers;
        tiers.GetArrayElementAtIndex(0).objectReferenceValue = baseController;
        for (int level = 2; level <= Tiers; level++)
        {
            tiers.GetArrayElementAtIndex(level - 1).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>($"{OutFolder}/MagePlayer_L{level}.overrideController");
        }

        skinSo.ApplyModifiedPropertiesWithoutUndo();

        var afterimage = driver.GetComponent<DashAfterimage>();
        if (afterimage == null && driver.GetComponent<DashSkill>() != null)
        {
            afterimage = driver.gameObject.AddComponent<DashAfterimage>();
        }

        if (afterimage != null)
        {
            var aso = new SerializedObject(afterimage);
            aso.FindProperty("source").objectReferenceValue = renderer;
            aso.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
        Debug.Log("MageAnimationBuilder: Player updated in " + driver.gameObject.scene.name);
    }

    static Sprite FirstSprite(string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            return AnimationUtility.GetObjectReferenceCurve(clip, b)[0].value as Sprite;
        }

        return null;
    }
}
