using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The skill tree screen. It is only opened from level transitions (the level select screen and the end-of-level
// score screen), never during play, so it does not pause the game. The player can pick a card with the mouse,
// arrow keys / d-pad and Enter / A, and close it with Esc / B or the close button.
public sealed class SkillTreePanel : MonoBehaviour
{
    [SerializeField] SkillManager manager;
    [SerializeField] GameObject content;
    [SerializeField] RectTransform cardContainer;
    [SerializeField] SkillCardView cardPrefab;
    [SerializeField] TMP_Text balanceText;
    [SerializeField] Button closeButton;
    // Skills that are in the design but not in the game yet can be listed here; they show as locked "?" cards.
    [SerializeField] string[] lockedSkillNames = System.Array.Empty<string>();
    [SerializeField] int columns = 4;

    readonly List<SkillCardView> cards = new();

    int selected;

    public static bool IsOpen { get; private set; }

    void Awake()
    {
        IsOpen = false;
        content.SetActive(false);
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    void OnEnable()
    {
        SkillPointService.Changed += OnPointsChanged;
    }

    void OnDisable()
    {
        SkillPointService.Changed -= OnPointsChanged;
        Close();
    }

    void Start()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
        }

        if (manager == null)
        {
            Debug.LogWarning("SkillTreePanel: no SkillManager in the scene.", this);
            return;
        }

        manager.TierChanged += OnTierChanged;
        manager.UnlockChanged += OnUnlockChanged;
        foreach (SkillDefinition definition in manager.Catalog)
        {
            if (definition != null)
            {
                AddCard().BindSkill(definition);
            }
        }

        foreach (string skillName in lockedSkillNames)
        {
            AddCard().BindLocked(skillName);
        }

        OnPointsChanged(SkillPointService.Balance);
        RefreshCards();
    }

    void OnDestroy()
    {
        if (manager != null)
        {
            manager.TierChanged -= OnTierChanged;
            manager.UnlockChanged -= OnUnlockChanged;
        }
    }

    void Update()
    {
        if (IsOpen)
        {
            HandleNavigation();
        }
    }

    SkillCardView AddCard()
    {
        SkillCardView card = Instantiate(cardPrefab, cardContainer);
        card.Hovered += OnCardHovered;
        card.UpgradeClicked += OnCardUpgradeClicked;
        cards.Add(card);
        return card;
    }

    public void Open()
    {
        if (IsOpen || manager == null)
        {
            return;
        }

        IsOpen = true;
        content.SetActive(true);
        SetSelected(Mathf.Clamp(selected, 0, cards.Count - 1));
        RefreshCards();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        content.SetActive(false);
    }

    void HandleNavigation()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        int dx = 0;
        int dy = 0;
        bool submit = false;
        bool cancel = false;
        if (keyboard != null)
        {
            dx += Pressed(keyboard.rightArrowKey) - Pressed(keyboard.leftArrowKey);
            dy += Pressed(keyboard.downArrowKey) - Pressed(keyboard.upArrowKey);
            submit |= keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            cancel |= keyboard.escapeKey.wasPressedThisFrame;
        }

        if (gamepad != null)
        {
            dx += Pressed(gamepad.dpad.right) - Pressed(gamepad.dpad.left);
            dy += Pressed(gamepad.dpad.down) - Pressed(gamepad.dpad.up);
            submit |= gamepad.buttonSouth.wasPressedThisFrame;
            cancel |= gamepad.buttonEast.wasPressedThisFrame;
        }

        if (cancel)
        {
            Close();
            return;
        }

        if (dx != 0 || dy != 0)
        {
            int next = selected + dx + dy * columns;
            if (next >= 0 && next < cards.Count)
            {
                SetSelected(next);
            }
        }

        if (submit && cards.Count > 0)
        {
            Upgrade(cards[selected]);
        }
    }

    static int Pressed(UnityEngine.InputSystem.Controls.ButtonControl control)
    {
        return control.wasPressedThisFrame ? 1 : 0;
    }

    void SetSelected(int index)
    {
        selected = index;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetSelected(i == index);
        }
    }

    void OnCardHovered(SkillCardView card)
    {
        SetSelected(cards.IndexOf(card));
    }

    void OnCardUpgradeClicked(SkillCardView card)
    {
        SetSelected(cards.IndexOf(card));
        Upgrade(card);
    }

    void Upgrade(SkillCardView card)
    {
        if (!card.IsLocked)
        {
            manager.TryUpgrade(card.Definition.SkillId);
        }
    }

    void OnTierChanged(SkillDefinition definition, int tier)
    {
        RefreshCards();
    }

    void OnUnlockChanged(SkillDefinition definition, bool unlocked)
    {
        RefreshCards();
    }

    void OnPointsChanged(int balance)
    {
        balanceText.text = balance.ToString();
        RefreshCards();
    }

    void RefreshCards()
    {
        foreach (SkillCardView card in cards)
        {
            card.Refresh(manager);
        }
    }
}
