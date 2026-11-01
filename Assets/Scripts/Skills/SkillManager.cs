using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SkillManager : MonoBehaviour
{
    [SerializeField] PlayerController2D player;
    [SerializeField] SkillDefinition[] catalog = Array.Empty<SkillDefinition>();
    // Menu scenes (level select) have no player: the manager only holds tiers and unlocks for the skill tree.
    [SerializeField] bool menuMode;

    readonly Dictionary<string, SkillDefinition> definitions = new();
    readonly Dictionary<string, SkillAbility> abilities = new();
    readonly Dictionary<string, bool> unlocked = new();
    readonly Dictionary<string, float> cooldownEnds = new();
    readonly Dictionary<string, int> tiers = new();

    Stamina stamina;

    public event Action<SkillDefinition, bool> UnlockChanged;
    public event Action<SkillDefinition> Used;
    public event Action<SkillDefinition, int> TierChanged;

    public IReadOnlyList<SkillDefinition> Catalog => catalog;

    void Awake()
    {
        if (player == null && !menuMode)
        {
            Debug.LogError("SkillManager: PlayerController2D is not assigned.", this);
            enabled = false;
            return;
        }

        if (player != null)
        {
            stamina = player.GetComponent<Stamina>();
            foreach (SkillAbility ability in player.GetComponents<SkillAbility>())
            {
                if (ability.Definition != null)
                {
                    abilities[ability.Definition.SkillId] = ability;
                }
            }
        }

        foreach (SkillDefinition definition in catalog)
        {
            if (definition == null)
            {
                continue;
            }

            if (definitions.ContainsKey(definition.SkillId))
            {
                Debug.LogWarning($"SkillManager: duplicate skill id '{definition.SkillId}'.", definition);
                continue;
            }

            definitions[definition.SkillId] = definition;
            unlocked[definition.SkillId] = definition.UnlockedByDefault;
        }

        foreach (SkillSaveEntry entry in SkillSaveStorage.Load().entries)
        {
            if (unlocked.ContainsKey(entry.skillId))
            {
                unlocked[entry.skillId] = entry.unlocked;
                tiers[entry.skillId] = Mathf.Clamp(entry.tier, 1, MaxTier);
            }
        }

        foreach (KeyValuePair<string, SkillDefinition> pair in definitions)
        {
            if (abilities.TryGetValue(pair.Key, out SkillAbility ability))
            {
                ability.enabled = unlocked[pair.Key];
            }
            else if (!menuMode)
            {
                Debug.LogWarning($"SkillManager: no SkillAbility on the player for '{pair.Key}'.", this);
            }
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || menuMode)
        {
            return;
        }

        foreach (SkillDefinition definition in definitions.Values)
        {
            Key key = definition.ActivationKey;
            if (key != Key.None && keyboard[key].wasPressedThisFrame)
            {
                TryUse(definition.SkillId);
            }
        }
    }

    public const int MaxTier = 3;

    // Tier 1 until the skill tree raises it (tiers 2 and 3 cost skill points).
    public int GetTier(string skillId)
    {
        return tiers.TryGetValue(skillId, out int tier) ? tier : 1;
    }

    public void SetTier(string skillId, int tier)
    {
        if (definitions.ContainsKey(skillId))
        {
            tiers[skillId] = Mathf.Clamp(tier, 1, MaxTier);
            Save();
            TierChanged?.Invoke(definitions[skillId], tiers[skillId]);
        }
    }

    // Skill points needed for the next tier; -1 if the skill is unknown or already at the top tier.
    public int GetUpgradeCost(string skillId)
    {
        if (!definitions.TryGetValue(skillId, out SkillDefinition definition) || GetTier(skillId) >= MaxTier)
        {
            return -1;
        }

        return definition.GetUpgradeCost(GetTier(skillId));
    }

    public bool CanUpgrade(string skillId)
    {
        int cost = GetUpgradeCost(skillId);
        return cost >= 0 && IsUnlocked(skillId) && SkillPointService.Balance >= cost;
    }

    // Spends the points, raises the tier and saves. False if locked, maxed or too few points.
    public bool TryUpgrade(string skillId)
    {
        if (!CanUpgrade(skillId) || !SkillPointService.TrySpend(GetUpgradeCost(skillId)))
        {
            return false;
        }

        SetTier(skillId, GetTier(skillId) + 1);
        return true;
    }

    public bool IsUnlocked(string skillId)
    {
        return unlocked.TryGetValue(skillId, out bool value) && value;
    }

    public float GetCooldownRemaining(string skillId)
    {
        return cooldownEnds.TryGetValue(skillId, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }

    public void Unlock(string skillId)
    {
        SetUnlocked(skillId, true);
    }

    public void Lock(string skillId)
    {
        SetUnlocked(skillId, false);
    }

    // Locked skills, skills on cooldown and skills the ability rejects all return false.
    public bool TryUse(string skillId)
    {
        if (!definitions.TryGetValue(skillId, out SkillDefinition definition) || !IsUnlocked(skillId))
        {
            return false;
        }

        if (GetCooldownRemaining(skillId) > 0f)
        {
            return false;
        }

        if (!abilities.TryGetValue(skillId, out SkillAbility ability) || !ability.isActiveAndEnabled)
        {
            return false;
        }

        // Without enough stamina the skill is refused before it starts; the failed spend tells the HUD to react.
        float staminaCost = definition.GetStaminaCost(GetTier(skillId));
        bool paid = stamina != null && staminaCost > 0f;
        if (paid && !stamina.CanSpend(staminaCost))
        {
            stamina.TrySpend(staminaCost);
            return false;
        }

        if (!ability.TryActivate())
        {
            return false;
        }

        if (paid)
        {
            stamina.TrySpend(staminaCost);
        }

        cooldownEnds[skillId] = Time.time + definition.Cooldown;
        Used?.Invoke(definition);
        return true;
    }

    void SetUnlocked(string skillId, bool value)
    {
        if (!definitions.TryGetValue(skillId, out SkillDefinition definition) || IsUnlocked(skillId) == value)
        {
            return;
        }

        unlocked[skillId] = value;
        if (!value)
        {
            cooldownEnds.Remove(skillId);
        }

        if (abilities.TryGetValue(skillId, out SkillAbility ability))
        {
            ability.enabled = value;
        }

        Save();
        UnlockChanged?.Invoke(definition, value);
    }

    void Save()
    {
        SkillSaveData data = new();
        foreach (KeyValuePair<string, bool> pair in unlocked)
        {
            data.entries.Add(new SkillSaveEntry { skillId = pair.Key, unlocked = pair.Value, tier = GetTier(pair.Key) });
        }

        SkillSaveStorage.Save(data);
    }
}
