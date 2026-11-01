using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class SkillSaveEntry
{
    public string skillId;
    public bool unlocked;
}

[Serializable]
public sealed class SkillSaveData
{
    public List<SkillSaveEntry> entries = new();
}

public static class SkillSaveStorage
{
    const string FileName = "skills.json";

    static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SkillSaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                SkillSaveData data = JsonUtility.FromJson<SkillSaveData>(File.ReadAllText(FilePath));
                if (data != null)
                {
                    return data;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SkillSaveStorage: could not read {FilePath}. {e.Message}");
        }

        return new SkillSaveData();
    }

    public static void Save(SkillSaveData data)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"SkillSaveStorage: could not write {FilePath}. {e.Message}");
        }
    }
}
