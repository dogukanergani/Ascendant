using UnityEngine;

// Counts level time in scaled seconds; Stop() freezes it when the level ends.
public sealed class LevelTimer : MonoBehaviour
{
    public float Elapsed { get; private set; }
    public bool Running { get; private set; } = true;

    void Update()
    {
        if (Running)
        {
            Elapsed += Time.deltaTime;
        }
    }

    public void Stop() => Running = false;

    public void ResetTimer()
    {
        Elapsed = 0f;
        Running = true;
    }

    public static string Format(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
