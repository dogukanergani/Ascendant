using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shows a floating number over every enemy the player damages. Listens to Health.AnyDamaged and reuses a fixed pool
// of DamageNumber objects, so a hit never allocates or instantiates.
public sealed class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] TMP_FontAsset font;
    [SerializeField] int poolSize = 40;
    // Above world sprites; the HUD canvas is a screen-space overlay and always draws over this.
    [SerializeField] int sortingOrder = 5000;
    [SerializeField] float lifetime = 0.6f;
    [SerializeField] float riseSpeed = 1.6f;
    [SerializeField] float horizontalSpread = 0.35f;
    [SerializeField] float headOffset = 0.9f;
    // Numbers closer than this to a new one are pushed up so they do not overlap.
    [SerializeField] float stackSpacing = 0.32f;
    [SerializeField] float normalScale = 0.5f;
    [SerializeField] float critScale = 0.85f;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color critColor = new(1f, 0.65f, 0.1f);
    [SerializeField] Color spellColor = new(0.45f, 0.75f, 1f);
    [SerializeField] Color spellCritColor = new(0.55f, 0.95f, 1f);

    readonly List<DamageNumber> pool = new();

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject item = new GameObject("DamageNumber");
            item.transform.SetParent(transform, false);
            DamageNumber number = item.AddComponent<DamageNumber>();
            number.Initialize(font, sortingOrder);
            pool.Add(number);
        }
    }

    void OnEnable()
    {
        Health.AnyDamaged += OnDamaged;
    }

    void OnDisable()
    {
        Health.AnyDamaged -= OnDamaged;
    }

    void OnDamaged(Health health, DamageInfo info)
    {
        if (info.Source == DamageSource.Enemy)
        {
            return;
        }

        Vector2 position = (Vector2)health.transform.position + new Vector2(Random.Range(-horizontalSpread, horizontalSpread), headOffset);
        position.y = FreeHeight(position);

        bool spell = info.Source == DamageSource.Spell;
        Color color = spell ? (info.IsCritical ? spellCritColor : spellColor) : (info.IsCritical ? critColor : normalColor);
        float scale = info.IsCritical ? critScale : normalScale;
        string text = info.IsCritical ? info.Amount + "!" : info.Amount.ToString();
        Vector2 velocity = new Vector2(Random.Range(-0.3f, 0.3f), riseSpeed);
        Acquire().Show(text, position, color, scale, velocity, lifetime);
    }

    // Raises the spawn height until it is clear of numbers still on screen near the same spot.
    float FreeHeight(Vector2 position)
    {
        float y = position.y;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            bool blocked = false;
            foreach (DamageNumber other in pool)
            {
                if (!other.InUse)
                {
                    continue;
                }

                Vector2 p = other.transform.position;
                if (Mathf.Abs(p.x - position.x) < 0.7f && Mathf.Abs(p.y - y) < stackSpacing)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                break;
            }

            y += stackSpacing;
        }

        return y;
    }

    // Free slot, or the oldest number when every one is busy.
    DamageNumber Acquire()
    {
        foreach (DamageNumber number in pool)
        {
            if (!number.InUse)
            {
                return number;
            }
        }

        return pool[0];
    }
}
