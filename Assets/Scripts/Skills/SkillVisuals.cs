using UnityEngine;

// Small procedurally generated sprites shared by the skills (shield arc, soft light disc), so they need no PNG assets.
public static class SkillVisuals
{
    const int Size = 32;
    const float PixelsPerUnit = 32f; // both sprites are exactly 1 world unit wide at scale 1

    static Sprite halfCircle;
    static Sprite softCircle;

    // A thick arc on the right half of a circle (opens toward +x); rotate it to face a direction.
    public static Sprite HalfCircle
    {
        get
        {
            if (halfCircle == null)
            {
                halfCircle = Build("ShieldArc", (x, y, d) => x >= Size * 0.5f && d > 0.62f && d <= 1f ? 1f : 0f);
            }

            return halfCircle;
        }
    }

    // A disc that fades smoothly from the centre to the rim.
    public static Sprite SoftCircle
    {
        get
        {
            if (softCircle == null)
            {
                softCircle = Build("SoftCircle", (x, y, d) =>
                {
                    float falloff = Mathf.Clamp01(1f - d);
                    return d <= 1f ? falloff * falloff : 0f;
                }, FilterMode.Bilinear);
            }

            return softCircle;
        }
    }

    delegate float AlphaFunc(int x, int y, float normalizedDistance);

    static Sprite Build(string name, AlphaFunc alpha, FilterMode filter = FilterMode.Point)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };

        float half = Size * 0.5f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y, d)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }
}
