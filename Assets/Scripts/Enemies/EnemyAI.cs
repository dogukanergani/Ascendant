using UnityEngine;

// Melee enemy brain: patrol, chase the player while it is visible, swing when in range.
// Tuned per orc through EnemyStats; drives the orc Animator (MoveX/MoveY, IsMoving, Attack, Hurt, Dead).
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public sealed class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Hurt, Dead }

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int HurtHash = Animator.StringToHash("Hurt");
    static readonly int DeadHash = Animator.StringToHash("Dead");

    // Steering candidates are tried at 0, +step, -step, +2 step, ... around the desired direction.
    const int SteerSamples = 9;
    const float SteerStep = 25f;

    [SerializeField] EnemyStats stats;

    public EnemyStats Stats => stats;
    [SerializeField] LayerMask wallMask = 1 << 11;
    [SerializeField] LayerMask enemyMask = 1 << 9;
    [SerializeField] float bodyRadius = 0.3f;
    [SerializeField] float whiskerLength = 0.7f;
    [SerializeField] float separationRadius = 0.8f;
    [SerializeField] float separationWeight = 0.8f;

    readonly Collider2D[] separationBuffer = new Collider2D[8];

    Rigidbody2D body;
    Health health;
    Animator animator;
    Collider2D[] colliders;
    Transform player;
    Collider2D playerCollider;
    Health playerHealth;

    State state = State.Patrol;
    Vector2 home;
    Vector2 patrolTarget;
    float patrolWaitUntil;
    Vector2 lastKnownPos;
    bool hasLastKnown;
    Vector2 facing = Vector2.down;
    Vector2 steerDir;
    Vector2 knockVelocity;
    float hurtUntil;
    float nextAttackTime;
    // Attack phases: telegraph, then swing (the hit lands at hitDelay), then recovery.
    float attackStart;
    bool hitDone;
    bool swingStarted;

    public State Current => state;

    // Spawned waves know where the player is from the start: they chase from any distance, through line of sight.
    bool hunting;

    public void StartHunting()
    {
        hunting = true;
    }

    // Positions are measured at the collider (the feet), not the transform pivot, for both orc and player.
    Vector2 Self => colliders[0].bounds.center;
    Vector2 Target => playerCollider.bounds.center;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        colliders = GetComponents<Collider2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.linearDamping = 0f;
        health.SetMaxParts(stats.maxHealth, 0f);
        home = Self;
        patrolTarget = home;
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void Start()
    {
        FindPlayer();
        SetFacing(facing);
    }

    void FindPlayer()
    {
        PlayerController2D controller = FindAnyObjectByType<PlayerController2D>();
        if (controller != null)
        {
            player = controller.transform;
            playerHealth = controller.GetComponent<Health>();
            playerCollider = controller.GetComponent<Collider2D>();
        }
    }

    void FixedUpdate()
    {
        if (state == State.Dead)
        {
            return;
        }

        if (playerCollider == null)
        {
            FindPlayer();
        }

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Attack: TickAttack(); break;
            case State.Hurt: TickHurt(); break;
        }
    }

    void TickPatrol()
    {
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        if (Time.time < patrolWaitUntil)
        {
            Stop();
            return;
        }

        if (Vector2.Distance(Self, patrolTarget) < 0.2f)
        {
            patrolWaitUntil = Time.time + stats.patrolWait;
            patrolTarget = home + Random.insideUnitCircle * stats.patrolRadius;
            Stop();
            return;
        }

        MoveToward(patrolTarget, stats.patrolSpeed);
    }

    void TickChase()
    {
        if (CanSeePlayer())
        {
            lastKnownPos = Target;
            hasLastKnown = true;
            Vector2 toPlayer = lastKnownPos - Self;
            float dist = toPlayer.magnitude;
            if (dist <= stats.attackRange && Time.time >= nextAttackTime)
            {
                BeginAttack(toPlayer);
                return;
            }

            if (dist <= stats.attackRange * 0.8f)
            {
                // Close enough: hold position and face the player while the cooldown runs.
                Stop();
                SetFacing(toPlayer);
                return;
            }

            MoveToward(lastKnownPos, stats.chaseSpeed);
            return;
        }

        if (hasLastKnown && Vector2.Distance(Self, lastKnownPos) > 0.3f)
        {
            MoveToward(lastKnownPos, stats.chaseSpeed);
            return;
        }

        // Lost the player and reached where it was last seen: patrol around here.
        hasLastKnown = false;
        home = Self;
        patrolTarget = home;
        patrolWaitUntil = Time.time + stats.patrolWait;
        state = State.Patrol;
        Stop();
    }

    void TickAttack()
    {
        body.linearVelocity = Vector2.zero;
        float t = Time.time - attackStart;

        if (!swingStarted && t >= stats.telegraphTime)
        {
            swingStarted = true;
            animator.SetTrigger(AttackHash);
        }

        if (swingStarted && !hitDone && t >= stats.telegraphTime + stats.hitDelay)
        {
            hitDone = true;
            ApplyHit();
        }

        if (t >= stats.telegraphTime + stats.attackDuration + stats.recoveryTime)
        {
            nextAttackTime = Time.time + stats.attackCooldown;
            state = State.Chase;
        }
    }

    void TickHurt()
    {
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, 20f * Time.fixedDeltaTime);
        body.linearVelocity = knockVelocity;
        if (Time.time < hurtUntil)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        if (playerCollider != null && !PlayerDead())
        {
            lastKnownPos = Target;
            hasLastKnown = true;
            state = State.Chase;
        }
        else
        {
            state = State.Patrol;
        }
    }

    void EnterChase()
    {
        state = State.Chase;
        lastKnownPos = Target;
        hasLastKnown = true;
    }

    void BeginAttack(Vector2 toPlayer)
    {
        state = State.Attack;
        attackStart = Time.time;
        hitDone = false;
        swingStarted = false;
        Stop();
        SetFacing(toPlayer);
    }

    // Lands the hit on the player if it is inside the swing area in front of the orc.
    void ApplyHit()
    {
        if (playerCollider == null)
        {
            return;
        }

        Vector2 center = Self + facing * (stats.attackRange * 0.5f);
        if (Vector2.Distance(playerCollider.ClosestPoint(center), center) > stats.hitRadius)
        {
            return;
        }

        Vector2 away = Target - Self;
        Vector2 knockback = (away.sqrMagnitude > 0.0001f ? away.normalized : facing) * stats.knockbackSpeed;
        player.GetComponent<IDamageable>()?.Damage(new DamageInfo(stats.damage, knockback, false, DamageSource.Enemy));
    }

    void OnDamaged(DamageInfo info)
    {
        Vector2 knockback = info.Knockback;
        if (state == State.Dead || health.IsDead)
        {
            return;
        }

        if (state == State.Attack && Random.value < stats.staggerResistance)
        {
            return;
        }

        state = State.Hurt;
        hurtUntil = Time.time + stats.hurtStun;
        knockVelocity = knockback.sqrMagnitude > 0.0001f ? knockback.normalized * stats.hurtKnockbackSpeed : Vector2.zero;
        body.linearVelocity = knockVelocity;
        animator.SetBool(IsMovingHash, false);
        animator.ResetTrigger(AttackHash);
        animator.SetTrigger(HurtHash);
    }

    void OnDied()
    {
        state = State.Dead;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        foreach (Collider2D c in colliders)
        {
            c.enabled = false;
        }

        animator.ResetTrigger(AttackHash);
        animator.ResetTrigger(HurtHash);
        animator.SetBool(IsMovingHash, false);
        animator.SetBool(DeadHash, true);
        Destroy(gameObject, stats.deathDuration);
    }

    bool PlayerDead()
    {
        return playerHealth != null && playerHealth.IsDead;
    }

    bool CanSeePlayer()
    {
        if (playerCollider == null || PlayerDead())
        {
            return false;
        }

        if (hunting)
        {
            return true;
        }

        Vector2 to = Target - Self;
        float range = state == State.Chase ? stats.sightRange * stats.loseSightFactor : stats.sightRange;
        float dist = to.magnitude;
        if (dist > range)
        {
            return false;
        }

        return dist < 0.01f || !Physics2D.Raycast(Self, to / dist, dist, wallMask);
    }

    void Stop()
    {
        body.linearVelocity = Vector2.zero;
        animator.SetBool(IsMovingHash, false);
    }

    void MoveToward(Vector2 target, float speed)
    {
        Vector2 desired = target - Self;
        if (desired.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }

        Vector2 dir = Steer(desired.normalized);
        body.linearVelocity = dir * speed;
        SetFacing(dir);
        animator.SetBool(IsMovingHash, true);
    }

    // Picks the free direction closest to the desired one by casting the body against walls along a fan of
    // candidates, then nudges it away from nearby orcs so they spread out without pushing each other.
    Vector2 Steer(Vector2 desired)
    {
        Vector2 best = desired;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < SteerSamples; i++)
        {
            int ring = (i + 1) / 2;
            float angle = ring * SteerStep * (i % 2 == 1 ? 1f : -1f);
            Vector2 candidate = Rotate(desired, angle);
            RaycastHit2D hit = Physics2D.CircleCast(Self, bodyRadius, candidate, whiskerLength, wallMask);
            float clearance = hit.collider == null ? 1f : hit.fraction;
            if (clearance < 0.4f)
            {
                continue;
            }

            // Prefer alignment with the goal, then open space, and keep last frame's heading to avoid jitter.
            float score = Vector2.Dot(candidate, desired) + clearance * 0.3f + Vector2.Dot(candidate, steerDir) * 0.15f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        steerDir = best;

        Vector2 push = Vector2.zero;
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = false };
        int count = Physics2D.OverlapCircle(body.position, separationRadius, filter, separationBuffer);
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D other = separationBuffer[i].attachedRigidbody;
            if (other == null || other == body)
            {
                continue;
            }

            Vector2 away = body.position - other.position;
            float d = away.magnitude;
            if (d < 0.0001f)
            {
                away = Random.insideUnitCircle;
                d = 0.1f;
            }

            push += away / d * (1f - d / separationRadius);
        }

        Vector2 result = best + push * separationWeight;
        return result.sqrMagnitude > 0.0001f ? result.normalized : best;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // Snaps the look direction to one of the four sprite directions and feeds the blend trees.
    void SetFacing(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        bool horizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        facing = horizontal ? new Vector2(Mathf.Sign(dir.x), 0f) : new Vector2(0f, Mathf.Sign(dir.y));
        animator.SetFloat(MoveXHash, facing.x);
        animator.SetFloat(MoveYHash, facing.y);
    }

    void OnDrawGizmosSelected()
    {
        if (stats == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);
    }
}
