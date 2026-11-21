using UnityEngine;

/// <summary>
/// Strach gracza i muzyka napięcia:
///  - bicie serca rośnie, gdy Stalker jest blisko (także w rurach nad tobą),
///  - niski dron gra cały czas, a warstwa napięcia narasta z bliskością potwora,
///  - "stinger" (akord grozy), gdy potwór cię zauważy,
///  - FearVolume (post-processing) mocniej działa na ekran,
///  - podpowiedź o wstrzymaniu oddechu, gdy coś węszy przy twojej kryjówce.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class FearFeedback : MonoBehaviour
{
    public static FearFeedback Instance { get; private set; }

    public float fearRange = 14f;
    [Range(0, 1)] public float musicVolume = 1f;

    public float Fear { get; private set; }

    PlayerController pc;
    AudioSource beatSrc, droneSrc, tensionSrc, stingerSrc;
    AudioClip beat, stinger;
    float nextBeat, lastStinger = -999f;
    Component fearVolume;

    void Awake()
    {
        Instance = this;
        pc = GetComponent<PlayerController>();
        beatSrc = Make2D();
        stingerSrc = Make2D();
        droneSrc = Make2D();
        tensionSrc = Make2D();
        beat = ProceduralAudio.Heartbeat();
        stinger = ProceduralAudio.Stinger();
        droneSrc.clip = ProceduralAudio.Drone(8f);
        droneSrc.loop = true;
        droneSrc.volume = 0f;
        tensionSrc.clip = ProceduralAudio.TensionLayer(8f);
        tensionSrc.loop = true;
        tensionSrc.volume = 0f;
    }

    AudioSource Make2D()
    {
        var a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        return a;
    }

    void Start()
    {
        fearVolume = VolumeUtil.Find("FearVolume");
        droneSrc.Play();
        tensionSrc.Play();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool over = gm != null && gm.IsOver;
        bool dying = gm != null && gm.IsDying;

        float target = 0f;
        bool threatened = false;
        foreach (var s in StalkerAI.All)
        {
            if (s == null) continue;
            float d = Vector3.Distance(s.transform.position, transform.position);
            float f = Mathf.Clamp01(1f - (d - 2f) / fearRange);
            if (s.IsHuntingPlayer) f = Mathf.Max(f, 0.85f);
            target = Mathf.Max(target, f);
            if (pc.IsHidden && s.IsThreateningSpot(pc.CurrentSpot)) threatened = true;
        }
        if (dying) target = 1f;
        if (over) target = 0f;

        float udt = Time.unscaledDeltaTime;
        Fear = Mathf.MoveTowards(Fear, target, udt * (target > Fear ? 1.5f : 0.3f));
        VolumeUtil.SetWeight(fearVolume, Fear * 0.9f);

        float droneTarget = over ? 0f : (0.1f + 0.12f * Fear) * musicVolume;
        float tensionTarget = over || dying ? 0f : Fear * Fear * 0.4f * musicVolume;
        droneSrc.volume = Mathf.MoveTowards(droneSrc.volume, droneTarget, udt * 0.3f);
        tensionSrc.volume = Mathf.MoveTowards(tensionSrc.volume, tensionTarget, udt * 0.5f);

        if (!over && Fear > 0.05f && Time.time >= nextBeat)
        {
            beatSrc.PlayOneShot(beat, Mathf.Lerp(0.15f, 1f, Fear));
            nextBeat = Time.time + Mathf.Lerp(1.1f, 0.42f, Fear);
        }

        if (threatened && !pc.IsHoldingBreath && gm != null && !dying)
            gm.ShowMessage(Loc.T("Coś jest tuż obok...  TRZYMAJ [SPACJA] – wstrzymaj oddech!", "Something is right outside...  HOLD [SPACE] – hold your breath!"), 0.2f);
    }

    /// <summary>Akord grozy – gdy potwór zauważy gracza (z ograniczeniem częstotliwości).</summary>
    public void PlayStinger()
    {
        if (Time.time - lastStinger < 20f) return;
        lastStinger = Time.time;
        stingerSrc.PlayOneShot(stinger, 0.8f * Mathf.Max(0.3f, musicVolume));
    }
}
