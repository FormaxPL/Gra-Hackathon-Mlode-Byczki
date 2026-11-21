using UnityEngine;

/// <summary>
/// Straszny ambient w całej grze (menu, mapy, przerywniki, trailer):
///  - zapętlone tło (ProceduralAudio.CreepyAmbience),
///  - co kilkanaście sekund odległy dźwięk z losowego kierunku: stukanie i jęk rur nad sufitem,
///    szept zza ściany, skrzypienie, brzęk metalu, kapanie. Z pogłosem „rury kanalizacyjnej”.
/// Tworzy się sam przy starcie gry i przeżywa zmianę sceny.
/// Konsola (`): „ambient on”, „ambient off”, „ambient 0.5” (głośność).
/// </summary>
public class AmbientScape : MonoBehaviour
{
    public enum Sound { Knocks, Groan, Whisper, Creak, Clank, Drip }

    public static AmbientScape Instance { get; private set; }
    /// <summary>Wyłączenie komendą „ambient off” (trailer i tak gra ambient).</summary>
    public static bool Enabled = true;
    /// <summary>Głośność całego ambientu 0..1 (komenda „ambient 0.5”).</summary>
    public static float Volume = 1f;

    [Tooltip("Głośność zapętlonego tła")]
    public float bedVolume = 0.3f;
    [Tooltip("Głośność odległych dźwięków")]
    public float eventVolume = 0.75f;
    [Tooltip("Przerwa między odległymi dźwiękami (s)")]
    public Vector2 eventInterval = new Vector2(7f, 18f);

    /// <summary>Trailer sam reżyseruje dźwięki – losowe zdarzenia są wtedy wyłączone, a tło zawsze gra.</summary>
    [System.NonSerialized] public bool scripted;

    AudioSource bed, far;
    AudioClip[] knocks, groans, whispers, creaks, clanks;
    AudioClip drip;
    AudioListener listener;
    readonly System.Random rng = new System.Random();
    float nextEvent, duck = 1f, duckTarget = 1f, duckSpeed = 1f, on;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();

    public static AmbientScape Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("Ambient (straszny)");
        DontDestroyOnLoad(go);
        return go.AddComponent<AmbientScape>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bed = gameObject.AddComponent<AudioSource>();
        bed.playOnAwake = false;
        bed.spatialBlend = 0f;
        bed.loop = true;
        bed.volume = 0f;
        bed.clip = ProceduralAudio.CreepyAmbience(24f);
        bed.Play();

        // odległe dźwięki: osobny obiekt 3D, przestawiany wokół słuchacza, z pogłosem i stłumieniem
        var farGo = new GameObject("Odległe dźwięki");
        farGo.transform.SetParent(transform, false);
        far = farGo.AddComponent<AudioSource>();
        far.playOnAwake = false;
        far.spatialBlend = 1f;
        far.rolloffMode = AudioRolloffMode.Linear;
        far.minDistance = 2f;
        far.maxDistance = 45f;
        far.dopplerLevel = 0f;
        var low = farGo.AddComponent<AudioLowPassFilter>();
        low.cutoffFrequency = 2400f;
        var rev = farGo.AddComponent<AudioReverbFilter>();
        rev.reverbPreset = AudioReverbPreset.SewerPipe;

        knocks = new[] { ProceduralAudio.DistantKnocks(11), ProceduralAudio.DistantKnocks(23), ProceduralAudio.DistantKnocks(37) };
        groans = new[] { ProceduralAudio.MetalGroan(5), ProceduralAudio.MetalGroan(19) };
        whispers = new[] { ProceduralAudio.Whisper(3, 2.2f), ProceduralAudio.Whisper(8, 1.6f) };
        creaks = new[] { ProceduralAudio.Creak(71), ProceduralAudio.Creak(92) };
        clanks = new[] { ProceduralAudio.Clank(401, 0.5f), ProceduralAudio.Clank(733, 0.4f) };
        drip = ProceduralAudio.Drip();
        nextEvent = Time.time + NextInterval(rng, eventInterval);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public static float NextInterval(System.Random r, Vector2 range) => range.x + (float)r.NextDouble() * (range.y - range.x);

    /// <summary>Wycisza ambient do poziomu level (0..1) w czasie seconds (np. cisza przed jumpscare'em).</summary>
    public void Duck(float level, float seconds)
    {
        duckTarget = Mathf.Clamp01(level);
        duckSpeed = seconds <= 0.001f ? 1000f : Mathf.Abs(duckTarget - duck) / seconds + 0.0001f;
    }

    /// <summary>Natychmiast ustawia poziom wyciszenia (np. czerń na początku trailera).</summary>
    public void SetDuck(float level) { duck = duckTarget = Mathf.Clamp01(level); }

    void Update()
    {
        bool active = Enabled || scripted;
        on = Mathf.MoveTowards(on, active ? 1f : 0f, Time.unscaledDeltaTime / 3f);
        duck = Mathf.MoveTowards(duck, duckTarget, duckSpeed * Time.unscaledDeltaTime);
        bed.volume = bedVolume * Volume * duck * on;

        if (scripted || !Enabled || duck < 0.5f) return;
        if (Time.time < nextEvent) return;
        nextEvent = Time.time + NextInterval(rng, eventInterval);
        PlayRandom();
    }

    AudioListener Listener()
    {
        if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
        return listener;
    }

    void PlayRandom()
    {
        var l = Listener();
        if (l == null) return;
        var kind = (Sound)rng.Next(6);
        // rury są pod sufitem: stukanie i jęki dochodzą z góry
        float up = kind == Sound.Knocks || kind == Sound.Groan || kind == Sound.Drip ? 1.5f + (float)rng.NextDouble() * 1.5f : 0f;
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float dist = kind == Sound.Whisper ? 3f + (float)rng.NextDouble() * 3f : 8f + (float)rng.NextDouble() * 10f;
        Vector3 pos = l.transform.position + new Vector3(Mathf.Cos(ang) * dist, up, Mathf.Sin(ang) * dist);
        Play(kind, pos, 1f);
    }

    /// <summary>Odtwarza odległy dźwięk w danym miejscu świata (trailer używa tego do reżyserii).</summary>
    public void Play(Sound kind, Vector3 worldPos, float volume)
    {
        AudioClip c;
        switch (kind)
        {
            case Sound.Knocks: c = Pick(knocks); break;
            case Sound.Groan: c = Pick(groans); break;
            case Sound.Whisper: c = Pick(whispers); break;
            case Sound.Creak: c = Pick(creaks); break;
            case Sound.Clank: c = Pick(clanks); break;
            default: c = drip; break;
        }
        far.transform.position = worldPos;
        far.pitch = 0.85f + (float)rng.NextDouble() * 0.25f;
        far.PlayOneShot(c, eventVolume * Volume * Mathf.Clamp01(volume));
    }

    AudioClip Pick(AudioClip[] a) => a[rng.Next(a.Length)];
}
