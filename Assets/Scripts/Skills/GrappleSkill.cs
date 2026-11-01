using System.Collections.Generic;
using UnityEngine;

public sealed class GrappleSkill : SkillAbility
{
    // Only colliders on this layer can be hooked.
    [SerializeField] string anchorLayerName = "GrappleAnchor";
    // Anything on these layers between the player and an anchor blocks the hook.
    [SerializeField] LayerMask sightBlockerMask = ~0;
    [SerializeField] float range = 8f;
    [SerializeField] float pullSpeed = 16f;
    [SerializeField] float arriveDistance = 0.6f;
    [SerializeField] float maxDuration = 1.2f;

    readonly List<RaycastHit2D> sightHits = new();

    int anchorMask;
    ContactFilter2D sightFilter;
    Vector2 anchor;
    float timeLeft;
    bool hooked;

    // Ticks after Run (0), Jump (10) and Wall (20) so the pull overrides them; Dash (30) can still interrupt it.
    public override int Priority => 25;

    public bool IsHooked => hooked;

    protected override void OnInitialize()
    {
        int layer = LayerMask.NameToLayer(anchorLayerName);
        if (layer < 0)
        {
            Debug.LogError($"GrappleSkill: layer '{anchorLayerName}' does not exist, the grapple cannot hook anything.", this);
            anchorMask = 0;
        }
        else
        {
            anchorMask = 1 << layer;
        }

        sightFilter = new ContactFilter2D { useLayerMask = true, layerMask = sightBlockerMask, useTriggers = false };
    }

    void OnDisable()
    {
        hooked = false;
    }

    public override bool TryActivate()
    {
        if (hooked || anchorMask == 0)
        {
            return false;
        }

        Vector2 origin = Player.Body.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, anchorMask);

        float bestDistance = float.PositiveInfinity;
        Vector2 bestPoint = origin;
        for (int i = 0; i < hits.Length; i++)
        {
            Vector2 point = hits[i].ClosestPoint(origin);
            float distance = Vector2.Distance(origin, point);
            if (distance < bestDistance && HasLineOfSight(origin, point, hits[i]))
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

    // The target anchor and the player's own collider never block; any other solid does.
    bool HasLineOfSight(Vector2 origin, Vector2 point, Collider2D target)
    {
        sightHits.Clear();
        Physics2D.Linecast(origin, point, sightFilter, sightHits);
        for (int i = 0; i < sightHits.Count; i++)
        {
            Collider2D blocker = sightHits[i].collider;
            if (blocker != target && blocker.attachedRigidbody != Player.Body)
            {
                return false;
            }
        }

        return true;
    }
}
