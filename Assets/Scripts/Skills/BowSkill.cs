using UnityEngine;
using UnityEngine.InputSystem;

// Fires a fast arrow in the facing direction (R). Tier 2 arrows pierce up to three enemies.
// Tier 3: the press only starts charging (it costs the normal arrow price); a quick release still shoots one arrow,
// holding for volleyHoldTime and releasing rains arrows around a point volleyDistance ahead for extra stamina.
public sealed class BowSkill : SkillAbility
{
    [SerializeField] Arrow arrowPrefab;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);
    [SerializeField] float spawnDistance = 0.6f;
    [SerializeField] LayerMask wallMask = 1 << 11;
    // Enemies one arrow can hit per tier (index 0 = tier 1).
    [SerializeField] int[] tierPierce = { 1, 3, 3 };
    [Header("Volley (tier 3)")]
    [SerializeField] float volleyHoldTime = 0.4f;
    [SerializeField] float volleyDistance = 5f;
    [SerializeField] int volleyCount = 8;
    [SerializeField] float volleySpread = 2.2f;
    // Stamina paid on release on top of the press cost (10 + 20 = 30 in total).
    [SerializeField] float volleyExtraStamina = 20f;
    [SerializeField] float volleyDamageMultiplier = 0.45f;
    [SerializeField] float volleyAreaRadius = 1f;
    [SerializeField] float volleyStagger = 0.06f;
    [SerializeField] float volleyFallHeight = 5f;
    [SerializeField] float volleyFallTime = 0.3f;
    [SerializeField] Color markerColor = new(1f, 0.85f, 0.3f, 0.5f);

    SkillManager manager;
    Stamina stamina;
    SpriteRenderer marker;
    bool charging;
    float chargeStart;

    public override int Priority => 46;

    protected override void OnInitialize()
    {
        stamina = Player.GetComponent<Stamina>();
    }

    void OnDisable()
    {
        StopCharging();
    }

    void OnDestroy()
    {
        if (marker != null)
        {
            Destroy(marker.gameObject);
        }
    }

    int Tier
    {
        get
        {
            if (manager == null)
            {
                manager = FindFirstObjectByType<SkillManager>();
            }

            return manager != null && Definition != null ? manager.GetTier(Definition.SkillId) : 1;
        }
    }

    public override bool TryActivate()
    {
        if (arrowPrefab == null || charging || !Player.CanControl)
        {
            return false;
        }

        if (Tier >= 3)
        {
            charging = true;
            chargeStart = Time.time;
            return true;
        }

        FireArrow();
        return true;
    }

    void Update()
    {
        if (!charging)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool held = keyboard != null && keyboard.rKey.isPressed;
        bool ready = Time.time - chargeStart >= volleyHoldTime;
        UpdateMarker(held && ready);
        if (held)
        {
            return;
        }

        StopCharging();
        if (ready && stamina != null && stamina.TrySpend(volleyExtraStamina))
        {
            FireVolley();
        }
        else
        {
            FireArrow();
        }
    }

    void StopCharging()
    {
        charging = false;
        if (marker != null)
        {
            marker.enabled = false;
        }
    }

    Vector2 Release => (Vector2)Player.transform.position + spawnOffset;

    void FireArrow()
    {
        int tier = Tier;
        Vector2 release = Release;
        Vector2 origin = release + Player.Facing * spawnDistance;
        if (Physics2D.Linecast(release, origin, wallMask))
        {
            origin = release;
        }

        Vector2Int range = Definition != null ? Definition.GetDamageRange(tier) : new Vector2Int(1, 1);
        DamageInfo damage = DamageRoll.Roll(range, 1f, Vector2.zero, DamageSource.Other);
        Arrow arrow = Instantiate(arrowPrefab, origin, Quaternion.identity);
        arrow.Launch(Player.Facing, damage, release, tierPierce[Mathf.Clamp(tier - 1, 0, tierPierce.Length - 1)]);
    }

    void FireVolley()
    {
        Vector2Int range = Definition != null ? Definition.GetDamageRange(Tier) : new Vector2Int(1, 1);
        Vector2 center = Release + Player.Facing * volleyDistance;
        for (int i = 0; i < volleyCount; i++)
        {
            // Sunflower spread: even coverage without a fixed pattern.
            float angle = i * 137.5f * Mathf.Deg2Rad;
            float distance = volleySpread * Mathf.Sqrt((i + 0.5f) / volleyCount);
            Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Arrow arrow = Instantiate(arrowPrefab, target, Quaternion.identity);
            arrow.Drop(target, i * volleyStagger, volleyFallHeight, volleyFallTime, range, volleyDamageMultiplier, volleyAreaRadius);
        }
    }

    void UpdateMarker(bool visible)
    {
        if (marker == null)
        {
            GameObject go = new GameObject("VolleyMarker");
            marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = SkillVisuals.SoftCircle;
            marker.sortingOrder = 3900;
        }

        marker.enabled = visible;
        marker.color = markerColor;
        marker.transform.position = Release + Player.Facing * volleyDistance;
        marker.transform.localScale = Vector3.one * volleySpread * 2f;
    }
}
