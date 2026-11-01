using UnityEngine;

public sealed class DashSkill : SkillAbility
{
    [SerializeField] float dashSpeed = 18f;
    [SerializeField] float dashDuration = 0.15f;

    float dashTimeLeft;
    int dashDirection;

    // Ticks after Run (0), Jump (10) and Wall (20) so dash velocity and zero gravity win for the tick.
    public override int Priority => 30;

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

        dashDirection = Player.FacingDirection;
        dashTimeLeft = dashDuration;
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!IsDashing)
        {
            return;
        }

        dashTimeLeft -= deltaTime;
        Player.Body.gravityScale = 0f;
        Player.Velocity = new Vector2(dashDirection * dashSpeed, 0f);
    }
}
