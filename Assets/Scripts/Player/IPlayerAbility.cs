public interface IPlayerAbility
{
    // Lower values tick first. Run = 0, Jump = 10, Wall = 20; skills can pick any value.
    int Priority { get; }

    bool IsActive { get; }

    void Initialize(PlayerController2D player);

    // Called from PlayerController2D.FixedUpdate after sensors are refreshed.
    void Tick(float deltaTime);
}
