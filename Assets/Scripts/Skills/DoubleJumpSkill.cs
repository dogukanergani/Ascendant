using UnityEngine;

public sealed class DoubleJumpSkill : SkillAbility
{
    [SerializeField] int extraJumps = 1;
    [SerializeField] float jumpSpeed = 12f;
    [SerializeField] float jumpBufferTime = 0.12f;

    int jumpsLeft;

    // Ticks after Jump (10) so ground and coyote jumps are handled first, and before Wall (20) so wall jumps keep their request.
    public override int Priority => 11;

    protected override void OnInitialize()
    {
        jumpsLeft = extraJumps;
    }

    protected override void OnTick(float deltaTime)
    {
        if (Player.IsGrounded || Player.IsTouchingWall)
        {
            jumpsLeft = extraJumps;
            return;
        }

        if (jumpsLeft <= 0 || !Player.HasJumpRequest(jumpBufferTime))
        {
            return;
        }

        jumpsLeft--;
        Player.ClearJumpRequest();
        Player.Velocity = new Vector2(Player.Velocity.x, jumpSpeed);
    }
}
