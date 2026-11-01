using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Placeholder death: the player stops, waits a moment and the scene reloads.
[RequireComponent(typeof(Health), typeof(PlayerController2D))]
public sealed class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] float reloadDelay = 1.5f;

    Health health;
    PlayerController2D player;

    void Awake()
    {
        health = GetComponent<Health>();
        player = GetComponent<PlayerController2D>();
    }

    void OnEnable()
    {
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Died -= OnDied;
    }

    void OnDied()
    {
        player.LockInput(reloadDelay + 1f);
        player.Velocity = Vector2.zero;
        StartCoroutine(Reload());
    }

    IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
