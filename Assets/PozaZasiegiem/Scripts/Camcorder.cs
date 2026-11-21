using UnityEngine;

/// <summary>
/// Kamera z intro (jak w Outlast): PPM podnosi ją do oka – widzisz przez wizjer (REC, bateria),
/// F lub N przełącza NOKTOWIZJĘ (widać w ciemności, ale zużywa baterię kamery),
/// kółko myszy = zoom. Noktowizja to podczerwień – potwór jej nie widzi, w przeciwieństwie do latarki.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class Camcorder : MonoBehaviour
{
    public static Camcorder Instance { get; private set; }

    [Header("Bateria (zużywa ją noktowizja)")]
    public float battery = 100f;
    public float nvDrainPerSecond = 1.0f;

    [Header("Zoom")]
    public float minFov = 28f;

    [Header("Referencje")]
    [Tooltip("Promiennik podczerwieni – światło widoczne tylko w noktowizji")]
    public Light irLight;

    public bool IsRaised { get; private set; }
    public bool NightVision { get; private set; }
    public float Raise01 { get; private set; }
    public bool Viewing => Raise01 > 0.95f;
    public float Battery01 => battery / 100f;

    PlayerController pc;
    Camera cam;
    float baseFov, zoom, recStart;
    Component camVolume, nvVolume;
    AudioSource src;
    bool restoreFlashlight;
    AudioClip beepOn, beepOff, nvOnClip, nvOffClip, handleClip;
    GUIStyle small, big;

    void Awake()
    {
        Instance = this;
        pc = GetComponent<PlayerController>();
        cam = GetComponentInChildren<Camera>();
        if (cam != null) baseFov = cam.fieldOfView;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        beepOn = ProceduralAudio.Beep(1800f, 0.06f);
        beepOff = ProceduralAudio.Beep(900f, 0.08f);
        nvOnClip = ProceduralAudio.NightVisionSwitch(true);
        nvOffClip = ProceduralAudio.NightVisionSwitch(false);
        handleClip = ProceduralAudio.Grab();
        if (irLight != null) irLight.enabled = false;
    }

    void Start()
    {
        camVolume = VolumeUtil.Find("CamcorderVolume");
        nvVolume = VolumeUtil.Find("NightVisionVolume");
        VolumeUtil.SetWeight(camVolume, 0f);
        VolumeUtil.SetWeight(nvVolume, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var gm = GameManager.Instance;
        bool blocked = GameManager.Blocked;
        var hand = HandInteraction.Instance;
        bool handBusy = hand != null && hand.Busy;

        if (gm != null && (gm.IsOver || gm.IsDying) && IsRaised) IsRaised = false;
        if (!blocked && !handBusy && GameInput.CameraToggleDown) SetRaised(!IsRaised);
        if (!blocked && IsRaised && (GameInput.NightVisionDown || GameInput.FlashlightDown)) ToggleNV();

        Raise01 = Mathf.MoveTowards(Raise01, IsRaised ? 1f : 0f, dt * 4f);

        if (Viewing && !blocked) zoom = Mathf.Clamp01(zoom + GameInput.ScrollY * 0.12f);
        else if (!Viewing) zoom = Mathf.MoveTowards(zoom, 0f, dt * 3f);
        if (cam != null) cam.fieldOfView = Mathf.Lerp(baseFov, minFov, zoom);

        bool nvActive = NightVision && Viewing;
        if (nvActive)
        {
            battery -= nvDrainPerSecond * dt;
            if (battery <= 0f)
            {
                battery = 0f;
                NightVision = false;
                nvActive = false;
                if (gm != null) gm.ShowMessage(Loc.T("Bateria kamery się wyczerpała.", "The camera battery is dead."), 2.5f);
            }
        }
        if (irLight != null) irLight.enabled = nvActive;

        // noktowizja = latarka gaśnie sama (nie zdradza cię), po wyłączeniu NV wraca
        var fl = pc != null ? pc.Flashlight : null;
        if (fl != null)
        {
            if (nvActive && fl.IsOn) { fl.SetOn(false); restoreFlashlight = true; }
            else if (!nvActive && restoreFlashlight) { restoreFlashlight = false; if (!Raise01Busy) fl.SetOn(true); }
        }
        VolumeUtil.SetWeight(camVolume, Mathf.InverseLerp(0.85f, 1f, Raise01));
        VolumeUtil.SetWeight(nvVolume, nvActive ? 1f : 0f);
    }

    public void SetRaised(bool up)
    {
        if (up == IsRaised) return;
        IsRaised = up;
        src.PlayOneShot(handleClip, 0.5f);
        src.PlayOneShot(up ? beepOn : beepOff, 0.5f);
        if (up) recStart = Time.time;
    }

    void ToggleNV()
    {
        if (!NightVision && battery <= 0f)
        {
            if (GameManager.Instance != null) GameManager.Instance.ShowMessage(Loc.T("Bateria kamery jest pusta.", "The camera battery is empty."), 2f);
            return;
        }
        NightVision = !NightVision;
        src.PlayOneShot(NightVision ? nvOnClip : nvOffClip, 0.9f);
    }

    public void AddBattery(float amount) => battery = Mathf.Min(100f, battery + amount);

    bool Raise01Busy => PlayerController.CutsceneLock || (GameManager.Instance != null && GameManager.Instance.IsOver);

    /// <summary>Natychmiast chowa kamerę (przerywniki).</summary>
    public void ForceLower()
    {
        IsRaised = false;
        NightVision = false;
        Raise01 = 0f;
        zoom = 0f;
        if (irLight != null) irLight.enabled = false;
        VolumeUtil.SetWeight(camVolume, 0f);
        VolumeUtil.SetWeight(nvVolume, 0f);
    }

    /// <summary>Pole widzenia z ustawień (zoom liczy się względem niego).</summary>
    public void SetBaseFov(float fov)
    {
        baseFov = fov;
        if (cam != null && zoom <= 0.001f) cam.fieldOfView = fov;
    }

    // ===================== NAKŁADKA WIZJERA =====================

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (!Viewing) return;
        var gm = GameManager.Instance;
        if (gm != null && (gm.IsOver || gm.IsDying)) return;

        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
        if (small == null || small.fontSize != Mathf.RoundToInt(26 * k))
        {
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * k), fontStyle = FontStyle.Bold };
            small.normal.textColor = Color.white;
            big = new GUIStyle(small) { fontSize = Mathf.RoundToInt(34 * k) };
        }
        Color line = new Color(1f, 1f, 1f, 0.75f);
        float m = 48f * k, L = 70f * k, t = 3f * k;

        // narożniki wizjera
        Fill(new Rect(m, m, L, t), line); Fill(new Rect(m, m, t, L), line);
        Fill(new Rect(sw - m - L, m, L, t), line); Fill(new Rect(sw - m - t, m, t, L), line);
        Fill(new Rect(m, sh - m - t, L, t), line); Fill(new Rect(m, sh - m - L, t, L), line);
        Fill(new Rect(sw - m - L, sh - m - t, L, t), line); Fill(new Rect(sw - m - t, sh - m - L, t, L), line);

        // REC + czas nagrania
        bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
        if (blink) Fill(new Rect(m + 20f * k, m + 24f * k, 22f * k, 22f * k), new Color(1f, 0.1f, 0.1f, 0.95f));
        GUI.Label(new Rect(m + 52f * k, m + 16f * k, 300f * k, 40f * k), "REC", big);
        float rec = Time.time - recStart;
        int hh = (int)(rec / 3600f), mm = (int)(rec / 60f) % 60, ss = (int)rec % 60;
        GUI.Label(new Rect(m + 20f * k, m + 58f * k, 300f * k, 36f * k), $"{hh:00}:{mm:00}:{ss:00}", small);

        // bateria kamery
        float bw = 90f * k, bh = 34f * k, bx = sw - m - bw - 30f * k, by = m + 18f * k;
        Fill(new Rect(bx, by, bw, t), line); Fill(new Rect(bx, by + bh - t, bw, t), line);
        Fill(new Rect(bx, by, t, bh), line); Fill(new Rect(bx + bw - t, by, t, bh), line);
        Fill(new Rect(bx + bw, by + bh * 0.3f, 8f * k, bh * 0.4f), line);
        int bars = Mathf.CeilToInt(Battery01 * 4f);
        for (int i = 0; i < bars; i++)
            Fill(new Rect(bx + 6f * k + i * (bw - 12f * k) / 4f, by + 6f * k, (bw - 12f * k) / 4f - 4f * k, bh - 12f * k),
                Battery01 < 0.25f ? new Color(1f, 0.25f, 0.2f, 0.9f) : line);

        // tryb i zoom
        string mode = NightVision ? Loc.T("NOKTOWIZJA  [F]", "NIGHT VISION  [F]") : Loc.T("NOKTOWIZJA: WYŁ.  [F]", "NIGHT VISION: OFF  [F]");
        GUI.Label(new Rect(m + 20f * k, sh - m - 56f * k, 600f * k, 40f * k), mode, small);
        GUI.Label(new Rect(sw - m - 260f * k, sh - m - 56f * k, 240f * k, 40f * k), $"x{Mathf.Lerp(1f, baseFov / Mathf.Max(1f, minFov), zoom):0.0}  " + Loc.T("[kółko]", "[wheel]"), small);

        // krzyżyk
        float c = 14f * k;
        Fill(new Rect(sw / 2f - c, sh / 2f - t / 2f, c * 2f, t), new Color(1f, 1f, 1f, 0.5f));
        Fill(new Rect(sw / 2f - t / 2f, sh / 2f - c, t, c * 2f), new Color(1f, 1f, 1f, 0.5f));
    }
}
