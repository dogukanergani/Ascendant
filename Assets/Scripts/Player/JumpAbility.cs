using UnityEngine;

public sealed class JumpAbility : MonoBehaviour, IPlayerAbility
{
    [SerializeField] float jumpHeight = 3f;
    [SerializeField] float timeToApex = 0.4f;
    [SerializeField] float fallGravityMultiplier = 1.6f;
    [SerializeField] float lowJumpGravityMultiplier = 2.5f;
    [SerializeField] float coyoteTime = 0.12f;
    [SerializeField] float jumpBufferTime = 0.12f;

    PlayerController2D player;
    float jumpVelocity;
    float baseGravityScale;

    public int Priority => 10;
    public bool IsActive => isActiveAndEnabled;

    public void Initialize(PlayerController2D owner)
    {
        player = owner;

        // Height and apex time define gravity and jump speed, so tuning stays intuitive.
        float gravity = 2f * jumpHeight / (timeToApex * timeToApex);
        jumpVelocity = 2f * jumpHeight / timeToApex;
        baseGravityScale = gravity / Mathf.Abs(Physics2D.gravity.y);
    }

    public void Tick(float deltaTime)
    {
        if (player.HasJumpRequest(jumpBufferTime) && player.GroundedWithin(coyoteTime))
        {
            Jump();
        }

        ApplyGravity();
    }

    void Jump()
    {
        player.ClearJumpRequest();
        player.ForgetGround();
        player.Velocity = new Vector2(player.Velocity.x, jumpVelocity);
    }

    // Releasing jump early or falling uses stronger gravity, which gives variable jump height.
    void ApplyGravity()
    {
        float scale = baseGravityScale;
        if (player.Velocity.y < 0f)
        {
            scale *= fallGravityMultiplier;
        }
        else if (!player.JumpHeld)
        {
            scale *= lowJumpGravityMultiplier;
        }

        player.Body.gravityScale = scale;
    }
}
