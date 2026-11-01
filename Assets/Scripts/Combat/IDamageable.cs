using UnityEngine;

public interface IDamageable
{
    // Returns false when the hit was ignored (invulnerable or already dead).
    bool Damage(int amount, Vector2 knockback);
}
