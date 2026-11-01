using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-game HUD: hearts, icons for unlocked skills with cooldown, and the level timer.
// Health is pushed in through SetHealth because the player has no health component yet.
public sealed class HudController : MonoBehaviour
{
    [SerializeField] Image[] hearts;
    [SerializeField] Sprite heartFull;
    [SerializeField] Sprite heartEmpty;
    [SerializeField] SkillSlotView slotPrefab;
    [SerializeField] Transform slotContainer;
    [SerializeField] TMP_Text timerText;
    [SerializeField] LevelTimer timer;
    [SerializeField] SkillManager skillManager;

    readonly Dictionary<string, SkillSlotView> slots = new();

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

        SetHealth(hearts.Length, hearts.Length);
    }

    void OnDestroy()
    {
        if (skillManager != null)
        {
            skillManager.UnlockChanged -= OnUnlockChanged;
        }
    }

    void Update()
    {
        if (timer != null)
        {
            timerText.text = LevelTimer.Format(timer.Elapsed);
        }
    }

    public void SetHealth(int current, int max)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].gameObject.SetActive(i < max);
            hearts[i].sprite = i < current ? heartFull : heartEmpty;
        }
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
