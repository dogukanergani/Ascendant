using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One card in the skill tree: icon, name, tier pips, effect text, and the upgrade button with its cost.
// A locked card (a skill that is not in the game yet or not unlocked) shows "?" and cannot be upgraded.
public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] UITheme theme;
    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image[] pips;
    [SerializeField] TMP_Text effectText;
    [SerializeField] Button upgradeButton;
    [SerializeField] TMP_Text upgradeLabel;
    [SerializeField] TMP_Text costText;
    [SerializeField] GameObject costIcon;

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
        Sprite sprite = skill.Icon != null ? skill.Icon : theme.FallbackSkillIcon(skill.SkillId);
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.color = Color.white;
        icon.preserveAspect = true;
    }

    public void BindLocked(string skillName)
    {
        definition = null;
        nameText.text = skillName;
        icon.sprite = theme.lockIcon;
        icon.enabled = true;
        icon.color = Color.white;
        icon.preserveAspect = true;
        SetPips(0);
        effectText.text = "?";
        SetButton("KİLİTLİ", -1, false);
    }

    public void Refresh(SkillManager manager)
    {
        if (definition == null || manager == null)
        {
            return;
        }

        string id = definition.SkillId;
        int tier = manager.GetTier(id);
        bool unlocked = manager.IsUnlocked(id);
        SetPips(unlocked ? tier : 0);
        int cost = manager.GetUpgradeCost(id);
        if (!unlocked)
        {
            effectText.text = "Kilitli";
            SetButton("KİLİTLİ", -1, false);
        }
        else if (cost < 0)
        {
            effectText.text = definition.DescribeTier(tier);
            SetButton("MAKS", -1, false);
        }
        else
        {
            effectText.text = definition.DescribeTier(tier + 1);
            SetButton("YÜKSELT", cost, manager.CanUpgrade(id));
        }
    }

    public void SetSelected(bool selected)
    {
        background.sprite = selected ? theme.cardSelectedSprite : theme.cardSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered?.Invoke(this);
    }

    // cost < 0 hides the coin and price. Disabled upgrade buttons use the grey sprite and a dim label.
    void SetButton(string label, int cost, bool interactable)
    {
        upgradeLabel.text = label;
        costText.text = cost >= 0 ? cost.ToString() : string.Empty;
        costIcon.SetActive(cost >= 0);
        upgradeButton.interactable = interactable;
        upgradeLabel.color = interactable ? theme.border : theme.disabledText;
        costText.color = interactable ? theme.border : theme.disabledText;
        // With a price the label sits left of the coin; without one it fills the button.
        RectTransform labelRect = upgradeLabel.rectTransform;
        labelRect.offsetMax = new Vector2(cost >= 0 ? -96f : -8f, -8f);
    }

    void SetPips(int filled)
    {
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].sprite = i < filled ? theme.pipFull : theme.pipEmpty;
        }
    }
}
