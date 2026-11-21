using System.Collections;
using UnityEngine;

public enum KillStyle { Lunge, Twist, Ceiling, Locker }

/// <summary>
/// Jumpscare przy śmierci – kilka wariantów:
///  - Lunge:   potwór rzuca się prosto w twarz,
///  - Twist:   stoi tuż przed tobą, powoli przekrzywia głowę... i atakuje,
///  - Ceiling: spada z rury nad głową, wisząc do góry nogami (zasadzka),
///  - Locker:  twarz potwora pojawia się w szczelinach szafki.
/// Po animacji ekran czernieje i GameManager pokazuje ekran śmierci.
/// </summary>
public class DeathSequence : MonoBehaviour
{
    public static DeathSequence Instance { get; private set; }
    public bool Playing { get; private set; }

    AudioSource src;
    AudioClip hit;
    float black;

    void Awake()
    {
        Instance = this;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        hit = ProceduralAudio.JumpscareHit();
    }

    public void Play(StalkerAI s, KillStyle style, string reason)
    {
        var gm = GameManager.Instance;
        var pc = PlayerController.Instance;
        if (Playing) return;
        if (s == null || pc == null)
        {
            if (gm != null) gm.GameOver(reason);
            return;
        }
        Playing = true;
        StartCoroutine(Run(s, style, reason));
    }

    IEnumerator Animate(float duration, System.Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    static void Look(Transform pivot, Vector3 target, float speed)
    {
        Vector3 d = target - pivot.position;
        if (d.sqrMagnitude < 0.0001f) return;
        pivot.rotation = Quaternion.Slerp(pivot.rotation, Quaternion.LookRotation(d), Time.deltaTime * speed);
    }

    void Hit(StalkerAI s, PlayerController pc, int voice)
    {
        src.PlayOneShot(hit, 1f);
        s.PlayKillVoice(voice);
        pc.AddShake(1f);
        if (GameManager.Instance != null) GameManager.Instance.Flash(new Color(0.55f, 0f, 0f, 0.55f), 0.45f);
    }

    IEnumerator Run(StalkerAI s, KillStyle style, string reason)
    {
        var gm = GameManager.Instance;
        var pc = PlayerController.Instance;
        s.BeginKill();
        Transform root = s.transform;
        ProceduralRig rig = s.Rig;
        Transform head = s.Head;
        Transform pivot = pc.cameraPivot;
        Vector3 eye = pivot.position;
        float floorY = pc.transform.position.y;

        if (rig != null) { rig.lookTarget = pivot; rig.reach = 1f; rig.scream = 0f; rig.headRoll = 0f; rig.climb = 0f; }

        // przesunięcie głowy względem korzenia (dla stojącej postaci)
        root.rotation = Quaternion.LookRotation(Flat(pc.transform.position - root.position, -pc.transform.forward));
        Vector3 headLocal = root.InverseTransformPoint(head.position);
        if (headLocal.y < 1f) headLocal = new Vector3(0f, 2.1f, 0.15f);

        switch (style)
        {
            case KillStyle.Ceiling:
            {
                Vector3 hatch = s.LastAmbushPoint;
                Vector3 dir = Flat(hatch - pc.transform.position, pc.transform.forward);
                Quaternion upside = Quaternion.LookRotation(-dir, Vector3.down);
                root.rotation = upside;
                Vector3 headStart = new Vector3(hatch.x, hatch.y - 0.15f, hatch.z);
                Vector3 headEnd = eye + dir * 0.55f;
                Vector3 rootStart = headStart - upside * headLocal;
                Vector3 rootEnd = headEnd - upside * headLocal;
                root.position = rootStart;
                s.PlayKillVoice(2);
                yield return StartCoroutine(Animate(0.3f, k => Look(pivot, headStart, 18f)));
                Hit(s, pc, 0);
                yield return StartCoroutine(Animate(0.2f, k =>
                {
                    root.position = Vector3.Lerp(rootStart, rootEnd, k * k);
                    if (rig != null) rig.scream = k;
                    Look(pivot, head.position, 35f);
                }));
                yield return StartCoroutine(Animate(0.8f, k =>
                {
                    if (rig != null) rig.scream = 1f;
                    pc.AddShake(0.9f);
                    Look(pivot, head.position, 30f);
                }));
                break;
            }
            case KillStyle.Locker:
            {
                var spot = pc.CurrentSpot;
                Vector3 inside = spot != null ? spot.transform.position : pc.transform.position;
                Vector3 front = spot != null ? spot.ExitPosition : inside + pc.transform.forward;
                Vector3 outward = Flat(front - inside, pc.transform.forward); // od gracza na zewnątrz szafki
                Quaternion face = Quaternion.LookRotation(-outward);
                root.rotation = face;
                Vector3 headMid = eye + outward * 0.85f;
                Vector3 headEnd = eye + outward * 0.42f;
                Vector3 rootStart = front + outward * 0.8f;
                rootStart.y = floorY;
                Vector3 rootMid = headMid - face * headLocal;
                Vector3 rootEnd = headEnd - face * headLocal;
                root.position = rootStart;
                s.PlayKillVoice(1);
                yield return StartCoroutine(Animate(0.9f, k => root.position = Vector3.Lerp(rootStart, rootMid, Mathf.SmoothStep(0f, 1f, k))));
                Hit(s, pc, 0);
                yield return StartCoroutine(Animate(0.12f, k =>
                {
                    root.position = Vector3.Lerp(rootMid, rootEnd, k);
                    if (rig != null) rig.scream = k;
                }));
                yield return StartCoroutine(Animate(0.8f, k => { if (rig != null) rig.scream = 1f; pc.AddShake(0.8f); }));
                break;
            }
            case KillStyle.Twist:
            {
                Vector3 dir = Flat(root.position - pc.transform.position, pc.transform.forward);
                Quaternion face = Quaternion.LookRotation(-dir);
                root.rotation = face;
                Vector3 headA = eye + dir * 0.95f + Vector3.up * 0.12f;
                Vector3 headB = eye + dir * 0.36f;
                Vector3 rootA = headA - face * headLocal;
                Vector3 rootB = headB - face * headLocal;
                root.position = rootA;
                s.PlayKillVoice(1);
                yield return StartCoroutine(Animate(1.1f, k =>
                {
                    if (rig != null) rig.headRoll = Mathf.SmoothStep(0f, 115f, k);
                    pc.AddShake(0.12f);
                    Look(pivot, head.position, 10f);
                }));
                Hit(s, pc, 0);
                yield return StartCoroutine(Animate(0.14f, k =>
                {
                    if (rig != null) { rig.headRoll = Mathf.Lerp(115f, 0f, k); rig.scream = k; }
                    root.position = Vector3.Lerp(rootA, rootB, k);
                    Look(pivot, head.position, 40f);
                }));
                yield return StartCoroutine(Animate(0.75f, k =>
                {
                    if (rig != null) rig.scream = 1f;
                    pc.AddShake(1f);
                    Look(pivot, head.position, 30f);
                }));
                break;
            }
            default: // Lunge
            {
                Vector3 dir = Flat(root.position - pc.transform.position, pc.transform.forward);
                Quaternion face = Quaternion.LookRotation(-dir);
                root.rotation = face;
                Vector3 rootA = pc.transform.position + dir * 1.4f;
                rootA.y = floorY;
                Vector3 headB = eye + dir * 0.42f;
                Vector3 rootB = headB - face * headLocal;
                root.position = rootA;
                yield return StartCoroutine(Animate(0.15f, k => Look(pivot, head.position, 25f)));
                Hit(s, pc, 0);
                yield return StartCoroutine(Animate(0.22f, k =>
                {
                    root.position = Vector3.Lerp(rootA, rootB, k * k);
                    if (rig != null) rig.scream = k;
                    Look(pivot, head.position, 30f);
                }));
                yield return StartCoroutine(Animate(0.75f, k =>
                {
                    if (rig != null) rig.scream = 1f;
                    pc.AddShake(1f);
                    Look(pivot, head.position, 30f);
                }));
                break;
            }
        }

        // cięcie do czerni
        yield return StartCoroutine(Animate(0.12f, k => black = k));
        yield return new WaitForSeconds(0.35f);
        if (gm != null) gm.GameOver(reason);
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f)
        {
            v = fallback;
            v.y = 0f;
        }
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    void OnGUI()
    {
        if (black <= 0f) return;
        GUI.depth = 10; // pod ekranem śmierci GameManagera
        GUI.color = new Color(0f, 0f, 0f, black);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
