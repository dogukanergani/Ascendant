using System.Collections;
using UnityEngine;

// The staff in the wizard's hand (DungeonTileset II has no attack frames): held upright beside the body and mirrored
// with the sprite, swung in an arc toward the facing direction on every staff attack, and tilted back during a dash.
[RequireComponent(typeof(PlayerAnimationDriver))]
public sealed class StaffVisual : MonoBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] SpriteRenderer staff;
    // Hand position for the right-facing pose, relative to the body (feet origin), in world units.
    [SerializeField] Vector2 handOffset = new(0.34f, 0.06f);
    [SerializeField] float restAngle = -8f;
    [SerializeField] float swingFrom = 70f;
    [SerializeField] float swingTo = -110f;
    [SerializeField] float swingDuration = 0.16f;
    [SerializeField] float lunge = 0.18f;
    [SerializeField] float bobAmount = 0.03f;

    PlayerAnimationDriver driver;
    PlayerController2D player;
    DashSkill dash;
    Coroutine swinging;
    float swingAngle;
    float swingPush;
    bool swingActive;

    void Awake()
    {
        driver = GetComponent<PlayerAnimationDriver>();
        player = GetComponent<PlayerController2D>();
        dash = GetComponent<DashSkill>();
    }

    void OnEnable()
    {
        driver.AttackPlayed += OnAttack;
    }

    void OnDisable()
    {
        driver.AttackPlayed -= OnAttack;
    }

    void LateUpdate()
    {
        if (body == null || staff == null) return;
        float side = body.flipX ? -1f : 1f;
        Vector2 facing = player != null ? player.Facing : Vector2.right;
        float bob = Mathf.Sin(Time.time * 6f) * bobAmount;
        float angle = swingActive ? swingAngle : restAngle;
        if (dash != null && dash.IsDashing) angle = restAngle + 35f;

        Vector2 push = swingActive ? facing.normalized * swingPush : Vector2.zero;
        staff.transform.localPosition = new Vector3(handOffset.x * side + push.x, handOffset.y + bob + push.y * 0.5f, 0f);
        staff.transform.localRotation = Quaternion.Euler(0f, 0f, angle * side);
        staff.flipX = body.flipX;
        // Behind the body when walking away from the camera, in front otherwise.
        staff.sortingOrder = body.sortingOrder + (facing.y > 0.5f ? -1 : 1);
    }

    void OnAttack()
    {
        if (swinging != null) StopCoroutine(swinging);
        swinging = StartCoroutine(Swing());
    }

    IEnumerator Swing()
    {
        swingActive = true;
        for (float t = 0f; t < swingDuration; t += Time.deltaTime)
        {
            float k = t / swingDuration;
            swingAngle = Mathf.Lerp(swingFrom, swingTo, 1f - (1f - k) * (1f - k));
            swingPush = Mathf.Sin(k * Mathf.PI) * lunge;
            yield return null;
        }

        // Ease back to the rest pose.
        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
        {
            swingAngle = Mathf.Lerp(swingTo, restAngle, t / 0.1f);
            swingPush = 0f;
            yield return null;
        }

        swingActive = false;
        swinging = null;
    }
}
