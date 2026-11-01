using UnityEngine;

// Cycles a list of sprites on a SpriteRenderer (torch flames). Starts at a random frame so neighbours do not flicker in sync.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField] float framesPerSecond = 9f;

    SpriteRenderer target;
    float time;

    void Awake()
    {
        target = GetComponent<SpriteRenderer>();
        time = Random.value * 10f;
    }

    void Update()
    {
        if (frames.Length == 0)
        {
            return;
        }

        time += Time.deltaTime;
        target.sprite = frames[(int)(time * framesPerSecond) % frames.Length];
    }
}
