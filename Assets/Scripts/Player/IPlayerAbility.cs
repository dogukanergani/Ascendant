public interface IPlayerAbility
{
    // Lower values tick first. Move = 0, Dash = 30, SpellBolt = 40; skills can pick any value.
    int Priority { get; }

    bool IsActive { get; }

    void Initialize(PlayerController2D player);

    // Called from PlayerController2D.FixedUpdate after input is refreshed.
    void Tick(float deltaTime);
}
