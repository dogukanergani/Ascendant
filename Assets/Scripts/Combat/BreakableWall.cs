using UnityEngine;

// A cracked wall: solid until a bomb blast reaches it, then it crumbles and opens the way.
public sealed class BreakableWall : MonoBehaviour
{
    [SerializeField] Color debrisFrom = new(0.85f, 0.8f, 0.7f);
    [SerializeField] Color debrisTo = new(0.35f, 0.3f, 0.28f);

    bool broken;

    public void Break()
    {
        if (broken)
        {
            return;
        }

        broken = true;
        SpellExplosion.Spawn(transform.position, 1.4f, 0.3f, debrisFrom, debrisTo);
        Destroy(gameObject);
    }
}
