using UnityEngine;

// Turns a skill's base damage range into one concrete hit: random base in the range, a spread multiplier and a
// critical chance, so numbers differ from hit to hit.
public static class DamageRoll
{
    public const float SpreadMin = 0.85f;
    public const float SpreadMax = 1.2f;
    public const float CritChance = 0.12f;
    public const float CritMultiplier = 1.6f;

    public static DamageInfo Roll(Vector2Int baseRange, float multiplier, Vector2 knockback, DamageSource source)
    {
        float baseDamage = Random.Range(baseRange.x, baseRange.y + 1) * multiplier;
        bool crit = Random.value < CritChance;
        float value = baseDamage * Random.Range(SpreadMin, SpreadMax) * (crit ? CritMultiplier : 1f);
        return new DamageInfo(Mathf.Max(1, Mathf.RoundToInt(value)), knockback, crit, source);
    }
}
