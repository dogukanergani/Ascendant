using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerInputReader))]
public sealed class PlayerController2D : MonoBehaviour
{
    [SerializeField] LayerMask solidMask = ~0;
    [SerializeField] float sensorSkin = 0.02f;
    [SerializeField] float groundSensorDepth = 0.1f;
    [SerializeField] float wallSensorDepth = 0.08f;

    readonly List<IPlayerAbility> abilities = new();
    readonly List<Collider2D> hits = new();

    Rigidbody2D body;
    Collider2D col;
    PlayerInputReader input;
    ContactFilter2D solidFilter;

    float jumpRequestTime = float.NegativeInfinity;
    float inputLockUntil = float.NegativeInfinity;

    public Rigidbody2D Body => body;
    public Vector2 Velocity
    {
        get => body.linearVelocity;
        set => body.linearVelocity = value;
    }

    public float MoveInput { get; private set; }
    public bool JumpHeld => input.JumpHeld;
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }
    public int WallDirection { get; private set; }
    public int LastWallDirection { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    public bool CanControl => Time.time >= inputLockUntil;

    public float LastGroundedTime { get; private set; } = float.NegativeInfinity;
    public float LastWallTime { get; private set; } = float.NegativeInfinity;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        input = GetComponent<PlayerInputReader>();

        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        solidFilter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask, useTriggers = false };

        foreach (IPlayerAbility ability in GetComponents<IPlayerAbility>())
        {
            AddAbility(ability);
        }
    }

    void OnEnable()
    {
        input.JumpPressed += RequestJump;
    }

    void OnDisable()
    {
        input.JumpPressed -= RequestJump;
    }

    void FixedUpdate()
    {
        MoveInput = input.Move.x;
        if (Mathf.Abs(MoveInput) > 0.01f)
        {
            FacingDirection = MoveInput > 0f ? 1 : -1;
        }

        UpdateSensors();

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

    public void RequestJump()
    {
        jumpRequestTime = Time.time;
    }

    public bool HasJumpRequest(float window)
    {
        return Time.time - jumpRequestTime <= window;
    }

    public void ClearJumpRequest()
    {
        jumpRequestTime = float.NegativeInfinity;
    }

    public bool GroundedWithin(float window)
    {
        return Time.time - LastGroundedTime <= window;
    }

    public bool WallContactWithin(float window)
    {
        return Time.time - LastWallTime <= window;
    }

    public void ForgetGround()
    {
        LastGroundedTime = float.NegativeInfinity;
    }

    public void ForgetWall()
    {
        LastWallTime = float.NegativeInfinity;
    }

    public void LockInput(float duration)
    {
        inputLockUntil = Time.time + duration;
    }

    void AddAbility(IPlayerAbility ability)
    {
        ability.Initialize(this);
        abilities.Add(ability);
        abilities.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    void UpdateSensors()
    {
        Bounds b = col.bounds;

        IsGrounded = Probe(
            new Vector2(b.center.x, b.min.y - groundSensorDepth * 0.5f + sensorSkin),
            new Vector2(b.size.x * 0.9f, groundSensorDepth));

        bool leftWall = Probe(
            new Vector2(b.min.x - wallSensorDepth * 0.5f + sensorSkin, b.center.y),
            new Vector2(wallSensorDepth, b.size.y * 0.7f));

        bool rightWall = Probe(
            new Vector2(b.max.x + wallSensorDepth * 0.5f - sensorSkin, b.center.y),
            new Vector2(wallSensorDepth, b.size.y * 0.7f));

        IsTouchingWall = leftWall || rightWall;
        if (leftWall && rightWall)
        {
            WallDirection = MoveInput >= 0f ? 1 : -1;
        }
        else if (leftWall || rightWall)
        {
            WallDirection = leftWall ? -1 : 1;
        }
        else
        {
            WallDirection = 0;
        }

        // Only refresh coyote timers while not rising, so a jump cannot re-arm itself on the same frame.
        if (IsGrounded && body.linearVelocity.y <= 0.01f)
        {
            LastGroundedTime = Time.time;
        }

        if (IsTouchingWall && !IsGrounded && CanControl)
        {
            LastWallTime = Time.time;
            LastWallDirection = WallDirection;
        }
    }

    bool Probe(Vector2 center, Vector2 size)
    {
        hits.Clear();
        int count = Physics2D.OverlapBox(center, size, 0f, solidFilter, hits);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].attachedRigidbody != body)
            {
                return true;
            }
        }

        return false;
    }
}
