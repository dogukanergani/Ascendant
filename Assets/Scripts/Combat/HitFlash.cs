using System.Collections;
using UnityEngine;

// Briefly swaps a target's sprites to a flat white material to show a hit landed.
public sealed class HitFlash : MonoBehaviour
{
    const float Duration = 0.08f;

    SpriteRenderer[] renderers;
    Material[] originals;
    Coroutine running;

    public static void Play(GameObject target, Material flashMaterial)
    {
        if (target == null || flashMaterial == null)
        {
            return;
        }

        HitFlash flash = target.GetComponent<HitFlash>();
        if (flash == null)
        {
            flash = target.AddComponent<HitFlash>();
        }

        flash.Flash(flashMaterial);
    }

    void Flash(Material flashMaterial)
    {
        if (running != null)
        {
            StopCoroutine(running);
            Restore();
        }

        running = StartCoroutine(Run(flashMaterial));
    }

    IEnumerator Run(Material flashMaterial)
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originals = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originals[i] = renderers[i].sharedMaterial;
            renderers[i].sharedMaterial = flashMaterial;
        }

        yield return new WaitForSecondsRealtime(Duration);
        Restore();
        running = null;
    }

    void Restore()
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sharedMaterial = originals[i];
            }
        }
    }
}
