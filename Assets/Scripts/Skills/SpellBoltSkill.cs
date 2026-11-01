using UnityEngine;

public sealed class SpellBoltSkill : SkillAbility
{
    [SerializeField] SpellBolt boltPrefab;
    [SerializeField] float spawnDistance = 0.6f;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);
    [SerializeField] LayerMask wallMask = 1 << 11;

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
        bolt.Launch(Player.Facing, damage, release);
        return true;
    }
}
