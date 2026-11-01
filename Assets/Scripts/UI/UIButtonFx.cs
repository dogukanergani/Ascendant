using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover / press / keyboard-focus feedback for a button: grows a little when pointed at or selected, shrinks when
// pressed. The sprite swap itself is the Button's own SpriteSwap transition; this adds the scale and the optional
// selection arrows.
[RequireComponent(typeof(Selectable))]
public sealed class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] float hoverScale = 1.06f;
    [SerializeField] float pressedScale = 0.96f;
    [SerializeField] float speed = 22f;
    // Pointing at the button also selects it, so mouse and keyboard/gamepad share one highlight.
    [SerializeField] bool selectOnHover = true;
    [SerializeField] GameObject selectionMarker;

    Selectable selectable;
    Vector3 baseScale = Vector3.one;
    bool hovered;
    bool pressed;
    bool selected;

    public void Configure(float hover, float press)
    {
        hoverScale = hover;
        pressedScale = press;
    }

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
        RefreshMarker();
    }

    void OnDisable()
    {
        hovered = pressed = selected = false;
        transform.localScale = baseScale;
        RefreshMarker();
    }

    void Update()
    {
        float target = 1f;
        if (selectable.IsInteractable())
        {
            target = pressed ? pressedScale : hovered || selected ? hoverScale : 1f;
        }

        Vector3 goal = baseScale * target;
        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (selectOnHover && selectable.IsInteractable() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = true;

    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        RefreshMarker();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        RefreshMarker();
    }

    void RefreshMarker()
    {
        if (selectionMarker != null)
        {
            selectionMarker.SetActive(selected && selectable != null && selectable.IsInteractable());
        }
    }
}
