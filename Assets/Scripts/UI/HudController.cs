using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-game HUD: hearts, stamina bar, icons for unlocked skills with cooldown, and the level timer.
// Hearts and stamina follow the player's Health and Stamina components.
public sealed class HudController : MonoBehaviour
{
    [SerializeField] Image[] hearts;
    [SerializeField] Sprite heartFull;
    [SerializeField] Sprite heartHalf;
    [SerializeField] Sprite heartEmpty;
    [SerializeField] Image staminaFill;
    [SerializeField] RectTransform staminaBar;
    [SerializeField] Image staminaFrame;
    [SerializeField] SkillSlotView slotPrefab;
    [SerializeField] Transform slotContainer;
    [SerializeField] TMP_Text timerText;
    [SerializeField] LevelTimer timer;
    [SerializeField] SkillManager skillManager;
    [SerializeField] float shakeDuration = 0.3f;
    [SerializeField] float shakeAmplitude = 10f;

    static readonly Color FrameColor = new(0.14f, 0.16f, 0.24f, 1f);
    static readonly Color FrameFlashColor = new(0.85f, 0.2f, 0.2f, 1f);

    readonly Dictionary<string, SkillSlotView> slots = new();

    Health health;
    Stamina stamina;
    Vector2 staminaBarHome;
    float shakeEndsAt = float.NegativeInfinity;

    void Start()
    {
        if (skillManager == null)
        {
            skillManager = FindFirstObjectByType<SkillManager>();
        }

        if (skillManager == null)
        {
            Debug.LogWarning("HudController: no SkillManager in the scene; skill icons are hidden.", this);
        }
        else
        {
            skillManager.UnlockChanged += OnUnlockChanged;
            foreach (SkillDefinition definition in skillManager.Catalog)
            {
                if (definition != null && skillManager.IsUnlocked(definition.SkillId))
                {
                    AddSlot(definition);
                }
            }
        }

        PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            health = player.GetComponent<Health>();
            stamina = player.GetComponent<Stamina>();
        }

        if (health != null)
        {
            health.Changed += SetHealth;
            SetHealth(health.Current, health.Max);
        }
        else
        {
            Debug.LogWarning("HudController: the player has no Health; showing full hearts.", this);
            SetHealth(hearts.Length * 2, hearts.Length * 2);
        }

        staminaBarHome = staminaBar.anchoredPosition;
        if (stamina != null)
        {
            stamina.Changed += SetStamina;
            stamina.Insufficient += OnStaminaInsufficient;
            SetStamina(stamina.Current, stamina.Max);
        }
        else
        {
            Debug.LogWarning("HudController: the player has no Stamina; the bar is hidden.", this);
            staminaBar.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (skillManager != null)
        {
            skillManager.UnlockChanged -= OnUnlockChanged;
        }

        if (health != null)
        {
            health.Changed -= SetHealth;
        }

        if (stamina != null)
        {
            stamina.Changed -= SetStamina;
            stamina.Insufficient -= OnStaminaInsufficient;
        }
    }

    void Update()
    {
        if (timer != null)
        {
            timerText.text = LevelTimer.Format(timer.Elapsed);
        }

        UpdateStaminaShake();
    }

    // Health is counted in parts; each heart shows up to two of them.
    public void SetHealth(int current, int max)
    {
        int partsPerHeart = health != null ? health.PartsPerHeart : 2;
        for (int i = 0; i < hearts.Length; i++)
        {
            int parts = current - i * partsPerHeart;
            hearts[i].gameObject.SetActive(i * partsPerHeart < max);
            hearts[i].sprite = parts >= partsPerHeart ? heartFull : parts > 0 ? heartHalf : heartEmpty;
        }
    }

    void SetStamina(float current, float max)
    {
        staminaFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    void OnStaminaInsufficient()
    {
        shakeEndsAt = Time.unscaledTime + shakeDuration;
    }

    void UpdateStaminaShake()
    {
        if (stamina == null)
        {
            return;
        }

        float remaining = shakeEndsAt - Time.unscaledTime;
        if (remaining <= 0f)
        {
            staminaBar.anchoredPosition = staminaBarHome;
            staminaFrame.color = FrameColor;
            return;
        }

        float strength = remaining / shakeDuration;
        float offset = Mathf.Sin(Time.unscaledTime * 80f) * shakeAmplitude * strength;
        staminaBar.anchoredPosition = staminaBarHome + new Vector2(offset, 0f);
        staminaFrame.color = Color.Lerp(FrameColor, FrameFlashColor, strength);
    }

    void OnUnlockChanged(SkillDefinition definition, bool unlocked)
    {
        if (unlocked)
        {
            AddSlot(definition);
        }
        else if (slots.Remove(definition.SkillId, out SkillSlotView slot))
        {
            Destroy(slot.gameObject);
        }
    }

    void AddSlot(SkillDefinition definition)
    {
        if (slots.ContainsKey(definition.SkillId))
        {
            return;
        }

        SkillSlotView slot = Instantiate(slotPrefab, slotContainer);
        slot.Bind(skillManager, definition);
        slots[definition.SkillId] = slot;
    }
}
