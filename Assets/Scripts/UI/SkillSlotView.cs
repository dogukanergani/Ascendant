using UnityEngine;
using UnityEngine.UI;

// One HUD skill icon: dimmed when on cooldown, with a radial fill overlay showing the remaining time.
public sealed class SkillSlotView : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Image cooldownOverlay;

    SkillManager manager;
    SkillDefinition definition;

    public SkillDefinition Definition => definition;

    public void Bind(SkillManager skillManager, SkillDefinition skill)
    {
        manager = skillManager;
        definition = skill;
        icon.sprite = skill.Icon;
        icon.preserveAspect = true;
        cooldownOverlay.fillAmount = 0f;
    }

    void Update()
    {
        if (manager == null)
        {
            return;
        }

        float remaining = manager.GetCooldownRemaining(definition.SkillId);
        cooldownOverlay.fillAmount = definition.Cooldown > 0f ? Mathf.Clamp01(remaining / definition.Cooldown) : 0f;
        icon.color = remaining > 0f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
    }
}
