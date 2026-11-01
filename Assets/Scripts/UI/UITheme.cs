using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The one place that defines how the UI looks: palette, font, sizes and the generated 9-slice pixel-art sprites.
// UIBuilder bakes these values into the prefabs and scenes (Tools/UI/Rebuild All after changing anything here);
// a few runtime components (bars, stars, key labels) also read the theme live.
[CreateAssetMenu(menuName = "Ascendant/UI Theme", fileName = "UITheme")]
public sealed class UITheme : ScriptableObject
{
    [System.Serializable]
    public struct ButtonSprites
    {
        public Sprite normal;
        public Sprite hover;
        public Sprite pressed;
        public Sprite disabled;
    }

    [Header("Colors")]
    public Color background = new(0.07f, 0.06f, 0.13f, 1f);
    public Color backgroundLight = new(0.12f, 0.1f, 0.22f, 1f);
    public Color panel = new(0.16f, 0.14f, 0.29f, 1f);
    public Color panelDark = new(0.1f, 0.09f, 0.19f, 1f);
    public Color border = new(0.04f, 0.035f, 0.09f, 1f);
    public Color borderLight = new(0.36f, 0.32f, 0.63f, 1f);
    public Color accent = new(0.96f, 0.72f, 0.24f, 1f);
    public Color accentDark = new(0.78f, 0.4f, 0.14f, 1f);
    public Color health = new(0.86f, 0.23f, 0.29f, 1f);
    public Color healthDark = new(0.27f, 0.09f, 0.14f, 1f);
    public Color stamina = new(0.25f, 0.64f, 0.95f, 1f);
    public Color staminaDark = new(0.08f, 0.17f, 0.3f, 1f);
    public Color staminaLow = new(0.93f, 0.3f, 0.28f, 1f);
    public Color disabled = new(0.35f, 0.34f, 0.46f, 1f);
    public Color disabledText = new(0.55f, 0.53f, 0.66f, 1f);
    public Color text = new(0.96f, 0.92f, 0.82f, 1f);
    public Color textDim = new(0.72f, 0.68f, 0.82f, 1f);
    public Color success = new(0.5f, 0.8f, 0.43f, 1f);
    public Color dim = new(0.03f, 0.02f, 0.07f, 0.78f);

    [Header("Font")]
    public TMP_FontAsset font;
    // Heavier cut of the same pixel font for titles and big numbers.
    public TMP_FontAsset fontBold;

    [Header("Sizes (font sizes are multiples of 12 so the pixel font stays sharp)")]
    // On-screen size of one art pixel at the 1920x1080 reference resolution.
    public int pixelScale = 3;
    public int fontSmall = 24;
    public int fontBody = 36;
    public int fontLarge = 48;
    public int fontTitle = 72;
    public int fontHuge = 120;
    // Distance kept between HUD elements and the screen edge.
    public int safeMargin = 48;
    public Vector2 buttonSize = new(520f, 96f);
    public Vector2 smallButtonSize = new(300f, 80f);
    public float hoverScale = 1.06f;
    public float pressedScale = 0.96f;
    public float lowStaminaFraction = 0.25f;

    [Header("Panels, slots and bars (9-slice)")]
    public Sprite panelSprite;
    public Sprite cardSprite;
    public Sprite cardSelectedSprite;
    public Sprite slotSprite;
    public Sprite slotSelectedSprite;
    public Sprite badgeSprite;
    public Sprite barFrameSprite;
    public Sprite barFillSprite;
    public Sprite whiteSprite;

    [Header("Buttons (9-slice)")]
    public ButtonSprites buttonDefault;
    public ButtonSprites buttonPrimary;

    [Header("Icons")]
    public Sprite heartFull;
    public Sprite heartHalf;
    public Sprite heartEmpty;
    public Sprite starFull;
    public Sprite starEmpty;
    public Sprite lockIcon;
    public Sprite coinIcon;
    public Sprite clockIcon;
    public Sprite pipFull;
    public Sprite pipEmpty;
    public Sprite arrowRight;
    public Sprite arrowLeft;
    public Sprite dashIcon;
    public Sprite staffIcon;
    public Sprite emberSprite;

    // Icon for skills whose SkillDefinition has none yet.
    public Sprite FallbackSkillIcon(string skillId)
    {
        return skillId switch
        {
            "dash" => dashIcon,
            "staff" => staffIcon,
            _ => null,
        };
    }

    // Short label for a skill's activation key on the HUD slot.
    public static string KeyLabel(Key key)
    {
        switch (key)
        {
            case Key.None:
                return string.Empty;
            case Key.LeftShift:
            case Key.RightShift:
                return "SHIFT";
            case Key.Space:
                return "BOŞLUK";
            case Key.LeftCtrl:
            case Key.RightCtrl:
                return "CTRL";
            case Key.LeftAlt:
            case Key.RightAlt:
                return "ALT";
            case Key.Tab:
                return "TAB";
        }

        string name = key.ToString();
        if (name.StartsWith("Digit"))
        {
            return name.Substring(5);
        }

        return name.ToUpperInvariant();
    }
}
