using UnityEngine;
using UnityEngine.UI;

// Shows 0-3 filled stars out of a fixed set of star images.
public sealed class StarRow : MonoBehaviour
{
    [SerializeField] Image[] stars;
    [SerializeField] Color filled = new(1f, 0.82f, 0.2f, 1f);
    [SerializeField] Color empty = new(0.2f, 0.2f, 0.25f, 1f);

    public void Set(int count)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].color = i < count ? filled : empty;
        }
    }
}
