using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Drives the Animator on the Visual child. Parameters (all optional, missing ones are skipped silently):
// MoveX/MoveY (float, facing direction), IsMoving (bool), Attack/Dash/Hurt/Death (trigger).
[RequireComponent(typeof(PlayerController2D))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int HurtHash = Animator.StringToHash("Hurt");
    static readonly int DeathHash = Animator.StringToHash("Death");

    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float movingSpeedThreshold = 0.3f;
    // Only for controllers with a single side clip drawn facing right; the Mage set has both side rows.
    [SerializeField] bool mirrorWhenFacingLeft;
    // Red tint on the body when hurt (the DungeonTileset II hit frame is a single pose).
    [SerializeField] Color hurtTint = new(1f, 0.35f, 0.35f, 1f);
    [SerializeField] float hurtTintTime = 0.18f;

    // Raised with every staff swing, for visuals that are not part of the Animator (the held staff).
    public event Action AttackPlayed;

    readonly HashSet<int> parameters = new();

    PlayerController2D player;
    DashSkill dash;
    Health health;
    bool dead;

    void Awake()
    {
        player = GetComponent<PlayerController2D>();
        dash = GetComponent<DashSkill>();
        health = GetComponent<Health>();
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
        if (health != null)
        {
            health.Died += OnDied;
        }

        if (dash != null)
        {
            dash.Dashed += OnDashed;
        }
    }

    void OnDisable()
    {
        player.Hurt -= OnHurt;
        if (health != null)
        {
            health.Died -= OnDied;
        }

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
        if (!dead) AttackPlayed?.Invoke();
    }

    void OnDashed()
    {
        SetTrigger(DashHash);
    }

    // Death overrides every other state; the controller does not leave it.
    void OnDied()
    {
        dead = true;
        SetTrigger(DeathHash);
    }

    void OnHurt()
    {
        SetTrigger(HurtHash);
        if (spriteRenderer != null && hurtTintTime > 0f && isActiveAndEnabled)
        {
            StopCoroutine(nameof(HurtTint));
            StartCoroutine(nameof(HurtTint));
        }
    }

    IEnumerator HurtTint()
    {
        for (float t = 0f; t < hurtTintTime; t += Time.deltaTime)
        {
            spriteRenderer.color = Color.Lerp(hurtTint, Color.white, t / hurtTintTime);
            yield return null;
        }

        spriteRenderer.color = Color.white;
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
        // Nothing interrupts the death clip.
        if (parameters.Contains(hash) && (!dead || hash == DeathHash)) animator.SetTrigger(hash);
    }
}
