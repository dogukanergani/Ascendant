using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Skills/Skill Definition", fileName = "Skill_New")]
public sealed class SkillDefinition : ScriptableObject
{
    [SerializeField] string skillId = "skill";
    [SerializeField] string displayName = "Skill";
    [SerializeField] Sprite icon;
    [SerializeField] float cooldown = 1f;
    [SerializeField] bool unlockedByDefault;
    // Key.None means the skill has no manual activation (passive skills like double jump).
    [SerializeField] Key activationKey = Key.None;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;
    public bool UnlockedByDefault => unlockedByDefault;
    public Key ActivationKey => activationKey;
}
