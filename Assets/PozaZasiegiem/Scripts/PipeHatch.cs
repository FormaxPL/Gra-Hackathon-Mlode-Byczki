using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Właz w rurze pod sufitem – tędy Stalker wchodzi do rurociągu i z niego zeskakuje.
/// Podczas zasadzki potwór czai się w środku: słychać mruczenie, z otworu kapie ślina,
/// a w ciemności błyszczą oczy. NIE PRZECHODŹ POD SPODEM.
/// Pivot = środek rury w miejscu włazu.
/// </summary>
public class PipeHatch : MonoBehaviour
{
    public static readonly List<PipeHatch> All = new List<PipeHatch>();

    [Tooltip("Indeks węzła w PipeNetwork")]
    public int node = -1;
    public ParticleSystem dust;
    public ParticleSystem drool;
    [Tooltip("Oczy widoczne w otworze podczas zasadzki")]
    public GameObject lurkEyes;

    public Vector3 InsidePoint => transform.position;
    public bool IsLurking { get; private set; }

    AudioSource oneShot, loop;
    AudioClip rattle, drip, purr;
    float nextDisturb;
    bool floorChecked, floorValid;
    Vector3 floorPoint;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        oneShot = AddAudio(false);
        loop = AddAudio(true);
        rattle = ProceduralAudio.Clank(Random.Range(0, 1000), 0.45f);
        drip = ProceduralAudio.Drip();
        purr = ProceduralAudio.LurkPurr(3f);
        loop.clip = purr;
        if (lurkEyes != null) lurkEyes.SetActive(false);
    }

    AudioSource AddAudio(bool isLoop)
    {
        var a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 1.5f;
        a.maxDistance = isLoop ? 16f : 22f;
        a.loop = isLoop;
        return a;
    }

    /// <summary>Punkt na NavMeshu pod włazem (liczony przy pierwszym użyciu).</summary>
    public bool TryGetFloor(out Vector3 point)
    {
        if (!floorChecked)
        {
            floorChecked = true;
            Vector3 below = new Vector3(transform.position.x, 0f, transform.position.z);
            for (float r = 0f; r <= 3f && !floorValid; r += 0.75f)
            {
                for (int i = 0; i < 8 && !floorValid; i++)
                {
                    float ang = i * Mathf.PI / 4f;
                    Vector3 probe = below + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                    if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1f, NavMesh.AllAreas) && hit.position.y < 0.3f)
                    {
                        floorPoint = hit.position;
                        floorValid = true;
                    }
                    if (r == 0f) break;
                }
            }
        }
        point = floorPoint;
        return floorValid;
    }

    /// <summary>Kurz + grzechot (coś przechodzi przez rurę nad włazem).</summary>
    public void Disturb(float volume)
    {
        if (Time.time < nextDisturb) return;
        nextDisturb = Time.time + 1.2f;
        if (dust != null) dust.Play();
        oneShot.pitch = Random.Range(0.8f, 1.2f);
        oneShot.PlayOneShot(rattle, volume);
    }

    /// <summary>Kapnięcie śliny z otworu.</summary>
    public void Drip()
    {
        if (drool != null) drool.Play();
        oneShot.pitch = Random.Range(0.85f, 1.25f);
        oneShot.PlayOneShot(drip, 0.7f);
    }

    public void SetLurking(bool on)
    {
        IsLurking = on;
        if (lurkEyes != null) lurkEyes.SetActive(on);
        if (on)
        {
            loop.volume = 0.85f;
            if (!loop.isPlaying) loop.Play();
        }
        else loop.Stop();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, 0f, transform.position.z));
    }
}
