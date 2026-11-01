using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Shows 0-3 filled stars out of a fixed set of star images. Set() is instant; Reveal() lights them one after
// another with a small pop.
public sealed class StarRow : MonoBehaviour
{
    [SerializeField] Image[] stars;
    [SerializeField] Sprite filledSprite;
    [SerializeField] Sprite emptySprite;
    [SerializeField] float revealInterval = 0.45f;
    [SerializeField] float popDuration = 0.3f;

    Coroutine reveal;

    public void Set(int count)
    {
        if (reveal != null)
        {
            StopCoroutine(reveal);
            reveal = null;
        }

        for (int i = 0; i < stars.Length; i++)
        {
            Apply(i, i < count);
            stars[i].rectTransform.localScale = Vector3.one;
        }
    }

    // All stars start empty; the earned ones light up in order. done is called after the last pop.
    public void Reveal(int count, System.Action done = null)
    {
        if (reveal != null)
        {
            StopCoroutine(reveal);
        }

        reveal = StartCoroutine(RevealRoutine(count, done));
    }

    public int Count => stars.Length;

    void Apply(int index, bool filled)
    {
        stars[index].sprite = filled ? filledSprite : emptySprite;
    }

    IEnumerator RevealRoutine(int count, System.Action done)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            Apply(i, false);
            stars[i].rectTransform.localScale = Vector3.one;
        }

        for (int i = 0; i < count && i < stars.Length; i++)
        {
            yield return new WaitForSecondsRealtime(revealInterval);
            Apply(i, true);
            RectTransform rt = stars[i].rectTransform;
            for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / popDuration;
                // Overshoot then settle.
                rt.localScale = Vector3.one * (1f + 0.8f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }

            rt.localScale = Vector3.one;
        }

        reveal = null;
        done?.Invoke();
    }
}
