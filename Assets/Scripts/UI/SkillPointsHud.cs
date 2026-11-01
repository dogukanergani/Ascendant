using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Skill point balance text with an optional "+N" toast whenever points are earned. Lives in the in-game HUD,
// on the level select screen and on the score screen. In the editor F9 adds 20 points for testing; only one
// instance per scene should have debugKey on.
public sealed class SkillPointsHud : MonoBehaviour
{
    [SerializeField] TMP_Text balanceText;
    [SerializeField] TMP_Text toastText;
    [SerializeField] bool debugKey = true;
    [SerializeField] float toastDuration = 1.2f;
    [SerializeField] float toastRise = 40f;

    int toastAmount;
    float toastTimer;
    Vector2 toastHome;

    void Awake()
    {
        if (toastText != null)
        {
            toastHome = toastText.rectTransform.anchoredPosition;
            toastText.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        SkillPointService.Changed += OnPointsChanged;
        SkillPointService.Gained += OnPointsGained;
        OnPointsChanged(SkillPointService.Balance);
    }

    void OnDisable()
    {
        SkillPointService.Changed -= OnPointsChanged;
        SkillPointService.Gained -= OnPointsGained;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (debugKey && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            SkillPointService.AddPoints(20);
        }
#endif

        UpdateToast();
    }

    void OnPointsChanged(int balance)
    {
        balanceText.text = $"Puan: {balance}";
    }

    // Gains within the toast's lifetime add up into one "+N".
    void OnPointsGained(int amount)
    {
        if (toastText == null)
        {
            return;
        }

        toastAmount = toastTimer > 0f ? toastAmount + amount : amount;
        toastTimer = toastDuration;
        toastText.text = $"+{toastAmount}";
        toastText.color = new Color(toastText.color.r, toastText.color.g, toastText.color.b, 1f);
        toastText.gameObject.SetActive(true);
    }

    void UpdateToast()
    {
        if (toastTimer <= 0f)
        {
            return;
        }

        toastTimer -= Time.unscaledDeltaTime;
        if (toastTimer <= 0f)
        {
            toastText.gameObject.SetActive(false);
            return;
        }

        float t = 1f - toastTimer / toastDuration;
        toastText.rectTransform.anchoredPosition = toastHome + new Vector2(0f, toastRise * t);
        Color color = toastText.color;
        color.a = Mathf.Clamp01(toastTimer / (toastDuration * 0.4f));
        toastText.color = color;
    }
}
