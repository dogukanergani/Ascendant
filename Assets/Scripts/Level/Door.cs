using UnityEngine;

// A room door: closed it blocks movement (collider on, closed sprite), open it is walk-through.
// Doors start open; the room that owns them closes and opens them.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Door : MonoBehaviour
{
    [SerializeField] SpriteRenderer[] renderers = System.Array.Empty<SpriteRenderer>();
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openSprite;
    [SerializeField] bool startClosed;

    BoxCollider2D blocker;

    public bool IsClosed { get; private set; }

    void Awake()
    {
        blocker = GetComponent<BoxCollider2D>();
        SetClosed(startClosed);
    }

    public void Close() => SetClosed(true);

    public void Open() => SetClosed(false);

    void SetClosed(bool closed)
    {
        IsClosed = closed;
        blocker.enabled = closed;
        foreach (SpriteRenderer r in renderers)
        {
            r.sprite = closed ? closedSprite : openSprite;
        }
    }
}
