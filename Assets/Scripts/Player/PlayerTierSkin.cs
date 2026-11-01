using UnityEngine;

// Swaps the player's animation set when the staff skill changes tier (Mage 1-3 look). Each tier entry is an
// override of the base controller; a missing entry falls back to the nearest lower tier, or the base controller.
[RequireComponent(typeof(PlayerAnimationDriver))]
public sealed class PlayerTierSkin : MonoBehaviour
{
    [SerializeField] string skillId = "staff";
    [SerializeField] Animator animator;
    // Index 0 = tier 1 ... index 2 = tier 3.
    [SerializeField] RuntimeAnimatorController[] tierControllers = new RuntimeAnimatorController[SkillManager.MaxTier];

    SkillManager manager;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void OnEnable()
    {
        manager = FindFirstObjectByType<SkillManager>();
        if (manager == null)
        {
            return;
        }

        manager.TierChanged += OnTierChanged;
        Apply(manager.GetTier(skillId));
    }

    void OnDisable()
    {
        if (manager != null)
        {
            manager.TierChanged -= OnTierChanged;
        }
    }

    void OnTierChanged(SkillDefinition definition, int tier)
    {
        if (definition != null && definition.SkillId == skillId)
        {
            Apply(tier);
        }
    }

    void Apply(int tier)
    {
        if (animator == null)
        {
            return;
        }

        for (int i = Mathf.Min(tier, tierControllers.Length) - 1; i >= 0; i--)
        {
            if (tierControllers[i] != null)
            {
                // Keeps the playing state when only the clips change.
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                int hash = state.fullPathHash;
                float time = state.normalizedTime;
                animator.runtimeAnimatorController = tierControllers[i];
                if (animator.isActiveAndEnabled && animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, time);
                }

                return;
            }
        }
    }
}
