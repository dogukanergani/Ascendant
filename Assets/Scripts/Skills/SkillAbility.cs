using UnityEngine;

// Base for skills that act on the player. Implements IPlayerAbility so PlayerController2D ticks it.
// The component is enabled only while its skill is unlocked (see SkillManager), so a locked skill never runs.
public abstract class SkillAbility : MonoBehaviour, IPlayerAbility
{
    [SerializeField] SkillDefinition definition;

    protected PlayerController2D Player { get; private set; }

    public SkillDefinition Definition => definition;
    public abstract int Priority { get; }
    public bool IsActive => isActiveAndEnabled;

    public void Initialize(PlayerController2D owner)
    {
        Player = owner;
        OnInitialize();
    }

    public void Tick(float deltaTime)
    {
        OnTick(deltaTime);
    }

    // Called by SkillManager when the player uses the skill. Return false if it could not start.
    public virtual bool TryActivate()
    {
        return false;
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnTick(float deltaTime)
    {
    }
}
