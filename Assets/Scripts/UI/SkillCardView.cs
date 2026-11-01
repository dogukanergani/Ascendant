using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One card in the skill tree: icon, name, tier pips, next tier cost and effect, and the upgrade button.
// A locked card (a skill that is not in the game yet or not unlocked) shows "?" and cannot be upgraded.
public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] Image selection;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image[] pips;
    [SerializeField] TMP_Text costText;
    [SerializeField] TMP_Text effectText;
    [SerializeField] Button upgradeButton;
    [SerializeField] TMP_Text upgradeLabel;
    [SerializeField] Color pipOn = new(1f, 0.85f, 0.3f, 1f);
    [SerializeField] Color pipOff = new(0.25f, 0.27f, 0.35f, 1f);
    [SerializeField] Color placeholderColor = new(0.35f, 0.4f, 0.6f, 1f);

    SkillDefinition definition;

    public SkillDefinition Definition => definition;
    public bool IsLocked => definition == null;

    public event Action<SkillCardView> Hovered;
    public event Action<SkillCardView> UpgradeClicked;

    void Awake()
    {
        upgradeButton.onClick.AddListener(() => UpgradeClicked?.Invoke(this));
    }

    public void BindSkill(SkillDefinition skill)
    {
        definition = skill;
        nameText.text = skill.DisplayName;
        if (skill.Icon != null)
        {
            icon.sprite = skill.Icon;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }
        else
        {
            icon.sprite = null;
            icon.color = placeholderColor;
        }
    }

    public void BindLocked(string skillName)
    {
        definition = null;
        nameText.text = skillName;
        icon.sprite = null;
        icon.color = new Color(0.2f, 0.22f, 0.3f, 1f);
        SetPips(0);
        costText.text = string.Empty;
        effectText.text = "?";
        upgradeLabel.text = "Kilitli";
        upgradeButton.interactable = false;
    }

    // tier is the current tier; unlocked false greys the card out.
    public void Refresh(SkillManager manager)
    {
        if (definition == null || manager == null)
        {
            return;
        }

        string id = definition.SkillId;
        int tier = manager.GetTier(id);
        bool unlocked = manager.IsUnlocked(id);
        SetPips(tier);
        int cost = manager.GetUpgradeCost(id);
        if (!unlocked)
        {
            costText.text = string.Empty;
            effectText.text = "Kilitli";
            upgradeLabel.text = "Kilitli";
        }
        else if (cost < 0)
        {
            costText.text = "En üst kademe";
            effectText.text = definition.DescribeTier(tier);
            upgradeLabel.text = "Maks";
        }
        else
        {
            costText.text = $"Maliyet: {cost}";
            effectText.text = $"{definition.DescribeTier(tier + 1)}";
            upgradeLabel.text = "Yükselt";
        }

        upgradeButton.interactable = manager.CanUpgrade(id);
    }

    public void SetSelected(bool selected)
    {
        selection.enabled = selected;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered?.Invoke(this);
    }

    void SetPips(int filled)
    {
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].color = i < filled ? pipOn : pipOff;
        }
    }
}
