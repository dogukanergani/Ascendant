using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class LevelProgress
{
    public int level;
    public bool completed;
    public int stars;
    public float bestTime;
    public int bestCollected;
}

[Serializable]
sealed class ProgressData
{
    public List<LevelProgress> levels = new();
}

// Static entry point for level unlock state and stars, persisted to progress.json.
// Levels are 0-based; level 0 is always open, every other level opens once the previous one is completed.
public static class ProgressService
{
    public const int LevelCount = 5;
    public const int MaxStars = 3;

    static ProgressData data;

    static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

    public static event Action Changed;

    public static bool IsCompleted(int level)
    {
        return Find(level)?.completed ?? false;
    }

    public static bool IsUnlocked(int level)
    {
        if (level < 0 || level >= LevelCount)
        {
            return false;
        }

        return level == 0 || IsCompleted(level - 1);
    }

    public static int GetStars(int level)
    {
        return Find(level)?.stars ?? 0;
    }

    public static float GetBestTime(int level)
    {
        return Find(level)?.bestTime ?? 0f;
    }

    // First level that is open but not completed; the last level once everything is done.
    public static int GetNextLevel()
    {
        for (int i = 0; i < LevelCount; i++)
        {
            if (IsUnlocked(i) && !IsCompleted(i))
            {
                return i;
            }
        }

        return LevelCount - 1;
    }

    public static void CompleteLevel(int level, int stars, float time, int collected)
    {
        if (level < 0 || level >= LevelCount)
        {
            return;
        }

        LevelProgress entry = Find(level);
        if (entry == null)
        {
            entry = new LevelProgress { level = level };
            Load().levels.Add(entry);
        }

        bool firstClear = !entry.completed;
        entry.completed = true;
        entry.stars = Mathf.Max(entry.stars, Mathf.Clamp(stars, 0, MaxStars));
        entry.bestTime = firstClear || entry.bestTime <= 0f ? time : Mathf.Min(entry.bestTime, time);
        entry.bestCollected = Mathf.Max(entry.bestCollected, collected);
        Save();
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        data = new ProgressData();
        Save();
        Changed?.Invoke();
    }

    static LevelProgress Find(int level)
    {
        return Load().levels.Find(e => e.level == level);
    }

    static ProgressData Load()
    {
        if (data != null)
        {
            return data;
        }

        data = new ProgressData();
        try
        {
            if (File.Exists(FilePath))
            {
                data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(FilePath)) ?? new ProgressData();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ProgressService: could not read progress.json ({e.Message}); starting fresh.");
            data = new ProgressData();
        }

        return data;
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Load(), true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ProgressService: could not write progress.json ({e.Message}).");
        }
    }

    // Domain reload may be disabled; make sure stale state never leaks into a new play session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        data = null;
        Changed = null;
    }
}
