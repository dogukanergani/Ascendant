using UnityEngine;

// Rolls loot when the Health on this object dies.
[RequireComponent(typeof(Health))]
public sealed class DropOnDeath : MonoBehaviour
{
    [SerializeField] GameObject heartPickup;
    [SerializeField, Range(0f, 1f)] float heartChance = 0.3f;
    [SerializeField] GameObject staminaOrb;
    [SerializeField, Range(0f, 1f)] float staminaChance = 0.4f;
    [SerializeField] float scatterRadius = 0.4f;

    Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }
    }

    void OnDied()
    {
        Roll(heartPickup, heartChance);
        Roll(staminaOrb, staminaChance);
    }

    void Roll(GameObject prefab, float chance)
    {
        if (prefab == null || Random.value >= chance)
        {
            return;
        }

        Vector2 offset = Random.insideUnitCircle * scatterRadius;
        Instantiate(prefab, transform.position + (Vector3)offset, Quaternion.identity);
    }
}
