using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Skills/Skill Definition", fileName = "Skill_New")]
public sealed class SkillDefinition : ScriptableObject
{
    [SerializeField] string skillId = "skill";
    [SerializeField] string displayName = "Skill";
    [SerializeField] Sprite icon;
    [SerializeField] float cooldown = 1f;
    [SerializeField] float staminaCost;
    [SerializeField] bool unlockedByDefault;
    // Key.None means the skill has no manual activation (passive skills like double jump).
    [SerializeField] Key activationKey = Key.None;

    // Base damage range per tier (index 0 = tier 1). Empty for skills that do no direct damage.
    [SerializeField] Vector2Int[] tierDamage = System.Array.Empty<Vector2Int>();

    public Vector2Int GetDamageRange(int tier)
    {
        if (tierDamage.Length == 0)
        {
            return Vector2Int.zero;
        }

        return tierDamage[Mathf.Clamp(tier - 1, 0, tierDamage.Length - 1)];
    }

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;
    public float StaminaCost => staminaCost;
    public bool UnlockedByDefault => unlockedByDefault;
    public Key ActivationKey => activationKey;
}
