using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One HUD skill icon: a radial dark overlay with the remaining seconds while on cooldown, a dark cover while the
// player lacks the stamina for it, and the activation key on a small badge.
public sealed class SkillSlotView : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Image cooldownOverlay;
    [SerializeField] Image staminaCover;
    [SerializeField] TMP_Text keyText;
    [SerializeField] GameObject keyBadge;
    [SerializeField] TMP_Text cooldownText;

    SkillManager manager;
    SkillDefinition definition;
    Stamina stamina;

    public SkillDefinition Definition => definition;

    public void Bind(SkillManager skillManager, SkillDefinition skill, Stamina playerStamina, Sprite iconSprite)
    {
        manager = skillManager;
        definition = skill;
        stamina = playerStamina;
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        cooldownOverlay.fillAmount = 0f;
        staminaCover.enabled = false;
        cooldownText.gameObject.SetActive(false);
        string label = UITheme.KeyLabel(skill.ActivationKey);
        keyBadge.SetActive(label.Length > 0);
        RectTransform badge = (RectTransform)keyBadge.transform;
        badge.sizeDelta = new Vector2(Mathf.Max(42f, keyText.GetPreferredValues(label).x + 24f), badge.sizeDelta.y);
        keyText.text = label;
    }

    void Update()
    {
        if (manager == null)
        {
            return;
        }

        string id = definition.SkillId;
        float remaining = manager.GetCooldownRemaining(id);
        bool cooling = remaining > 0.05f;
        cooldownOverlay.fillAmount = cooling && definition.Cooldown > 0f ? Mathf.Clamp01(remaining / definition.Cooldown) : 0f;
        cooldownText.gameObject.SetActive(cooling && definition.Cooldown > 0.6f);
        if (cooling)
        {
            cooldownText.text = remaining < 10f ? remaining.ToString("0.0") : Mathf.CeilToInt(remaining).ToString();
        }

        bool lacksStamina = stamina != null && !stamina.CanSpend(definition.GetStaminaCost(manager.GetTier(id)));
        staminaCover.enabled = lacksStamina && !cooling;
    }
}
