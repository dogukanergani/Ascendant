using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// TEMPORARY play-mode test driver. Delete after use.
public sealed class MovementProbe : MonoBehaviour
{
    const string OutPath = @"C:\Users\DOUKAN~1\AppData\Local\Temp\claude\C--Users-do-ukan-Platformer\18df6192-6fa6-427d-8268-5d8154d2bcb7\scratchpad\probe.txt";

    PlayerController2D p;
    Rigidbody2D rb;
    SkillManager skills;
    Keyboard kb;
    InputSettings.EditorInputBehaviorInPlayMode oldBehavior;
    readonly System.Collections.Generic.HashSet<Key> held = new();
    readonly StringBuilder log = new();

    float maxY, minVy, maxVy;

    void Log(string s)
    {
        log.AppendLine(s);
        Debug.Log("PROBE " + s);
        File.WriteAllText(OutPath, log.ToString());
    }

    void Press(Key k, bool on)
    {
        if (on) held.Add(k); else held.Remove(k);
        Key[] arr = new Key[held.Count];
        held.CopyTo(arr);
        InputSystem.QueueStateEvent(kb, new KeyboardState(arr));
    }

    IEnumerator Tap(Key k, float dur = 0.04f)
    {
        Press(k, true);
        yield return new WaitForSeconds(dur);
        Press(k, false);
    }

    void ResetTrack()
    {
        maxY = float.NegativeInfinity;
        minVy = float.PositiveInfinity;
        maxVy = float.NegativeInfinity;
    }

    void Update()
    {
        if (rb == null) return;
        maxY = Mathf.Max(maxY, rb.position.y);
        minVy = Mathf.Min(minVy, rb.linearVelocity.y);
        maxVy = Mathf.Max(maxVy, rb.linearVelocity.y);
    }

    void Place(float x, float y)
    {
        rb.position = new Vector2(x, y);
        transform.position = new Vector3(x, y, 0f);
        rb.linearVelocity = Vector2.zero;
    }

    IEnumerator Ground()
    {
        Place(-5f, -1.18f);
        yield return new WaitForSeconds(0.5f);
    }

    void SetEnabled<T>(bool on) where T : Behaviour
    {
        GameObject.Find("Player").GetComponent<T>().enabled = on;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        GameObject player = GameObject.Find("Player");
        p = player.GetComponent<PlayerController2D>();
        rb = player.GetComponent<Rigidbody2D>();
        skills = FindFirstObjectByType<SkillManager>();

        oldBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        kb = InputSystem.AddDevice<Keyboard>("ProbeKeyboard");
        InputSystem.QueueStateEvent(kb, new KeyboardState());

        yield return new WaitForSeconds(1f);
        Log($"[init] grounded={p.IsGrounded} pos={rb.position} unlocked dash={skills.IsUnlocked("dash")} dj={skills.IsUnlocked("double_jump")} grapple={skills.IsUnlocked("grapple")}");

        // Run
        yield return Ground();
        SetEnabled<DoubleJumpSkill>(false);
        float x0 = rb.position.x;
        Press(Key.D, true);
        yield return new WaitForSeconds(0.5f);
        Log($"[run] holdD vx={rb.linearVelocity.x:F2} dx={rb.position.x - x0:F2} moveInput={p.MoveInput}");
        Press(Key.D, false);
        yield return new WaitForSeconds(0.3f);
        Log($"[run] release vx={rb.linearVelocity.x:F2}");

        // Variable jump height
        yield return Ground();
        float y0 = rb.position.y;
        ResetTrack();
        yield return Tap(Key.Space, 0.04f);
        yield return new WaitForSeconds(1.2f);
        Log($"[jump] tap height={maxY - y0:F2}");
        yield return Ground();
        y0 = rb.position.y;
        ResetTrack();
        Press(Key.Space, true);
        yield return new WaitForSeconds(1.0f);
        Press(Key.Space, false);
        Log($"[jump] hold height={maxY - y0:F2} (expected ~3.0)");

        // Coyote time
        for (int pass = 0; pass < 2; pass++)
        {
            float delay = pass == 0 ? 0.06f : 0.30f;
            Place(3.4f, 2.32f);
            yield return new WaitForSeconds(0.4f);
            Press(Key.D, true);
            float t = 0f;
            while (p.IsGrounded && t < 2f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(delay);
            ResetTrack();
            yield return Tap(Key.Space, 0.04f);
            yield return new WaitForSeconds(0.08f);
            Log($"[coyote] delay={delay:F2}s after leaving ground -> maxVy={maxVy:F2} (jump {(maxVy > 5f ? "YES" : "NO")}; expected {(pass == 0 ? "YES" : "NO")})");
            Press(Key.D, false);
            yield return new WaitForSeconds(1f);
        }

        // Jump buffer
        for (int pass = 0; pass < 2; pass++)
        {
            float lead = pass == 0 ? 0.07f : 0.35f;
            Place(-5f, 1.5f);
            yield return null;
            while (!(rb.linearVelocity.y < -1f && (rb.position.y - 0.8f + 2f) < -rb.linearVelocity.y * lead)) yield return null;
            ResetTrack();
            yield return Tap(Key.Space, 0.03f);
            yield return new WaitForSeconds(0.3f);
            Log($"[buffer] pressed ~{lead:F2}s before landing -> maxVy={maxVy:F2} (jump on landing {(maxVy > 5f ? "YES" : "NO")}; expected {(pass == 0 ? "YES" : "NO")})");
            yield return new WaitForSeconds(1f);
        }

        // Wall slide without pressing into wall
        SetEnabled<DoubleJumpSkill>(true);
        Place(7.6f, 5f);
        yield return new WaitForSeconds(0.5f);
        Log($"[wall-slide] no input: touching={p.IsTouchingWall} vy={rb.linearVelocity.y:F2} (slide clamp is -2)");
        Place(7.6f, 5f);
        Press(Key.D, true);
        yield return new WaitForSeconds(0.5f);
        Log($"[wall-slide] pressing into wall: touching={p.IsTouchingWall} vy={rb.linearVelocity.y:F2}");
        Press(Key.D, false);
        Place(7.6f, 5f);
        Press(Key.A, true);
        yield return new WaitForSeconds(0.5f);
        Log($"[wall-slide] pressing away: touching={p.IsTouchingWall} vy={rb.linearVelocity.y:F2}");
        Press(Key.A, false);

        // Wall jump
        Place(7.6f, 4f);
        Press(Key.D, true);
        yield return new WaitForSeconds(0.4f);
        yield return Tap(Key.Space, 0.04f);
        yield return new WaitForSeconds(0.04f);
        Log($"[wall-jump] vx={rb.linearVelocity.x:F2} vy={rb.linearVelocity.y:F2} (expected vx -8, vy ~13)");
        Press(Key.D, false);
        yield return new WaitForSeconds(1f);

        // Dash
        yield return Ground();
        Press(Key.D, true);
        yield return new WaitForSeconds(0.1f);
        Press(Key.D, false);
        yield return Tap(Key.LeftShift, 0.04f);
        yield return new WaitForSeconds(0.05f);
        DashSkill dash = p.GetComponent<DashSkill>();
        Log($"[dash] isDashing={dash.IsDashing} vx={rb.linearVelocity.x:F2} vy={rb.linearVelocity.y:F2} gravity={rb.gravityScale:F2}");
        yield return new WaitForSeconds(0.6f);
        Log($"[dash] after: vx={rb.linearVelocity.x:F2} isDashing={dash.IsDashing}");

        // Double jump
        yield return Ground();
        yield return Tap(Key.Space, 0.04f);
        yield return new WaitForSeconds(0.2f);
        ResetTrack();
        yield return Tap(Key.Space, 0.04f);
        yield return new WaitForSeconds(0.03f);
        Log($"[double-jump] second press: maxVy={maxVy:F2} (expected ~12)");
        yield return new WaitForSeconds(0.2f);
        ResetTrack();
        yield return Tap(Key.Space, 0.04f);
        yield return new WaitForSeconds(0.03f);
        Log($"[double-jump] third press: maxVy={maxVy:F2} (expected not 12)");
        yield return new WaitForSeconds(1.2f);

        // Grapple: standing in an empty area, only ground/walls exist.
        yield return Ground();
        GrappleSkill grapple = p.GetComponent<GrappleSkill>();
        bool used = skills.TryUse("grapple");
        Log($"[grapple] standing on bare ground, TryUse={used} hooked={grapple.IsHooked}");
        yield return new WaitForSeconds(1.5f);

        Log("[done]");
        InputSystem.RemoveDevice(kb);
        InputSystem.settings.editorInputBehaviorInPlayMode = oldBehavior;
    }
}
