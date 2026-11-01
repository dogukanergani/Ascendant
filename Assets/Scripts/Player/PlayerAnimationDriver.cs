using System.Collections.Generic;
using UnityEngine;

// Drives the Animator on the Visual child. Parameters (all optional, missing ones are skipped silently):
// MoveX/MoveY (float, facing direction), IsMoving (bool), Attack/Dash/Hurt (trigger).
[RequireComponent(typeof(PlayerController2D))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int HurtHash = Animator.StringToHash("Hurt");

    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float movingSpeedThreshold = 0.3f;
    // The side clips are drawn facing right; left is the mirrored sprite.
    [SerializeField] bool mirrorWhenFacingLeft = true;

    readonly HashSet<int> parameters = new();

    PlayerController2D player;
    DashSkill dash;

    void Awake()
    {
        player = GetComponent<PlayerController2D>();
        dash = GetComponent<DashSkill>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        CacheParameters();
    }

    void OnEnable()
    {
        player.Hurt += OnHurt;
        if (dash != null)
        {
            dash.Dashed += OnDashed;
        }
    }

    void OnDisable()
    {
        player.Hurt -= OnHurt;
        if (dash != null)
        {
            dash.Dashed -= OnDashed;
        }
    }

    void Update()
    {
        Vector2 facing = player.Facing;
        SetFloat(MoveXHash, facing.x);
        SetFloat(MoveYHash, facing.y);
        SetBool(IsMovingHash, player.Velocity.magnitude > movingSpeedThreshold);

        if (spriteRenderer != null && mirrorWhenFacingLeft && Mathf.Abs(facing.x) > 0f)
        {
            spriteRenderer.flipX = facing.x < 0f;
        }
    }

    // Called by the staff attack skill when a swing starts.
    public void PlayAttack()
    {
        SetTrigger(AttackHash);
    }

    void OnDashed()
    {
        SetTrigger(DashHash);
    }

    void OnHurt()
    {
        SetTrigger(HurtHash);
    }

    void CacheParameters()
    {
        parameters.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            parameters.Add(parameter.nameHash);
        }
    }

    void SetFloat(int hash, float value)
    {
        if (parameters.Contains(hash)) animator.SetFloat(hash, value);
    }

    void SetBool(int hash, bool value)
    {
        if (parameters.Contains(hash)) animator.SetBool(hash, value);
    }

    void SetTrigger(int hash)
    {
        if (parameters.Contains(hash)) animator.SetTrigger(hash);
    }
}
