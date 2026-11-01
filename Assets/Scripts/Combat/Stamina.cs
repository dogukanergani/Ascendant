using System;
using UnityEngine;

// Spend-and-refill resource for skills. Regeneration starts regenDelay seconds after the last spend.
public sealed class Stamina : MonoBehaviour
{
    [SerializeField] float max = 100f;
    [SerializeField] float regenDelay = 1f;
    [SerializeField] float regenPerSecond = 15f;

    float current;
    float regenStartsAt;

    public event Action<float, float> Changed;
    // Raised when a spend was refused, so the HUD can give feedback.
    public event Action Insufficient;

    public float Current => current;
    public float Max => max;

    void Awake()
    {
        current = max;
    }

    void Update()
    {
        if (current >= max || Time.time < regenStartsAt)
        {
            return;
        }

        current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
        Changed?.Invoke(current, max);
    }

    public bool CanSpend(float amount)
    {
        return current >= amount;
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (current < amount)
        {
            Insufficient?.Invoke();
            return false;
        }

        current -= amount;
        regenStartsAt = Time.time + regenDelay;
        Changed?.Invoke(current, max);
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f || current >= max)
        {
            return;
        }

        current = Mathf.Min(max, current + amount);
        Changed?.Invoke(current, max);
    }
}
