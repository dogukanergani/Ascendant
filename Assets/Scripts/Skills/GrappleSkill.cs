using UnityEngine;

public sealed class GrappleSkill : SkillAbility
{
    // Only colliders on this mask can be hooked.
    [SerializeField] LayerMask anchorMask = ~0;
    [SerializeField] float range = 8f;
    [SerializeField] float pullSpeed = 16f;
    [SerializeField] float arriveDistance = 0.6f;
    [SerializeField] float maxDuration = 1.2f;

    Vector2 anchor;
    float timeLeft;
    bool hooked;

    // Ticks after Run (0), Jump (10) and Wall (20) so the pull overrides them; Dash (30) can still interrupt it.
    public override int Priority => 25;

    public bool IsHooked => hooked;

    void OnDisable()
    {
        hooked = false;
    }

    public override bool TryActivate()
    {
        if (hooked)
        {
            return false;
        }

        Vector2 origin = Player.Body.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, anchorMask);

        float bestDistance = float.PositiveInfinity;
        Vector2 bestPoint = origin;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].attachedRigidbody == Player.Body)
            {
                continue;
            }

            Vector2 point = hits[i].ClosestPoint(origin);
            float distance = Vector2.Distance(origin, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = point;
            }
        }

        if (float.IsPositiveInfinity(bestDistance))
        {
            return false;
        }

        anchor = bestPoint;
        timeLeft = maxDuration;
        hooked = true;
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!hooked)
        {
            return;
        }

        timeLeft -= deltaTime;
        Vector2 toAnchor = anchor - Player.Body.position;
        if (timeLeft <= 0f || toAnchor.magnitude <= arriveDistance)
        {
            hooked = false;
            return;
        }

        Player.Body.gravityScale = 0f;
        Player.Velocity = toAnchor.normalized * pullSpeed;
    }
}
