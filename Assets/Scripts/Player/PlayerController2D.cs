using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerInputReader))]
public sealed class PlayerController2D : MonoBehaviour
{
    [SerializeField] float hurtInvulnerability = 0.6f;
    [SerializeField] float hurtKnockbackSpeed = 8f;
    [SerializeField] float hurtInputLock = 0.15f;

    readonly List<IPlayerAbility> abilities = new();

    Rigidbody2D body;
    Health health;
    PlayerInputReader input;

    float inputLockUntil = float.NegativeInfinity;
    float invulnerableUntil = float.NegativeInfinity;

    public event Action Hurt;

    public Rigidbody2D Body => body;
    public Vector2 Velocity
    {
        get => body.linearVelocity;
        set => body.linearVelocity = value;
    }

    // Raw move input, length clamped to 1 so diagonals are not faster.
    public Vector2 MoveInput { get; private set; }
    // Last movement direction snapped to one of the four sprite directions (right, left, up, down).
    public Vector2 Facing { get; private set; } = Vector2.down;
    public bool CanControl => Time.time >= inputLockUntil;
    public bool IsInvulnerable => health != null ? health.IsInvulnerable : Time.time < invulnerableUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputReader>();
        health = GetComponent<Health>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        foreach (IPlayerAbility ability in GetComponents<IPlayerAbility>())
        {
            AddAbility(ability);
        }
    }

    void FixedUpdate()
    {
        MoveInput = Vector2.ClampMagnitude(input.Move, 1f);
        UpdateFacing(MoveInput);

        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < abilities.Count; i++)
        {
            if (abilities[i].IsActive)
            {
                abilities[i].Tick(dt);
            }
        }
    }

    public void RegisterAbility(IPlayerAbility ability)
    {
        AddAbility(ability);
    }

    public void LockInput(float duration)
    {
        inputLockUntil = Time.time + duration;
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        if (health != null)
        {
            health.GrantInvulnerability(duration);
        }
    }

    // Returns false when the hit was ignored because the player is invulnerable.
    // With a Health component the hit goes through it and its Damaged event applies the knockback.
    public bool TakeHit(Vector2 sourcePosition, int damage = 1)
    {
        if (IsInvulnerable)
        {
            return false;
        }

        Vector2 away = body.position - sourcePosition;
        Vector2 knockback = (away.sqrMagnitude > 0.0001f ? away.normalized : -Facing) * hurtKnockbackSpeed;
        if (health != null)
        {
            return health.Damage(damage, knockback);
        }

        GrantInvulnerability(hurtInvulnerability);
        ApplyHurt(knockback);
        return true;
    }

    void OnDamaged(int amount, Vector2 knockback)
    {
        ApplyHurt(knockback);
    }

    void ApplyHurt(Vector2 knockback)
    {
        LockInput(hurtInputLock);
        Velocity = knockback;
        Hurt?.Invoke();
    }

    void AddAbility(IPlayerAbility ability)
    {
        ability.Initialize(this);
        abilities.Add(ability);
        abilities.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    void UpdateFacing(Vector2 move)
    {
        float ax = Mathf.Abs(move.x);
        float ay = Mathf.Abs(move.y);
        if (ax < 0.01f && ay < 0.01f)
        {
            return;
        }

        bool horizontal;
        if (Mathf.Abs(ax - ay) < 0.01f)
        {
            // Pure diagonal: keep the current facing axis.
            horizontal = Mathf.Abs(Facing.x) > 0f;
        }
        else
        {
            horizontal = ax > ay;
        }

        Facing = horizontal ? new Vector2(Mathf.Sign(move.x), 0f) : new Vector2(0f, Mathf.Sign(move.y));
    }
}
