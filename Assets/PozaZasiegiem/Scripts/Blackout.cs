using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AWARIA ZASILANIA: gasną lampy, świecące automaty i znaki, robi się prawie czarno.
/// Latarka ledwo świeci i ŚCIĄGA potwora (hałas + większy zasięg wzroku potwora).
/// Najlepszym narzędziem staje się kamera z noktowizją, której potwór nie widzi.
/// </summary>
public class Blackout : MonoBehaviour
{
    public static Blackout Instance { get; private set; }
    public static bool Active { get; private set; }

    [Tooltip("Siła latarki podczas awarii (0..1)")]
    public float flashlightPower = 0.3f;
    [Tooltip("Zasięg latarki podczas awarii (0..1)")]
    public float flashlightRange = 0.5f;
    [Tooltip("Co ile sekund włączona latarka 'wabi' potwora")]
    public float lureInterval = 2.2f;
    public float lureRadius = 15f;
    [Range(0, 1)] public float ambientLeft = 0.06f;

    float endTime = -1f, nextLure;
    Color savedAmbient, savedFog, savedCamBg;
    bool saved;
    readonly List<Renderer> swapped = new List<Renderer>();
    readonly List<Material[]> swappedOn = new List<Material[]>();
    readonly Dictionary<Material, Material> offCache = new Dictionary<Material, Material>();
    AudioSource src;
    AudioClip downClip, upClip;
    Coroutine routine;

    void Awake()
    {
        Instance = this;
        Active = false;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = false;
        downClip = ProceduralAudio.PowerDown();
        upClip = ProceduralAudio.PowerUp();
    }

    // po przeładowaniu sceny w trakcie awarii światła mogły się wyłączyć, zanim Active wróciło na false
    void Start() => LightCategory.ApplyAll();

    void OnDestroy() { if (Instance == this) Active = false; }

    /// <summary>Zaczyna awarię. duration &lt; 0 = do odwołania.</summary>
    public void Begin(float duration, bool showHint)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(BeginRoutine(duration, showHint));
    }

    public void End(bool flicker = true)
    {
        if (!Active) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(EndRoutine(flicker));
    }

    IEnumerator BeginRoutine(float duration, bool showHint)
    {
        src.PlayOneShot(downClip, 0.9f);
        // krótkie migotanie przed zgaśnięciem
        for (int i = 0; i < 3; i++)
        {
            SetPower(false);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
            SetPower(true);
            yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
        }
        SetPower(false);
        endTime = duration > 0f ? Time.time + duration : -1f;
        nextLure = Time.time + lureInterval;
        if (showHint && GameManager.Instance != null)
            GameManager.Instance.ShowMessage(Loc.T(
                "AWARIA ZASILANIA.\nLatarka ledwo świeci i ŚCIĄGA potwora. Użyj kamery: [PPM], potem [F] – noktowizja.",
                "POWER FAILURE.\nThe flashlight barely works and DRAWS the creature in. Use the camera: [RMB], then [F] – night vision."), 7f);
        routine = null;
    }

    IEnumerator EndRoutine(bool flicker)
    {
        src.PlayOneShot(upClip, 0.8f);
        if (flicker)
            for (int i = 0; i < 4; i++)
            {
                SetPower(true);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
                SetPower(false);
                yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));
            }
        SetPower(true);
        routine = null;
    }

    void SetPower(bool on)
    {
        bool blackout = !on;
        if (Active == blackout) return;
        Active = blackout;

        if (!saved)
        {
            savedAmbient = RenderSettings.ambientLight;
            savedFog = RenderSettings.fogColor;
            var cam = Camera.main;
            if (cam != null) savedCamBg = cam.backgroundColor;
            saved = true;
        }
        RenderSettings.ambientLight = blackout ? savedAmbient * ambientLeft : savedAmbient;
        RenderSettings.fogColor = blackout ? Color.black : savedFog;
        if (Camera.main != null) Camera.main.backgroundColor = blackout ? Color.black : savedCamBg;
        if (blackout) SwapEmissives(); else RestoreEmissives();
        LightCategory.ApplyAll();

        var amb = GameManager.Instance != null ? GameManager.Instance.Ambience : null;
        if (amb != null) amb.volume = blackout ? 0.02f : 0.12f;
    }

    void SwapEmissives()
    {
        RestoreEmissives();
#if UNITY_2022_2_OR_NEWER
        var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<Renderer>();
#endif
        foreach (var r in all)
        {
            if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;
            var mats = r.sharedMaterials;
            bool any = false;
            for (int i = 0; i < mats.Length; i++)
                if (IsEmissive(mats[i])) { any = true; break; }
            if (!any) continue;
            if (r.GetComponentInParent<BlackoutImmune>() != null || r.GetComponentInParent<PlayerController>() != null
                || r.GetComponentInParent<StalkerAI>() != null || r.GetComponentInParent<Pickup>() != null
                || r.GetComponentInParent<Note>() != null || r.GetComponentInParent<PipeHatch>() != null) continue;
            var off = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) off[i] = IsEmissive(mats[i]) ? OffVersion(mats[i]) : mats[i];
            swapped.Add(r);
            swappedOn.Add(mats);
            r.sharedMaterials = off;
        }
    }

    void RestoreEmissives()
    {
        for (int i = 0; i < swapped.Count; i++)
            if (swapped[i] != null) swapped[i].sharedMaterials = swappedOn[i];
        swapped.Clear();
        swappedOn.Clear();
    }

    static bool IsEmissive(Material m)
    {
        if (m == null || !m.IsKeywordEnabled("_EMISSION") || !m.HasProperty("_EmissionColor")) return false;
        Color c = m.GetColor("_EmissionColor");
        return c.maxColorComponent > 0.05f;
    }

    Material OffVersion(Material m)
    {
        if (offCache.TryGetValue(m, out var off)) return off;
        off = new Material(m) { name = m.name + " (bez prądu)" };
        off.DisableKeyword("_EMISSION");
        off.SetColor("_EmissionColor", Color.black);
        if (off.HasProperty("_BaseColor")) off.SetColor("_BaseColor", off.GetColor("_BaseColor") * 0.35f);
        offCache[m] = off;
        return off;
    }

    void Update()
    {
        if (!Active || routine != null) return;
        if (endTime > 0f && Time.time >= endTime) { End(); return; }
        var p = PlayerController.Instance;
        if (p == null || GameManager.Blocked) return;
        // włączona latarka w ciemności = "latarnia" dla potwora
        if (p.FlashlightOn && !p.IsHidden && Time.time >= nextLure)
        {
            nextLure = Time.time + lureInterval;
            NoiseSystem.Emit(p.transform.position, lureRadius);
        }
    }
}
