using UnityEngine;

public sealed class WallAbility : MonoBehaviour, IPlayerAbility
{
    [SerializeField] float slideSpeed = 2f;
    [SerializeField] Vector2 wallJumpForce = new Vector2(8f, 13f);
    [SerializeField] float wallJumpLockTime = 0.18f;
    [SerializeField] float wallCoyoteTime = 0.1f;
    [SerializeField] float wallJumpBufferTime = 0.12f;

    PlayerController2D player;

    public int Priority => 20;
    public bool IsActive => isActiveAndEnabled;

    public void Initialize(PlayerController2D owner)
    {
        player = owner;
    }

    public void Tick(float deltaTime)
    {
        if (!player.IsGrounded
            && player.HasJumpRequest(wallJumpBufferTime)
            && player.WallContactWithin(wallCoyoteTime))
        {
            WallJump();
            return;
        }

        if (IsSliding() && player.Velocity.y < -slideSpeed)
        {
            player.Velocity = new Vector2(player.Velocity.x, -slideSpeed);
        }
    }

    bool IsSliding()
    {
        return !player.IsGrounded && player.IsTouchingWall && player.Velocity.y < 0f;
    }

    void WallJump()
    {
        player.ClearJumpRequest();
        player.ForgetWall();
        player.LockInput(wallJumpLockTime);

        int awayFromWall = -player.LastWallDirection;
        player.Velocity = new Vector2(awayFromWall * wallJumpForce.x, wallJumpForce.y);
    }
}
