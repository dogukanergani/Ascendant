using UnityEngine;

// Tosses a bomb a short way in the facing direction. The blast radius and damage come from the skill tier;
// tier 3 bombs also detonate other bombs caught in their blast.
public sealed class BombSkill : SkillAbility
{
    [SerializeField] Bomb bombPrefab;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);
    [SerializeField] float spawnDistance = 0.8f;
    [SerializeField] LayerMask wallMask = 1 << 11;
    // Blast radius per tier (index 0 = tier 1); the last entry is used for higher tiers.
    [SerializeField] float[] tierRadius = { 2f, 2.6f, 3.2f };

    SkillManager manager;

    public override int Priority => 45;

    public override bool TryActivate()
    {
        if (bombPrefab == null || !Player.CanControl)
        {
            return false;
        }

        Vector2 release = (Vector2)Player.transform.position + spawnOffset;
        Vector2 origin = release + Player.Facing * spawnDistance;
        if (Physics2D.Linecast(release, origin, wallMask))
        {
            origin = release;
        }

        if (manager == null)
        {
            manager = FindFirstObjectByType<SkillManager>();
        }

        int tier = manager != null && Definition != null ? manager.GetTier(Definition.SkillId) : 1;
        Vector2Int range = Definition != null ? Definition.GetDamageRange(tier) : new Vector2Int(1, 1);
        float radius = tierRadius[Mathf.Clamp(tier - 1, 0, tierRadius.Length - 1)];
        Bomb bomb = Instantiate(bombPrefab, origin, Quaternion.identity);
        bomb.Launch(Player.Facing, range, radius, tier >= 3);
        return true;
    }
}
