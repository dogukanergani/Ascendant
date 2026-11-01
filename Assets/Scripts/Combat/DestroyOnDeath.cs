using UnityEngine;

// Removes the object when its Health dies. Placeholder for targets that have no death behaviour of their own.
[RequireComponent(typeof(Health))]
public sealed class DestroyOnDeath : MonoBehaviour
{
    [SerializeField] float delay = 0.05f;

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
        Destroy(gameObject, delay);
    }
}
