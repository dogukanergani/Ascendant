using System.Collections.Generic;
using UnityEngine;

public sealed class GrappleSkill : SkillAbility
{
    const float PixelsPerUnit = 32f;

    // Only colliders on this layer can be hooked.
    [SerializeField] string anchorLayerName = "GrappleAnchor";
    // Anything on these layers between the player and an anchor blocks the hook.
    [SerializeField] LayerMask sightBlockerMask = ~0;
    [SerializeField] float range = 8f;
    // Half-angle of the cone in front of the player in which anchors can be hooked.
    [SerializeField] float aimHalfAngle = 45f;
    [SerializeField] float pullSpeed = 16f;
    [SerializeField] float arriveDistance = 0.6f;
    [SerializeField] float maxDuration = 1.2f;

    [Header("Visuals")]
    // The pull starts once the rope has reached the anchor.
    [SerializeField] float throwTime = 0.12f;
    [SerializeField] float ropeWidth = 2f / PixelsPerUnit;
    [SerializeField] Color ropeColor = new Color(0.45f, 0.3f, 0.16f);
    [SerializeField] Color highlightColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] Color targetColor = new Color(1f, 0.85f, 0.25f, 0.95f);
    [SerializeField] int ropeSortingOrder = 5;
    [SerializeField] int highlightSortingOrder = 4;
    [SerializeField] Vector2 ropeOriginOffset = new Vector2(0f, 0.15f);

    readonly List<RaycastHit2D> sightHits = new();
    readonly List<SpriteRenderer> rings = new();

    int anchorMask;
    ContactFilter2D sightFilter;
    Vector2 anchor;
    float timeLeft;
    float throwLeft;
    bool hooked;

    LineRenderer rope;
    SpriteRenderer hookTip;
    Sprite ringSprite;

    // Ticks after Move (0) so the pull overrides it; Dash (30) can still interrupt it.
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
        BuildVisuals();
    }

    void OnDisable()
    {
        hooked = false;
        HideVisuals();
    }

    public override bool TryActivate()
    {
        if (hooked || anchorMask == 0)
        {
            return false;
        }

        if (!FindBestAnchor(null, out Vector2 point))
        {
            return false;
        }

        anchor = point;
        timeLeft = maxDuration;
        throwLeft = throwTime;
        hooked = true;
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!hooked)
        {
            return;
        }

        // While the rope is still flying out the player keeps moving normally.
        if (throwLeft > 0f)
        {
            throwLeft -= deltaTime;
            return;
        }

        timeLeft -= deltaTime;
        Vector2 toAnchor = anchor - Player.Body.position;
        if (timeLeft <= 0f || toAnchor.magnitude <= arriveDistance)
        {
            hooked = false;
            Player.Velocity = Vector2.zero;
            return;
        }

        Player.Velocity = toAnchor.normalized * pullSpeed;
    }

    void LateUpdate()
    {
        if (hooked)
        {
            ShowRope();
            SetRingsActive(0);
        }
        else
        {
            HideRope();
            ShowHighlights();
        }
    }

    // Picks the nearest hookable anchor with a clear line. When valid is given, every such anchor is added to it.
    bool FindBestAnchor(List<Vector2> valid, out Vector2 bestPoint)
    {
        Vector2 origin = Player.Body.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, anchorMask);
        float minDot = Mathf.Cos(aimHalfAngle * Mathf.Deg2Rad);

        float bestDistance = float.PositiveInfinity;
        bestPoint = origin;
        for (int i = 0; i < hits.Length; i++)
        {
            Vector2 point = hits[i].ClosestPoint(origin);
            Vector2 toPoint = point - origin;
            if (toPoint.sqrMagnitude > 0.0001f && Vector2.Dot(toPoint.normalized, Player.Facing) < minDot)
            {
                continue;
            }

            if (!HasLineOfSight(origin, point, hits[i]))
            {
                continue;
            }

            valid?.Add(point);
            float distance = Vector2.Distance(origin, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = point;
            }
        }

        return !float.IsPositiveInfinity(bestDistance);
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

    void ShowRope()
    {
        Vector2 start = Player.Body.position + ropeOriginOffset;
        float t = throwTime > 0f ? Mathf.Clamp01(1f - Mathf.Max(throwLeft, 0f) / throwTime) : 1f;
        Vector2 tip = Vector2.Lerp(start, anchor, t);

        rope.enabled = true;
        rope.SetPosition(0, start);
        rope.SetPosition(1, tip);

        hookTip.enabled = true;
        hookTip.transform.position = tip;
        Vector2 dir = anchor - start;
        hookTip.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void HideRope()
    {
        rope.enabled = false;
        hookTip.enabled = false;
    }

    void HideVisuals()
    {
        if (rope != null && hookTip != null)
        {
            HideRope();
            SetRingsActive(0);
        }
    }

    readonly List<Vector2> validPoints = new();

    void ShowHighlights()
    {
        validPoints.Clear();
        if (anchorMask == 0 || !FindBestAnchor(validPoints, out Vector2 target))
        {
            SetRingsActive(0);
            return;
        }

        SetRingsActive(validPoints.Count);
        for (int i = 0; i < validPoints.Count; i++)
        {
            SpriteRenderer ring = rings[i];
            ring.transform.position = validPoints[i];
            ring.color = (validPoints[i] - target).sqrMagnitude < 0.0001f ? targetColor : highlightColor;
        }
    }

    void SetRingsActive(int count)
    {
        while (rings.Count < count)
        {
            rings.Add(CreateRing());
        }

        for (int i = 0; i < rings.Count; i++)
        {
            if (rings[i] == null)
            {
                continue;
            }

            rings[i].gameObject.SetActive(i < count);
        }
    }

    void BuildVisuals()
    {
        Material material = new Material(Shader.Find("Sprites/Default"));

        GameObject ropeObject = new GameObject("GrappleRope");
        rope = ropeObject.AddComponent<LineRenderer>();
        rope.sharedMaterial = material;
        rope.positionCount = 2;
        rope.useWorldSpace = true;
        rope.widthMultiplier = ropeWidth;
        rope.startColor = ropeColor;
        rope.endColor = ropeColor;
        rope.sortingOrder = ropeSortingOrder;
        rope.numCapVertices = 0;
        rope.enabled = false;

        ringSprite = MakeRingSprite(8);

        hookTip = new GameObject("GrappleHook").AddComponent<SpriteRenderer>();
        hookTip.sprite = MakeHookSprite();
        hookTip.sortingOrder = ropeSortingOrder + 1;
        hookTip.enabled = false;
    }

    SpriteRenderer CreateRing()
    {
        SpriteRenderer ring = new GameObject("GrappleAnchorRing").AddComponent<SpriteRenderer>();
        ring.sprite = ringSprite;
        ring.sortingOrder = highlightSortingOrder;
        return ring;
    }

    void OnDestroy()
    {
        if (rope != null) Destroy(rope.gameObject);
        if (hookTip != null) Destroy(hookTip.gameObject);
        foreach (SpriteRenderer ring in rings)
        {
            if (ring != null) Destroy(ring.gameObject);
        }
    }

    // Pixel-art sprites are generated at runtime so no extra assets are needed.
    static Sprite MakeRingSprite(int radius)
    {
        int size = radius * 2 + 1;
        Texture2D tex = NewTexture(size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                tex.SetPixel(x, y, d <= radius + 0.3f && d >= radius - 1.2f ? Color.white : Color.clear);
            }
        }

        return FinishSprite(tex);
    }

    // A small grey arrowhead pointing along +X.
    static Sprite MakeHookSprite()
    {
        Texture2D tex = NewTexture(5);
        Color grey = new Color(0.75f, 0.75f, 0.78f);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                tex.SetPixel(x, y, Mathf.Abs(y - 2) <= (4 - x) / 2 ? grey : Color.clear);
            }
        }

        return FinishSprite(tex);
    }

    static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
    }

    static Sprite FinishSprite(Texture2D tex)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }
}
