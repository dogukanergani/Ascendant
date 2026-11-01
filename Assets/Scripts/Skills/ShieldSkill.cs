using UnityEngine;
using UnityEngine.InputSystem;

// Hold L or the right mouse button to raise the shield. While it is up it blocks melee hits that come from the front
// half circle (hit and knocks the attacker back), drains stamina every second and slows the player.
// Tier 2: a hit blocked in the first parryWindow seconds is a parry: the attacker is stunned and some stamina returns.
// Tier 3: after a parry the next staff hit deals double damage (see ConsumeParryBonus).
public sealed class ShieldSkill : SkillAbility
{
    [SerializeField] float staminaPerSecond = 12f;
    // Stamina needed to raise the shield at all.
    [SerializeField] float minStaminaToRaise = 5f;
    [SerializeField] float moveSpeedFactor = 0.5f;
    [SerializeField] float parryWindow = 0.2f;
    [SerializeField] float parryStunTime = 1.2f;
    [SerializeField] float parryStaminaRefund = 10f;
    [SerializeField] float blockPushSpeed = 7f;
    [SerializeField] float blockStunTime = 0.25f;
    [SerializeField] float parryBonusMultiplier = 2f;
    [SerializeField] float parryBonusDuration = 5f;
    [Header("Visual")]
    [SerializeField] Vector2 visualCenter = new(0f, 0.3f);
    [SerializeField] float visualDistance = 0.55f;
    [SerializeField] float visualSize = 1.5f;
    [SerializeField] Color shieldColor = new(0.35f, 0.65f, 1f, 0.9f);

    SkillManager manager;
    Stamina stamina;
    Health health;
    SpriteRenderer visual;
    bool raised;
    float raisedAt;
    float bonusUntil = float.NegativeInfinity;
    float flashUntil;

    // Ticks after Move (0) so the slow-down scales the velocity Move just set.
    public override int Priority => 25;

    public bool IsRaised => raised;

    protected override void OnInitialize()
    {
        stamina = Player.GetComponent<Stamina>();
        health = Player.GetComponent<Health>();
        if (health != null)
        {
            health.Interceptor = Intercept;
        }

        GameObject go = new GameObject("ShieldVisual");
        go.transform.SetParent(Player.transform, false);
        visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = SkillVisuals.HalfCircle;
        visual.sortingOrder = 4000;
        visual.enabled = false;
    }

    void OnDisable()
    {
        Lower();
    }

    void OnDestroy()
    {
        if (health != null && health.Interceptor == Intercept)
        {
            health.Interceptor = null;
        }
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            FindManager();
            if (manager != null && Definition != null)
            {
                manager.TryUse(Definition.SkillId);
            }
        }
    }

    void FindManager()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
        }
    }

    int Tier
    {
        get
        {
            FindManager();
            return manager != null && Definition != null ? manager.GetTier(Definition.SkillId) : 1;
        }
    }

    public override bool TryActivate()
    {
        if (raised || !Player.CanControl || stamina == null || !stamina.CanSpend(minStaminaToRaise))
        {
            return false;
        }

        raised = true;
        raisedAt = Time.time;
        visual.enabled = true;
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!raised)
        {
            return;
        }

        if (!IsHeld() || !stamina.TrySpend(staminaPerSecond * deltaTime))
        {
            Lower();
            return;
        }

        Player.Velocity *= moveSpeedFactor;
        UpdateVisual();
    }

    static bool IsHeld()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (keyboard != null && keyboard.lKey.isPressed) || (mouse != null && mouse.rightButton.isPressed);
    }

    void Lower()
    {
        raised = false;
        if (visual != null)
        {
            visual.enabled = false;
        }
    }

    void UpdateVisual()
    {
        Vector2 facing = Player.Facing;
        visual.transform.localPosition = visualCenter + facing * visualDistance;
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
        visual.transform.localScale = Vector3.one * visualSize;
        bool inParry = Tier >= 2 && Time.time - raisedAt <= parryWindow;
        visual.color = Time.time < flashUntil || inParry ? Color.white : shieldColor;
    }

    // Health.Interceptor: true swallows the hit.
    bool Intercept(DamageInfo info)
    {
        if (!raised || info.Source != DamageSource.Enemy || !info.HasOrigin)
        {
            return false;
        }

        Vector2 center = (Vector2)Player.transform.position + visualCenter;
        Vector2 toAttacker = info.Origin - center;
        if (toAttacker.sqrMagnitude > 0.0001f && Vector2.Angle(Player.Facing, toAttacker) > 90f)
        {
            return false;
        }

        flashUntil = Time.time + 0.12f;
        Vector2 away = toAttacker.sqrMagnitude > 0.0001f ? -toAttacker.normalized : -Player.Facing;
        bool parry = Tier >= 2 && Time.time - raisedAt <= parryWindow;
        if (info.Attacker != null)
        {
            if (parry)
            {
                info.Attacker.Stun(parryStunTime, -away * blockPushSpeed);
            }
            else
            {
                info.Attacker.Stun(blockStunTime, -away * blockPushSpeed);
            }
        }

        if (parry)
        {
            stamina?.Restore(parryStaminaRefund);
            if (Tier >= 3)
            {
                bonusUntil = Time.time + parryBonusDuration;
            }
        }

        return true;
    }

    // Called by the staff when a swing lands: 2 after a tier 3 parry (once), otherwise 1.
    public float ConsumeParryBonus()
    {
        if (Time.time > bonusUntil)
        {
            return 1f;
        }

        bonusUntil = float.NegativeInfinity;
        return parryBonusMultiplier;
    }
}
