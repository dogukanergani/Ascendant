using System;
using UnityEngine;

public sealed class DashSkill : SkillAbility
{
    [SerializeField] float dashSpeed = 16f;
    [SerializeField] float dashDuration = 0.15f;
    // Invulnerability outlasts the dash slightly so the exit frames are safe too.
    [SerializeField] float invulnerabilityPadding = 0.05f;

    SkillManager manager;
    float dashTimeLeft;
    float currentSpeed;
    Vector2 dashDirection;

    // Ticks after Move (0) so dash velocity wins for the tick.
    public override int Priority => 30;

    public event Action Dashed;

    public bool IsDashing => dashTimeLeft > 0f;

    void OnDisable()
    {
        dashTimeLeft = 0f;
    }

    public override bool TryActivate()
    {
        if (IsDashing)
        {
            return false;
        }

        Vector2 input = Player.MoveInput;
        dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : Player.Facing;
        dashTimeLeft = dashDuration;
        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
        }

        // Higher tiers dash farther: same duration, more speed.
        int tier = manager != null && Definition != null ? manager.GetTier(Definition.SkillId) : 1;
        currentSpeed = dashSpeed * (Definition != null ? Definition.GetReachMultiplier(tier) : 1f);
        Player.GrantInvulnerability(dashDuration + invulnerabilityPadding);
        Dashed?.Invoke();
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!IsDashing)
        {
            return;
        }

        dashTimeLeft -= deltaTime;
        Player.Velocity = dashDirection * currentSpeed;
    }
}
