using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-game HUD: hearts, stamina bar, icons for unlocked skills with cooldown, and the level timer.
// Hearts and stamina follow the player's Health and Stamina components.
public sealed class HudController : MonoBehaviour
{
    [SerializeField] UITheme theme;
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
    [SerializeField] float heartPunchDuration = 0.35f;

    readonly Dictionary<string, SkillSlotView> slots = new();

    Health health;
    Stamina stamina;
    PlayerController2D player;
    Vector2 staminaBarHome;
    float shakeEndsAt = float.NegativeInfinity;
    float[] heartPunchAt;
    int[] heartParts;

    void Start()
    {
        if (skillManager == null)
        {
            skillManager = FindFirstObjectByType<SkillManager>();
        }

        player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            health = player.GetComponent<Health>();
            stamina = player.GetComponent<Stamina>();
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

        heartPunchAt = new float[hearts.Length];
        heartParts = new int[hearts.Length];
        for (int i = 0; i < hearts.Length; i++)
        {
            heartPunchAt[i] = float.NegativeInfinity;
            heartParts[i] = -1;
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

        UpdateStamina();
        UpdateHeartPunch();
    }

    // Health is counted in parts; each heart shows up to two of them.
    public void SetHealth(int current, int max)
    {
        int partsPerHeart = health != null ? health.PartsPerHeart : 2;
        for (int i = 0; i < hearts.Length; i++)
        {
            int parts = Mathf.Clamp(current - i * partsPerHeart, 0, partsPerHeart);
            hearts[i].gameObject.SetActive(i * partsPerHeart < max);
            hearts[i].sprite = parts >= partsPerHeart ? heartFull : parts > 0 ? heartHalf : heartEmpty;
            // A heart that lost a part pops.
            if (heartParts != null && heartParts[i] >= 0 && parts < heartParts[i])
            {
                heartPunchAt[i] = Time.unscaledTime;
            }

            if (heartParts != null)
            {
                heartParts[i] = parts;
            }
        }
    }

    void UpdateHeartPunch()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            float t = (Time.unscaledTime - heartPunchAt[i]) / heartPunchDuration;
            float scale = t is >= 0f and < 1f ? 1f + 0.45f * Mathf.Sin(t * Mathf.PI) : 1f;
            hearts[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
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

    // Refused spend: the bar shakes and its frame flashes red. Low stamina: the fill turns red and pulses.
    void UpdateStamina()
    {
        if (stamina == null)
        {
            return;
        }

        Color frameColor = Color.white;
        Vector2 offset = Vector2.zero;
        bool low = stamina.Max > 0f && stamina.Current / stamina.Max < theme.lowStaminaFraction;
        if (low)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
            staminaFill.color = Color.Lerp(theme.stamina, theme.staminaLow, 0.55f + 0.45f * pulse);
            offset.x += Mathf.Sin(Time.unscaledTime * 55f) * 1.5f;
        }
        else
        {
            staminaFill.color = theme.stamina;
        }

        float remaining = shakeEndsAt - Time.unscaledTime;
        if (remaining > 0f)
        {
            float strength = remaining / shakeDuration;
            offset.x += Mathf.Sin(Time.unscaledTime * 80f) * shakeAmplitude * strength;
            frameColor = Color.Lerp(Color.white, theme.staminaLow, strength);
        }

        staminaBar.anchoredPosition = staminaBarHome + offset;
        staminaFrame.color = frameColor;
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
        Sprite icon = definition.Icon != null ? definition.Icon : theme.FallbackSkillIcon(definition.SkillId);
        if (icon == null || slots.ContainsKey(definition.SkillId))
        {
            return;
        }

        SkillSlotView slot = Instantiate(slotPrefab, slotContainer);
        slot.Bind(skillManager, definition, stamina, icon);
        slots[definition.SkillId] = slot;
    }
}
