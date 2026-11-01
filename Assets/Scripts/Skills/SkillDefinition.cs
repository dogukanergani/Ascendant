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

    // Skill points to go from tier 1 to 2 (index 0) and from tier 2 to 3 (index 1).
    [SerializeField] int[] upgradeCosts = { 5, 12 };
    // Stamina cost per tier (index 0 = tier 1). Empty keeps staminaCost for every tier.
    [SerializeField] float[] tierStaminaCost = System.Array.Empty<float>();
    // Multiplier on how far the skill reaches per tier (dash distance). Empty means 1 for every tier.
    [SerializeField] float[] tierReachMultiplier = System.Array.Empty<float>();
    // Extra effect text per tier shown in the skill tree (index 0 = tier 1), after the damage range.
    [SerializeField] string[] tierNotes = System.Array.Empty<string>();

    // Cost to reach tier + 1 from the given tier; -1 at the top tier.
    public int GetUpgradeCost(int tier)
    {
        if (tier < 1 || tier > upgradeCosts.Length)
        {
            return -1;
        }

        return upgradeCosts[tier - 1];
    }

    public float GetStaminaCost(int tier)
    {
        return tierStaminaCost.Length == 0 ? staminaCost : tierStaminaCost[Mathf.Clamp(tier - 1, 0, tierStaminaCost.Length - 1)];
    }

    public float GetReachMultiplier(int tier)
    {
        return tierReachMultiplier.Length == 0 ? 1f : tierReachMultiplier[Mathf.Clamp(tier - 1, 0, tierReachMultiplier.Length - 1)];
    }

    // What the skill does at a tier, for the skill tree: damage range, then the tier note.
    public string DescribeTier(int tier)
    {
        string text = string.Empty;
        Vector2Int damage = GetDamageRange(tier);
        if (damage != Vector2Int.zero)
        {
            text = $"Hasar {damage.x}-{damage.y}";
        }

        if (tierNotes.Length > 0)
        {
            string note = tierNotes[Mathf.Clamp(tier - 1, 0, tierNotes.Length - 1)];
            if (!string.IsNullOrEmpty(note))
            {
                text = text.Length > 0 ? $"{text}\n{note}" : note;
            }
        }

        return text;
    }

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
