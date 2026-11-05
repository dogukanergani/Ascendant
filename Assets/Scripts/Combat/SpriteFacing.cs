using UnityEngine;

// The DungeonTileset II characters are drawn facing right only: mirrors the sprite from the Animator's MoveX
// (EnemyAI writes its facing there), so the same clips serve both sides.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFacing : MonoBehaviour
{
    static readonly int MoveXHash = Animator.StringToHash("MoveX");

    [SerializeField] float deadZone = 0.05f;

    SpriteRenderer spriteRenderer;
    Animator animator;
    bool hasMoveX;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                hasMoveX |= p.nameHash == MoveXHash;
            }
        }
    }

    void LateUpdate()
    {
        if (!hasMoveX) return;
        float x = animator.GetFloat(MoveXHash);
        if (Mathf.Abs(x) > deadZone)
        {
            spriteRenderer.flipX = x < 0f;
        }
    }
}
