using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Brama serwisowa – zamknięta aż do kontaktu z Agencją. Po odblokowaniu kraty jadą w górę,
/// zapala się kogut i słychać sygnał, żeby dało się ją znaleźć w ciemności.
/// </summary>
public class GateController : MonoBehaviour
{
    public Transform bars;
    public float openHeight = 2.6f;
    public float openTime = 4f;
    public NavMeshObstacle obstacle;
    public Collider blocker;
    public GameObject beacon;
    public Renderer statusLamp;
    public Material lampLocked, lampOpen;

    public bool IsOpen { get; private set; }
    public bool Unlocked { get; private set; }

    AudioSource src, beepSrc;
    Vector3 barsClosed;

    void Awake()
    {
        if (bars != null) barsClosed = bars.localPosition;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.maxDistance = 40f;
        src.rolloffMode = AudioRolloffMode.Linear;
        beepSrc = gameObject.AddComponent<AudioSource>();
        beepSrc.playOnAwake = false;
        beepSrc.spatialBlend = 1f;
        beepSrc.minDistance = 3f;
        beepSrc.maxDistance = 45f;
        beepSrc.rolloffMode = AudioRolloffMode.Linear;
        beepSrc.loop = true;
        beepSrc.clip = MakeBeacon();
        if (beacon != null) beacon.SetActive(false);
    }

    static AudioClip MakeBeacon()
    {
        // krótki podwójny sygnał co 1.2 s
        int sr = 44100;
        int n = (int)(sr * 1.2f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = (t < 0.09f ? 1f : 0f) + (t > 0.16f && t < 0.25f ? 1f : 0f);
            d[i] = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.25f * env;
        }
        var c = AudioClip.Create("GateBeacon", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    public void Unlock(bool instant)
    {
        if (Unlocked) return;
        Unlocked = true;
        if (statusLamp != null && lampOpen != null) statusLamp.sharedMaterial = lampOpen;
        if (beacon != null) beacon.SetActive(true);
        beepSrc.Play();
        if (instant) { SetOpen(1f); return; }
        StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        src.PlayOneShot(ProceduralAudio.GateGrind(openTime), 1f);
        float t = 0f;
        while (t < openTime)
        {
            t += Time.deltaTime;
            SetOpen(Mathf.SmoothStep(0f, 1f, t / openTime));
            yield return null;
        }
        SetOpen(1f);
    }

    void SetOpen(float k)
    {
        if (bars != null) bars.localPosition = barsClosed + Vector3.up * openHeight * k;
        if (k > 0.6f && !IsOpen)
        {
            IsOpen = true;
            if (obstacle != null) obstacle.enabled = false;
            if (blocker != null) blocker.enabled = false;
        }
    }

    public void StopBeacon()
    {
        beepSrc.Stop();
    }
}
