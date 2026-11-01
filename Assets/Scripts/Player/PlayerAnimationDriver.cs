using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float runSpeedThreshold = 0.5f;
    // A single-step upward velocity gain larger than this counts as a jump (ground, wall or double jump).
    [SerializeField] float jumpImpulseThreshold = 4f;

    PlayerController2D player;
    float previousVy;

    void Awake()
    {
        player = GetComponent<PlayerController2D>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void FixedUpdate()
    {
        float vy = player.Velocity.y;
        if (vy - previousVy > jumpImpulseThreshold)
        {
            animator.SetTrigger(JumpHash);
        }

        previousVy = vy;
    }

    void Update()
    {
        bool running = player.IsGrounded
            && Mathf.Abs(player.MoveInput) > 0.01f
            && Mathf.Abs(player.Velocity.x) > runSpeedThreshold;
        animator.SetBool(IsRunningHash, running);
        spriteRenderer.flipX = player.FacingDirection < 0;
    }

    // Called by the future sword ability.
    public void PlayAttack()
    {
        animator.SetTrigger(AttackHash);
    }
}
