using System.Collections.Generic;
using UnityEngine;

// Basic melee: a three-hit staff combo. Every press swings in the facing direction (nudged toward the nearest enemy
// by a small aim assist); a moment into the swing the hit area stays active for a short window and damages and
// knocks back every enemy inside it once. Damage comes from the skill tier. Costs no stamina.
public sealed class StaffAttackSkill : SkillAbility
{
    [SerializeField] LayerMask enemyMask = 1 << 9;
    [SerializeField] float[] comboMultiplier = { 1f, 1f, 1.4f };
    [SerializeField] float[] comboKnockback = { 4f, 4f, 8f };
    // Time from pressing to the hit landing (frame 3 of 5).
    [SerializeField] float hitDelay = 0.1f;
    // How long the hit area stays active, so moving enemies are not missed by a single-frame check.
    [SerializeField] float hitWindow = 0.12f;
    // Time after a swing before the next press is accepted.
    [SerializeField] float swingInterval = 0.25f;
    // A press within this time after the last swing starts continues the combo.
    [SerializeField] float comboWindow = 0.5f;
    [SerializeField] float moveSpeedFactor = 0.4f;
    [SerializeField] Vector2 hitCenterOffset = new(0f, 0.3f);
    // Hit area: reach in front of the player, a little behind so enemies right beside them are covered, and width.
    [SerializeField] float hitForward = 1.6f;
    [SerializeField] float hitBehind = 0.2f;
    [SerializeField] float hitWidth = 1.4f;
    [Header("Aim assist")]
    [SerializeField] float assistRadius = 2.4f;
    [SerializeField] float assistAngle = 55f;
    // 0 keeps the facing direction, 1 aims straight at the target.
    [SerializeField, Range(0f, 1f)] float assistStrength = 0.7f;
    [SerializeField] float hitStopTime = 0.03f;
    [SerializeField] Material flashMaterial;

    readonly Collider2D[] hits = new Collider2D[32];
    readonly HashSet<IDamageable> hitThisSwing = new();

    PlayerInputReader input;
    PlayerAnimationDriver animationDriver;
    SkillManager manager;
    ShieldSkill shield;
    float parryBonus = 1f;
    int comboStep;
    int swingStep;
    float lastSwingStart = float.NegativeInfinity;
    float hitTime;
    bool swingPending;
    bool windowOpen;
    float windowEnd;
    bool landedThisSwing;
    Vector2 swingDirection = Vector2.down;
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

        swingPending = false;
        windowOpen = false;
        comboStep = 0;
        RestoreTimeScale();
    }

    void OnAttackPressed()
    {
        FindManager();
        if (manager != null && Definition != null)
        {
            manager.TryUse(Definition.SkillId);
        }
    }

    void FindManager()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
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

        swingDirection = AimedDirection();
        lastSwingStart = Time.time;
        hitTime = Time.time + hitDelay;
        swingPending = true;
        windowOpen = false;
        animationDriver?.PlayAttack();
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (IsSwinging)
        {
            Player.Velocity *= moveSpeedFactor;
        }

        if (swingPending && Time.time >= hitTime)
        {
            swingPending = false;
            BeginHitWindow();
        }

        if (windowOpen)
        {
            ApplyHit();
            if (Time.time >= windowEnd)
            {
                windowOpen = false;
                if (landedThisSwing && hitStopTime > 0f && !SkillTreePanel.IsOpen)
                {
                    Time.timeScale = 0f;
                    hitStopEnd = Time.unscaledTime + hitStopTime;
                }
            }
        }
    }

    void Update()
    {
        if (hitStopEnd > 0f && Time.unscaledTime >= hitStopEnd)
        {
            RestoreTimeScale();
        }
    }

    // The facing direction, turned toward the nearest enemy that is roughly in front of the player.
    Vector2 AimedDirection()
    {
        Vector2 facing = Player.Facing;
        Vector2 origin = (Vector2)Player.transform.position + hitCenterOffset;
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };
        int count = Physics2D.OverlapCircle(origin, assistRadius, filter, hits);
        float bestDistance = float.MaxValue;
        Vector2 best = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Health health = hits[i].GetComponentInParent<Health>();
            if (health == null || health.IsDead || hits[i].GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            Vector2 to = (Vector2)hits[i].bounds.center - origin;
            float distance = to.magnitude;
            if (distance < 0.01f || Vector2.Angle(facing, to) > assistAngle || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = to / distance;
        }

        if (bestDistance == float.MaxValue)
        {
            return facing;
        }

        return Vector2.Lerp(facing, best, assistStrength).normalized;
    }

    void BeginHitWindow()
    {
        swingStep = Mathf.Clamp(comboStep, 0, comboMultiplier.Length - 1);
        comboStep = (swingStep + 1) % comboMultiplier.Length;
        hitThisSwing.Clear();
        landedThisSwing = false;
        // Shield tier 3: the first swing after a parry hits for double.
        shield ??= GetComponent<ShieldSkill>();
        parryBonus = shield != null ? shield.ConsumeParryBonus() : 1f;
        windowOpen = true;
        windowEnd = Time.time + hitWindow;
    }

    void ApplyHit()
    {
        float centerAlong = (hitForward - hitBehind) * 0.5f;
        Vector2 center = (Vector2)Player.transform.position + hitCenterOffset + swingDirection * centerAlong;
        Vector2 size = new Vector2(hitForward + hitBehind, hitWidth);
        float angle = Mathf.Atan2(swingDirection.y, swingDirection.x) * Mathf.Rad2Deg;
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };
        int count = Physics2D.OverlapBox(center, size, angle, filter, hits);
        if (count == 0)
        {
            return;
        }

        FindManager();
        int tier = manager != null && Definition != null ? manager.GetTier(Definition.SkillId) : 1;
        Vector2Int range = Definition != null ? Definition.GetDamageRange(tier) : new Vector2Int(1, 1);
        float knockback = comboKnockback[Mathf.Min(swingStep, comboKnockback.Length - 1)];
        for (int i = 0; i < count; i++)
        {
            IDamageable target = hits[i].GetComponentInParent<IDamageable>();
            if (target == null || hitThisSwing.Contains(target) || hits[i].GetComponentInParent<PlayerController2D>() != null)
            {
                continue;
            }

            hitThisSwing.Add(target);
            DamageInfo info = DamageRoll.Roll(range, comboMultiplier[swingStep] * parryBonus, swingDirection * knockback, DamageSource.Staff);
            if (target.Damage(info))
            {
                landedThisSwing = true;
                HitFlash.Play(((Component)target).gameObject, flashMaterial);
            }
        }
    }

    void RestoreTimeScale()
    {
        if (hitStopEnd > 0f)
        {
            hitStopEnd = 0f;
            // The skill tree pauses the game itself; a hit stop ending must not unpause it.
            Time.timeScale = SkillTreePanel.IsOpen ? 0f : 1f;
        }
    }

    void OnDrawGizmos()
    {
        Vector2 direction = Application.isPlaying ? swingDirection : Vector2.down;
        float centerAlong = (hitForward - hitBehind) * 0.5f;
        Vector2 center = (Vector2)transform.position + hitCenterOffset + direction * centerAlong;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Gizmos.color = windowOpen ? Color.red : Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(hitForward + hitBehind, hitWidth, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
