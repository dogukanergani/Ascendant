using System.IO;
using UnityEditor;
using UnityEngine;

// Edit-mode orthographic renders of Level_01 (overview + one per room) into Assets/Screenshots (gitignored).
public static class LevelShots
{
    const string Folder = "Assets/Screenshots";

    static readonly (string name, float x, float y, float halfH)[] Views =
    {
        ("lvl_overview", 4f, 18f, 30f),
        ("lvl_room0_rest", 0f, 0f, 7f),
        ("lvl_room1", 19f, -1f, 9f),
        ("lvl_room2", 19f, 15f, 8f),
        ("lvl_room3", -3f, 15f, 9f),
        ("lvl_boss", -4f, 34f, 11f),
        ("lvl_sample", 48.5f, 5.5f, 9f),
        ("game_r0", 0f, 0f, 3.47f),
        ("game_corridor", 19f, 8f, 3.47f),
        ("game_r1_door", 10f, 0f, 3.47f),
        ("game_boss", -4f, 30f, 3.47f),
        ("game_r3", -3f, 15f, 3.47f),
    };

    [MenuItem("Tools/Level/Render Level_01 Shots")]
    public static void All()
    {
        Directory.CreateDirectory(Folder);
        GameObject guide = GameObject.Find("Layout_Guide");
        bool guideWasActive = guide != null && guide.activeSelf;
        if (guide != null) guide.SetActive(false);
        foreach (var (name, x, y, halfH) in Views)
        {
            Shot(name, x, y, halfH);
        }

        if (guide != null) guide.SetActive(guideWasActive);
    }

    public static void Shot(string name, float x, float y, float halfH)
    {
        int h = 900, w = 1600;
        var go = new GameObject("tmpcam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = halfH;
        cam.transform.position = new Vector3(x, y, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.035f, 0.07f);
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes($"{Folder}/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
