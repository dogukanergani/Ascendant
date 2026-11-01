using UnityEngine;

public sealed class SpellBoltSkill : SkillAbility
{
    [SerializeField] SpellBolt boltPrefab;
    [SerializeField] float spawnDistance = 0.6f;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);
    [SerializeField] LayerMask wallMask = 1 << 11;
    // Blast radius per tier (index 0 = tier 1); the last entry is used for higher tiers.
    [SerializeField] float[] tierSplashRadius = { 1.5f, 2.0f, 2.6f };

    SkillManager manager;

    public override int Priority => 40;

    public override bool TryActivate()
    {
        if (boltPrefab == null)
        {
            return false;
        }

        Vector2 release = (Vector2)Player.transform.position + spawnOffset;
        Vector2 origin = release + Player.Facing * spawnDistance;
        // A wall right in front of the player: spawn at the player instead of behind the wall.
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
        DamageInfo damage = DamageRoll.Roll(range, 1f, Vector2.zero, DamageSource.Spell);

        SpellBolt bolt = Instantiate(boltPrefab, origin, Quaternion.identity);
        float radius = tierSplashRadius != null && tierSplashRadius.Length > 0
            ? tierSplashRadius[Mathf.Clamp(tier - 1, 0, tierSplashRadius.Length - 1)]
            : -1f;
        bolt.Launch(Player.Facing, damage, release, radius);
        return true;
    }
}
