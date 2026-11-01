using System;
using System.IO;
using UnityEngine;

[Serializable]
sealed class SkillPointData
{
    public int points;
}

// Static skill point balance, persisted to skillpoints.json. Enemies pay out their EnemyStats.skillPointReward when
// they die; a level can call AddPoints directly for a finish bonus.
public static class SkillPointService
{
    static SkillPointData data;

    static string FilePath => Path.Combine(Application.persistentDataPath, "skillpoints.json");

    public static int Balance
    {
        get
        {
            Ensure();
            return data.points;
        }
    }

    // Raised with the new balance after any change.
    public static event Action<int> Changed;
    // Raised with the amount whenever points are earned (not when spent).
    public static event Action<int> Gained;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        data = null;
        Changed = null;
        Gained = null;
        Health.AnyDamaged -= OnAnyDamaged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Health.AnyDamaged -= OnAnyDamaged;
        Health.AnyDamaged += OnAnyDamaged;
    }

    public static void AddPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Ensure();
        data.points += amount;
        Save();
        Changed?.Invoke(data.points);
        Gained?.Invoke(amount);
    }

    public static bool TrySpend(int amount)
    {
        Ensure();
        if (amount < 0 || data.points < amount)
        {
            return false;
        }

        data.points -= amount;
        Save();
        Changed?.Invoke(data.points);
        return true;
    }

    // The hit that brings an enemy's health to zero is the only one that can reach here with IsDead set,
    // because a dead Health refuses further damage.
    static void OnAnyDamaged(Health health, DamageInfo info)
    {
        if (health.IsDead && health.TryGetComponent(out EnemyAI enemy) && enemy.Stats != null)
        {
            AddPoints(enemy.Stats.skillPointReward);
        }
    }

    static void Ensure()
    {
        if (data != null)
        {
            return;
        }

        data = new SkillPointData();
        try
        {
            if (File.Exists(FilePath))
            {
                data = JsonUtility.FromJson<SkillPointData>(File.ReadAllText(FilePath)) ?? data;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SkillPointService: could not read {FilePath}. {e.Message}");
        }
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"SkillPointService: could not write {FilePath}. {e.Message}");
        }
    }
}
