using System.Collections.Generic;
using UnityEngine;

// A secret the lantern uncovers. The children are the secret itself:
//  - default: hidden (inactive) until a lantern of requiredTier or higher lights this object, then switched on;
//  - dissolve: an illusory wall that is visible and solid until the lantern lights it, then it vanishes.
// Revealing is permanent for the scene.
public sealed class HiddenReveal : MonoBehaviour
{
    static readonly List<HiddenReveal> all = new();

    [SerializeField] int requiredTier = 1;
    [SerializeField] bool dissolve;
    [SerializeField] Color puffFrom = new(1f, 0.95f, 0.6f);
    [SerializeField] Color puffTo = new(1f, 0.7f, 0.2f);

    bool revealed;

    public static IReadOnlyList<HiddenReveal> All => all;

    void Awake()
    {
        if (!dissolve)
        {
            SetChildren(false);
        }
    }

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    // Called by the lantern for objects inside its radius. Returns true the moment it is revealed.
    public bool TryReveal(int lanternTier)
    {
        if (revealed || lanternTier < requiredTier)
        {
            return false;
        }

        revealed = true;
        SpellExplosion.Spawn(transform.position, 1.2f, 0.35f, puffFrom, puffTo);
        SetChildren(!dissolve);
        return true;
    }

    void SetChildren(bool active)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(active);
        }
    }
}
