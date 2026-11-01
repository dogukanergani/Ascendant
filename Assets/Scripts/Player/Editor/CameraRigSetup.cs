using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

// One-shot, idempotent conversion of content to the tilted camera: standing things get an UprightBillboard on a
// collider-free visual object. Run "Enemy Prefabs" once and "Open Scene" in each gameplay scene.
public static class CameraRigSetup
{
    static readonly string[] EnemyPrefabs =
    {
        "Orc1", "Orc2", "Orc3", "Orc3Boss", "Slime", "Bat"
    };

    // Props that lie flat on the ground and stay unrotated; every other prop under "Props" stands upright.
    static readonly string[] FlatProps = { "Weeds", "CrackDecal" };

    [MenuItem("Tools/Camera/Setup Tilt In Enemy Prefabs")]
    public static void SetupEnemyPrefabs()
    {
        foreach (string name in EnemyPrefabs)
        {
            string path = $"Assets/Prefabs/Enemies/{name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SplitVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log("CameraRigSetup: enemy prefabs now have a billboarded Visual child.");
    }

    [MenuItem("Tools/Camera/Setup Tilt In Open Scene")]
    public static void SetupOpenScene()
    {
        var player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            SpriteRenderer visual = player.GetComponentInChildren<SpriteRenderer>();
            if (visual != null && visual.gameObject != player.gameObject)
            {
                AddBillboard(visual.gameObject);
            }
        }

        GameObject props = GameObject.Find("Props");
        if (props != null)
        {
            var children = new List<Transform>();
            foreach (Transform child in props.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                if (child.GetComponent<SpriteRenderer>() == null || IsFlat(child.name))
                {
                    continue;
                }

                if (child.GetComponent<Collider2D>() != null)
                {
                    SplitVisual(child.gameObject);
                }
                else
                {
                    AddBillboard(child.gameObject);
                }
            }
        }

        Camera cam = Camera.main;
        if (cam != null && player != null && cam.GetComponent<CameraFollow>() == null)
        {
            cam.gameObject.AddComponent<CameraFollow>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("CameraRigSetup: open scene converted to the tilted camera.");
    }

    static bool IsFlat(string objectName)
    {
        foreach (string flat in FlatProps)
        {
            if (objectName.StartsWith(flat))
            {
                return true;
            }
        }

        return false;
    }

    static void AddBillboard(GameObject go)
    {
        if (go.GetComponent<UprightBillboard>() == null)
        {
            go.AddComponent<UprightBillboard>();
        }
    }

    // Moves the SpriteRenderer (and Animator) off an object that owns colliders/physics onto a "Visual" child.
    static void SplitVisual(GameObject owner)
    {
        Transform existing = owner.transform.Find("Visual");
        if (existing != null)
        {
            AddBillboard(existing.gameObject);
            return;
        }

        SpriteRenderer sprite = owner.GetComponent<SpriteRenderer>();
        if (sprite == null)
        {
            return;
        }

        var visual = new GameObject("Visual") { layer = owner.layer };
        visual.transform.SetParent(owner.transform, false);
        MoveComponent(sprite, visual);
        Animator animator = owner.GetComponent<Animator>();
        if (animator != null)
        {
            MoveComponent(animator, visual);
        }

        AddBillboard(visual);
    }

    static void MoveComponent(Component source, GameObject destination)
    {
        ComponentUtility.CopyComponent(source);
        ComponentUtility.PasteComponentAsNew(destination);
        Object.DestroyImmediate(source);
    }
}
