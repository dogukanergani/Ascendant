using UnityEngine;

// 8-directional movement with acceleration and deceleration. Kept under the old class name so scene references survive.
public sealed class RunAbility : MonoBehaviour, IPlayerAbility
{
    [SerializeField] float maxSpeed = 6f;
    [SerializeField] float acceleration = 70f;
    [SerializeField] float deceleration = 90f;

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

        Vector2 target = player.MoveInput * maxSpeed;
        float rate = target.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        player.Velocity = Vector2.MoveTowards(player.Velocity, target, rate * deltaTime);
    }
}
