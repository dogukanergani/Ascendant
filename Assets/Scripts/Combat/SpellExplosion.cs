using System.Collections.Generic;
using UnityEngine;

// Short blue/purple burst drawn where a spell bolt lands. Sprites are generated once in code and the instances live in
// a small static pool, so a burst never instantiates after the first few.
public sealed class SpellExplosion : MonoBehaviour
{
    const int PoolSize = 8;
    const int TextureSize = 32;
    const float PixelsPerUnit = 16f; // sprite is 2 world units wide at scale 1

    static readonly List<SpellExplosion> pool = new();
    static Sprite sprite;
    static Transform host;
    static int nextIndex;

    SpriteRenderer sr;
    float age;
    float duration;
    float radius;
    bool inUse;

    public static void Spawn(Vector2 position, float radius, float duration = 0.25f)
    {
        SpellExplosion explosion = Acquire();
        explosion.Play(position, radius, duration);
    }

    static SpellExplosion Acquire()
    {
        if (host == null)
        {
            pool.Clear();
            host = new GameObject("SpellExplosions").transform;
        }

        foreach (SpellExplosion e in pool)
        {
            if (!e.inUse)
            {
                return e;
            }
        }

        if (pool.Count < PoolSize)
        {
            GameObject go = new GameObject("SpellExplosion");
            go.transform.SetParent(host, false);
            SpellExplosion created = go.AddComponent<SpellExplosion>();
            created.sr = go.AddComponent<SpriteRenderer>();
            created.sr.sprite = GetSprite();
            created.sr.sortingOrder = 4900;
            go.SetActive(false);
            pool.Add(created);
            return created;
        }

        // Every slot is busy: recycle in turn.
        nextIndex = (nextIndex + 1) % pool.Count;
        return pool[nextIndex];
    }

    void Play(Vector2 position, float explosionRadius, float length)
    {
        transform.position = position;
        transform.localScale = Vector3.one * 0.1f;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.value * 90f);
        radius = explosionRadius;
        duration = Mathf.Max(0.05f, length);
        age = 0f;
        inUse = true;
        sr.color = new Color(0.85f, 0.95f, 1f, 1f);
        gameObject.SetActive(true);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / duration;
        if (t >= 1f)
        {
            inUse = false;
            gameObject.SetActive(false);
            return;
        }

        // Fast ease-out growth to the full radius, then a fade; colour runs from white-blue to violet.
        float grow = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * (radius * Mathf.Lerp(0.2f, 1f, grow));
        Color c = Color.Lerp(new Color(0.85f, 0.95f, 1f), new Color(0.55f, 0.3f, 1f), t);
        c.a = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
        sr.color = c;
    }

    static Sprite GetSprite()
    {
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SpellExplosion"
        };

        float half = TextureSize * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = 0f;
                if (d <= 1f)
                {
                    // Bright outer ring, a soft translucent disc inside, and a solid core.
                    if (d > 0.78f)
                    {
                        a = 1f;
                    }
                    else if (d < 0.28f)
                    {
                        a = 0.9f;
                    }
                    else
                    {
                        a = 0.3f;
                    }

                    // Sparse pixel notches on the ring keep it from looking like a perfect outline.
                    if (d > 0.78f && ((x * 7 + y * 13) % 5 == 0))
                    {
                        a = 0f;
                    }
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        return sprite;
    }
}
