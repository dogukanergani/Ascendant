using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The skill tree screen. K (gamepad Select) opens and closes it; while open the game is paused and the player can
// pick a card with the mouse, arrow keys / d-pad and Enter / A. Also shows the skill point balance and a "+N" toast
// whenever points are earned, even when the tree is closed.
public sealed class SkillTreePanel : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] string actionMapName = "Player";
    [SerializeField] string toggleActionName = "SkillTree";
    [SerializeField] SkillManager manager;
    [SerializeField] GameObject content;
    [SerializeField] RectTransform cardContainer;
    [SerializeField] SkillCardView cardPrefab;
    [SerializeField] TMP_Text balanceText;
    [SerializeField] TMP_Text hudBalanceText;
    [SerializeField] TMP_Text toastText;
    // Skills that are in the design but not in the game yet; shown as locked "?" cards.
    [SerializeField] string[] lockedSkillNames = { "Bomba", "Yay", "Fener", "Kalkan" };
    [SerializeField] int columns = 4;
    [SerializeField] float toastDuration = 1.2f;
    [SerializeField] float toastRise = 40f;

    readonly List<SkillCardView> cards = new();

    InputAction toggleAction;
    int selected;
    float previousTimeScale = 1f;
    int toastAmount;
    float toastTimer;
    Vector2 toastHome;

    public static bool IsOpen { get; private set; }

    void Awake()
    {
        IsOpen = false;
        toastHome = toastText.rectTransform.anchoredPosition;
        toastText.gameObject.SetActive(false);
        content.SetActive(false);
    }

    void OnEnable()
    {
        if (actions != null)
        {
            InputActionMap map = actions.FindActionMap(actionMapName, false);
            toggleAction = map?.FindAction(toggleActionName, false);
            if (toggleAction != null)
            {
                toggleAction.performed += OnTogglePerformed;
                toggleAction.Enable();
            }
        }

        SkillPointService.Changed += OnPointsChanged;
        SkillPointService.Gained += OnPointsGained;
    }

    void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.performed -= OnTogglePerformed;
        }

        SkillPointService.Changed -= OnPointsChanged;
        SkillPointService.Gained -= OnPointsGained;
        if (IsOpen)
        {
            Close();
        }
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
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            SkillPointService.AddPoints(20);
        }
#endif

        UpdateToast();
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

    void OnTogglePerformed(InputAction.CallbackContext context)
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (IsOpen || manager == null)
        {
            return;
        }

        IsOpen = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
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
        Time.timeScale = previousTimeScale;
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
        string text = $"Puan: {balance}";
        balanceText.text = text;
        hudBalanceText.text = text;
        RefreshCards();
    }

    void RefreshCards()
    {
        foreach (SkillCardView card in cards)
        {
            card.Refresh(manager);
        }
    }

    // Gains within the toast's lifetime add up into one "+N".
    void OnPointsGained(int amount)
    {
        toastAmount = toastTimer > 0f ? toastAmount + amount : amount;
        toastTimer = toastDuration;
        toastText.text = $"+{toastAmount}";
        toastText.color = new Color(toastText.color.r, toastText.color.g, toastText.color.b, 1f);
        toastText.gameObject.SetActive(true);
    }

    void UpdateToast()
    {
        if (toastTimer <= 0f)
        {
            return;
        }

        toastTimer -= Time.unscaledDeltaTime;
        if (toastTimer <= 0f)
        {
            toastText.gameObject.SetActive(false);
            return;
        }

        float t = 1f - toastTimer / toastDuration;
        toastText.rectTransform.anchoredPosition = toastHome + new Vector2(0f, toastRise * t);
        Color color = toastText.color;
        color.a = Mathf.Clamp01(toastTimer / (toastDuration * 0.4f));
        toastText.color = color;
    }
}
