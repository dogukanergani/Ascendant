using UnityEngine;

public sealed class SpellBoltSkill : SkillAbility
{
    [SerializeField] SpellBolt boltPrefab;
    [SerializeField] float spawnDistance = 0.6f;
    [SerializeField] Vector2 spawnOffset = new(0f, 0.3f);

    public override int Priority => 40;

    public override bool TryActivate()
    {
        if (boltPrefab == null)
        {
            return false;
        }

        Vector2 origin = (Vector2)Player.transform.position + spawnOffset + Player.Facing * spawnDistance;
        SpellBolt bolt = Instantiate(boltPrefab, origin, Quaternion.identity);
        bolt.Launch(Player.Facing);
        return true;
    }
}
