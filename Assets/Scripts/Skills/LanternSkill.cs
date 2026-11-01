using UnityEngine;

// Toggle (E): a soft yellow glow around the player that uncovers HiddenReveal objects inside its radius and drains
// stamina every second. Tier 2: also uncovers illusory walls and speeds the player up a little.
// Tier 3: switching it on fires a flash that stuns the enemies in the radius (with its own cooldown).
public sealed class LanternSkill : SkillAbility
{
    [SerializeField] float staminaPerSecond = 4f;
    // Stamina needed to light the lantern at all.
    [SerializeField] float minStaminaToLight = 3f;
    // Light radius per tier (index 0 = tier 1).
    [SerializeField] float[] tierRadius = { 4f, 6f, 8f };
    [SerializeField] float tier2SpeedFactor = 1.15f;
    [Header("Flash (tier 3)")]
    [SerializeField] float flashStunTime = 1.5f;
    [SerializeField] float flashCooldown = 8f;
    [SerializeField] LayerMask enemyMask = 1 << 9;
    [Header("Visual")]
    [SerializeField] Material lightMaterial;
    [SerializeField] Vector2 lightCenter = new(0f, 0.3f);
    [SerializeField] Color lightColor = new(1f, 0.85f, 0.35f, 0.4f);

    readonly Collider2D[] flashHits = new Collider2D[32];

    SkillManager manager;
    Stamina stamina;
    SpriteRenderer glow;
    bool lit;
    float flashReadyAt;

    // Ticks after Move (0) so the speed boost scales the velocity Move just set.
    public override int Priority => 26;

    public bool IsLit => lit;

    protected override void OnInitialize()
    {
        stamina = Player.GetComponent<Stamina>();
        GameObject go = new GameObject("LanternGlow");
        go.transform.SetParent(Player.transform, false);
        go.transform.localPosition = lightCenter;
        glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = SkillVisuals.SoftCircle;
        glow.sortingOrder = 3800;
        if (lightMaterial != null)
        {
            glow.sharedMaterial = lightMaterial;
        }

        glow.enabled = false;
    }

    void OnDisable()
    {
        SetLit(false);
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

    float Radius => tierRadius[Mathf.Clamp(Tier - 1, 0, tierRadius.Length - 1)];

    // Pressing toggles. Lighting needs a little stamina; switching off is always allowed.
    public override bool TryActivate()
    {
        if (lit)
        {
            SetLit(false);
            return true;
        }

        if (stamina == null || !stamina.CanSpend(minStaminaToLight))
        {
            return false;
        }

        SetLit(true);
        if (Tier >= 3 && Time.time >= flashReadyAt)
        {
            Flash();
        }

        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!lit)
        {
            return;
        }

        if (!stamina.TrySpend(staminaPerSecond * deltaTime))
        {
            SetLit(false);
            return;
        }

        int tier = Tier;
        if (tier >= 2)
        {
            Player.Velocity *= tier2SpeedFactor;
        }

        float radius = Radius;
        Vector2 center = (Vector2)Player.transform.position + lightCenter;
        foreach (HiddenReveal hidden in HiddenReveal.All)
        {
            if (((Vector2)hidden.transform.position - center).sqrMagnitude <= radius * radius && hidden.TryReveal(tier))
            {
                break; // the list changes while revealing; the rest are picked up next tick
            }
        }
    }

    void Update()
    {
        if (!lit)
        {
            return;
        }

        // Soft flicker so the light feels alive.
        float flicker = 1f + Mathf.Sin(Time.time * 7f) * 0.03f + Mathf.Sin(Time.time * 17.3f) * 0.02f;
        glow.transform.localScale = Vector3.one * (Radius * 2f * flicker);
        glow.color = lightColor;
    }

    void SetLit(bool value)
    {
        lit = value;
        if (glow != null)
        {
            glow.enabled = value;
        }
    }

    void Flash()
    {
        flashReadyAt = Time.time + flashCooldown;
        Vector2 center = (Vector2)Player.transform.position + lightCenter;
        float radius = Radius;
        SpellExplosion.Spawn(center, radius, 0.3f, Color.white, new Color(1f, 0.9f, 0.4f));
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };
        int count = Physics2D.OverlapCircle(center, radius, filter, flashHits);
        for (int i = 0; i < count; i++)
        {
            EnemyAI enemy = flashHits[i].GetComponentInParent<EnemyAI>();
            enemy?.Stun(flashStunTime, Vector2.zero);
        }
    }
}
