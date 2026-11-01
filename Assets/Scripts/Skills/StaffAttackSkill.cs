using UnityEngine;

// Basic melee: a three-hit staff combo. Every press swings in the facing direction; the hit lands a moment into
// the swing and damages and knocks back everything on the enemy layer inside a short box. Costs no stamina.
public sealed class StaffAttackSkill : SkillAbility
{
    [SerializeField] LayerMask enemyMask = 1 << 9;
    [SerializeField] int[] comboDamage = { 1, 1, 2 };
    [SerializeField] float[] comboKnockback = { 4f, 4f, 8f };
    // Time from pressing to the hit landing (frame 3 of 5).
    [SerializeField] float hitDelay = 0.1f;
    // Time after a swing before the next press is accepted.
    [SerializeField] float swingInterval = 0.25f;
    // A press within this time after the last swing starts continues the combo.
    [SerializeField] float comboWindow = 0.5f;
    [SerializeField] float moveSpeedFactor = 0.4f;
    [SerializeField] Vector2 hitCenterOffset = new(0f, 0.3f);
    [SerializeField] float hitReach = 0.7f;
    [SerializeField] Vector2 hitSize = new(0.9f, 1.1f);
    [SerializeField] float hitStopTime = 0.04f;
    [SerializeField] Material flashMaterial;

    readonly Collider2D[] hits = new Collider2D[16];

    PlayerInputReader input;
    PlayerAnimationDriver animationDriver;
    SkillManager manager;
    int comboStep;
    float lastSwingStart = float.NegativeInfinity;
    float hitTime;
    bool hitPending;
    Vector2 swingDirection;
    float hitStopEnd;

    // Ticks after Move (0) so the slow-down scales the velocity Move just set.
    public override int Priority => 20;

    public bool IsSwinging => Time.time < lastSwingStart + swingInterval;
    public int ComboStep => comboStep;

    protected override void OnInitialize()
    {
        input = Player.GetComponent<PlayerInputReader>();
        animationDriver = Player.GetComponent<PlayerAnimationDriver>();
    }

    void OnEnable()
    {
        if (input == null)
        {
            input = GetComponent<PlayerInputReader>();
        }

        if (input != null)
        {
            input.AttackPressed += OnAttackPressed;
        }
    }

    void OnDisable()
    {
        if (input != null)
        {
            input.AttackPressed -= OnAttackPressed;
        }

        hitPending = false;
        comboStep = 0;
        RestoreTimeScale();
    }

    void OnAttackPressed()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
        }

        if (manager != null && Definition != null)
        {
            manager.TryUse(Definition.SkillId);
        }
    }

    public override bool TryActivate()
    {
        if (IsSwinging || !Player.CanControl)
        {
            return false;
        }

        // Too long since the last swing and the combo starts over.
        if (Time.time > lastSwingStart + comboWindow)
        {
            comboStep = 0;
        }

        swingDirection = Player.Facing;
        lastSwingStart = Time.time;
        hitTime = Time.time + hitDelay;
        hitPending = true;
        animationDriver?.PlayAttack();
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (IsSwinging)
        {
            Player.Velocity *= moveSpeedFactor;
        }

        if (hitPending && Time.time >= hitTime)
        {
            hitPending = false;
            ApplyHit();
        }
    }

    void Update()
    {
        if (hitStopEnd > 0f && Time.unscaledTime >= hitStopEnd)
        {
            RestoreTimeScale();
        }
    }

    void ApplyHit()
    {
        int step = Mathf.Clamp(comboStep, 0, comboDamage.Length - 1);
        Vector2 center = (Vector2)Player.transform.position + hitCenterOffset + swingDirection * hitReach;
        Vector2 size = Mathf.Abs(swingDirection.x) > 0f ? hitSize : new Vector2(hitSize.y, hitSize.x);

        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };
        int count = Physics2D.OverlapBox(center, size, 0f, filter, hits);
        float knockback = comboKnockback[Mathf.Min(step, comboKnockback.Length - 1)];
        int landed = 0;
        for (int i = 0; i < count; i++)
        {
            IDamageable target = hits[i].GetComponentInParent<IDamageable>();
            if (target == null || hits[i].GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            if (target.Damage(comboDamage[step], swingDirection * knockback))
            {
                landed++;
                HitFlash.Play(((Component)target).gameObject, flashMaterial);
            }
        }

        comboStep = (step + 1) % comboDamage.Length;
        if (landed > 0 && hitStopTime > 0f)
        {
            Time.timeScale = 0f;
            hitStopEnd = Time.unscaledTime + hitStopTime;
        }
    }

    void RestoreTimeScale()
    {
        if (hitStopEnd > 0f)
        {
            hitStopEnd = 0f;
            Time.timeScale = 1f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 facing = Application.isPlaying && Player != null ? Player.Facing : Vector2.down;
        Gizmos.DrawWireCube((Vector2)transform.position + hitCenterOffset + facing * hitReach, hitSize);
    }
}
