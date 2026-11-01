using System.Collections;
using UnityEngine;

// The pack has no dash animation, so a dash reuses the walk frames and leaves fading ghosts of the current sprite.
[RequireComponent(typeof(DashSkill))]
public sealed class DashAfterimage : MonoBehaviour
{
    [SerializeField] SpriteRenderer source;
    [SerializeField] float duration = 0.2f;
    [SerializeField] float interval = 0.035f;
    [SerializeField] float fadeTime = 0.22f;
    [SerializeField] Color tint = new(0.55f, 0.75f, 1f, 0.55f);

    DashSkill dash;

    void Awake()
    {
        dash = GetComponent<DashSkill>();
        if (source == null)
        {
            source = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void OnEnable()
    {
        dash.Dashed += OnDashed;
    }

    void OnDisable()
    {
        dash.Dashed -= OnDashed;
    }

    void OnDashed()
    {
        StartCoroutine(Trail());
    }

    IEnumerator Trail()
    {
        float end = Time.time + duration;
        var wait = new WaitForSeconds(interval);
        while (Time.time < end)
        {
            SpawnGhost();
            yield return wait;
        }
    }

    void SpawnGhost()
    {
        if (source == null || source.sprite == null)
        {
            return;
        }

        var go = new GameObject("DashGhost");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sharedMaterial = source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        sr.spriteSortPoint = source.spriteSortPoint;
        sr.color = tint;
        go.AddComponent<FadeAndDestroy>().Begin(sr, fadeTime);
    }

    sealed class FadeAndDestroy : MonoBehaviour
    {
        SpriteRenderer sprite;
        float time;
        float total;
        float startAlpha;

        public void Begin(SpriteRenderer target, float fadeTime)
        {
            sprite = target;
            total = Mathf.Max(0.01f, fadeTime);
            startAlpha = target.color.a;
        }

        void Update()
        {
            time += Time.deltaTime;
            Color c = sprite.color;
            c.a = Mathf.Lerp(startAlpha, 0f, time / total);
            sprite.color = c;
            if (time >= total)
            {
                Destroy(gameObject);
            }
        }
    }
}
