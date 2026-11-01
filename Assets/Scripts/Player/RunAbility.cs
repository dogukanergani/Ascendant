using UnityEngine;

public sealed class RunAbility : MonoBehaviour, IPlayerAbility
{
    [SerializeField] float maxSpeed = 8f;
    [SerializeField] float groundAcceleration = 80f;
    [SerializeField] float groundDeceleration = 100f;
    [SerializeField] float airAcceleration = 40f;
    [SerializeField] float airDeceleration = 30f;

    PlayerController2D player;

    public int Priority => 0;
    public bool IsActive => isActiveAndEnabled;

    public void Initialize(PlayerController2D owner)
    {
        player = owner;
    }

    public void Tick(float deltaTime)
    {
        if (!player.CanControl)
        {
            return;
        }

        float target = player.MoveInput * maxSpeed;
        bool hasInput = Mathf.Abs(target) > 0.01f;

        float rate;
        if (player.IsGrounded)
        {
            rate = hasInput ? groundAcceleration : groundDeceleration;
        }
        else
        {
            rate = hasInput ? airAcceleration : airDeceleration;
        }

        Vector2 velocity = player.Velocity;
        velocity.x = Mathf.MoveTowards(velocity.x, target, rate * deltaTime);
        player.Velocity = velocity;
    }
}
