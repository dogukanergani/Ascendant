using UnityEngine;
using UnityEngine.UI;

// Animated backdrop for the menu screens: the cave parallax layers drift sideways at different speeds and warm
// embers float upwards. All motion uses unscaled time and is cheap (a few dozen small images).
public sealed class MenuBackground : MonoBehaviour
{
    [SerializeField] RawImage farLayer;
    [SerializeField] RawImage nearLayer;
    [SerializeField] RectTransform emberArea;
    [SerializeField] Image emberTemplate;
    [SerializeField] float farSpeed = 0.006f;
    [SerializeField] float nearSpeed = 0.016f;
    [SerializeField] int emberCount = 36;
    [SerializeField] Vector2 emberSpeedRange = new(18f, 60f);
    [SerializeField] Color[] emberColors =
    {
        new(1f, 0.72f, 0.28f, 1f),
        new(0.95f, 0.45f, 0.2f, 1f),
        new(0.7f, 0.55f, 1f, 1f),
    };

    struct Ember
    {
        public RectTransform rect;
        public Image image;
        public float speed;
        public float swayPhase;
        public float swayAmount;
        public float baseX;
        public float flicker;
        public Color color;
    }

    Ember[] embers;
    float farOffset;
    float nearOffset;

    void Start()
    {
        MakeRepeating(farLayer);
        MakeRepeating(nearLayer);
        if (emberTemplate == null || emberArea == null)
        {
            return;
        }

        emberTemplate.gameObject.SetActive(false);
        embers = new Ember[emberCount];
        Rect area = emberArea.rect;
        for (int i = 0; i < embers.Length; i++)
        {
            Image image = Instantiate(emberTemplate, emberArea);
            image.gameObject.SetActive(true);
            float size = Random.value < 0.3f ? 9f : 6f;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            Color color = emberColors[Random.Range(0, emberColors.Length)];
            embers[i] = new Ember
            {
                rect = image.rectTransform,
                image = image,
                speed = Random.Range(emberSpeedRange.x, emberSpeedRange.y),
                swayPhase = Random.value * 6.28f,
                swayAmount = Random.Range(8f, 30f),
                baseX = Random.Range(area.xMin, area.xMax),
                flicker = Random.Range(1.5f, 4f),
                color = color,
            };
            embers[i].rect.anchoredPosition = new Vector2(embers[i].baseX, Random.Range(area.yMin, area.yMax));
        }
    }

    static void MakeRepeating(RawImage image)
    {
        if (image != null && image.texture != null)
        {
            image.texture.wrapMode = TextureWrapMode.Repeat;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Scroll(farLayer, ref farOffset, farSpeed * dt);
        Scroll(nearLayer, ref nearOffset, nearSpeed * dt);
        if (embers == null)
        {
            return;
        }

        Rect area = emberArea.rect;
        float time = Time.unscaledTime;
        for (int i = 0; i < embers.Length; i++)
        {
            Ember ember = embers[i];
            Vector2 pos = ember.rect.anchoredPosition;
            pos.y += ember.speed * dt;
            if (pos.y > area.yMax + 20f)
            {
                pos.y = area.yMin - 20f;
                ember.baseX = Random.Range(area.xMin, area.xMax);
                embers[i].baseX = ember.baseX;
            }

            pos.x = ember.baseX + Mathf.Sin(time * 0.8f + ember.swayPhase) * ember.swayAmount;
            ember.rect.anchoredPosition = pos;
            // Fade in near the bottom, out near the top, with a slow flicker.
            float height = Mathf.InverseLerp(area.yMin, area.yMax, pos.y);
            float edge = Mathf.Clamp01(Mathf.Min(height, 1f - height) * 5f);
            float flicker = 0.65f + 0.35f * Mathf.Sin(time * ember.flicker + ember.swayPhase);
            Color color = ember.color;
            color.a = edge * flicker * 0.9f;
            ember.image.color = color;
        }
    }

    static void Scroll(RawImage image, ref float offset, float delta)
    {
        if (image == null)
        {
            return;
        }

        offset = Mathf.Repeat(offset + delta, 1f);
        Rect rect = image.uvRect;
        rect.x = offset;
        image.uvRect = rect;
    }
}
