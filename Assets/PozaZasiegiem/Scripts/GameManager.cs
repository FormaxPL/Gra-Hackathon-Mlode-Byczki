using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stan gry + cały HUD (rysowany przez OnGUI, żeby nie trzeba było ustawiać Canvasa).
/// Później możecie przenieść HUD na Canvas/TextMeshPro, logika zostaje ta sama.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static bool Blocked => Instance != null && Instance.InputBlocked;

    [Header("Zasady")]
    public int partsRequired = 3;

    [Header("Testy")]
    [Tooltip("Na start daje wykrywacz i gaśnicę – do testowania AI")]
    public bool debugGiveAllItems = false;
    [Tooltip("F1 w grze przełącza")]
    public bool showDebug = true;

    [TextArea(2, 5)]
    public string introMessage = "Spadłeś pod ziemię. BRAK ZASIĘGU.\nZnajdź 3 części i napraw radiostację w biurze kierownika.\nNasłuchuj stukania w rurach pod sufitem.  [PPM] kamera";
    [TextArea(2, 5)]
    public string introMessageEn = "You fell underground. NO SIGNAL.\nFind 3 parts and repair the radio in the manager's office.\nListen for knocking in the pipes under the ceiling.  [RMB] camera";

    public bool IsOver { get; private set; }
    public AudioSource Ambience { get; private set; }
    public bool IsDying => DeathSequence.Instance != null && DeathSequence.Instance.Playing && !IsOver;
    public bool IsReading => HandInteraction.Instance != null && HandInteraction.Instance.IsReading;
    public bool InputBlocked => IsOver || noteOpen || IsDying || IsReading || MenuSystem.IsOpen || PlayerController.CutsceneLock;

    string prompt, message, noteText, endTitle, endText;
    float messageUntil, flashUntil, flashDuration = 0.5f;
    Color flashColor;
    bool noteOpen, won;
    int noteFrame;
    GUIStyle centerStyle, bigStyle, smallStyle, noteStyle;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        var p = PlayerController.Instance;
        if (debugGiveAllItems && p != null)
        {
            p.Inventory.hasTracker = true;
            p.Inventory.hasWeapon = true;
            p.Inventory.weaponCharges = 5;
        }
        if (MenuSystem.Instance == null) ShowIntro();

        var amb = gameObject.AddComponent<AudioSource>();
        Ambience = amb;
        amb.clip = ProceduralAudio.Hum(2f);
        amb.loop = true;
        amb.volume = 0.12f;
        amb.Play();
    }

    void Update()
    {
        if (GameInput.DebugToggleDown) showDebug = !showDebug;
        if (IsOver)
        {
            if (GameInput.RestartDown) { MenuSystem.skipMainMenuOnce = true; Restart(); }
            return;
        }
        if (noteOpen && Time.frameCount > noteFrame && (GameInput.InteractDown || GameInput.FireDown))
            noteOpen = false;

        if (IsDying || IsReading || MenuSystem.IsOpen) return;
        if (!noteOpen && Cursor.lockState != CursorLockMode.Locked && GameInput.FireDown)
            PlayerController.LockCursor(true);
    }

    // ---------------- API ----------------

    public void SetPrompt(string p) => prompt = p;

    public void ShowIntro() => ShowMessage(Loc.T(introMessage, introMessageEn), 7f);

    public void ShowMessage(string m, float seconds)
    {
        message = m;
        messageUntil = Time.time + seconds;
    }

    public void OpenNote(string text)
    {
        noteText = text;
        noteOpen = true;
        noteFrame = Time.frameCount;
    }

    public void Flash(Color c, float duration)
    {
        flashColor = c;
        flashDuration = duration;
        flashUntil = Time.unscaledTime + duration;
    }

    /// <summary>Śmierć gracza z jumpscare'em (DeathSequence), a potem ekran śmierci.</summary>
    public void Kill(StalkerAI killer, KillStyle style, string reason)
    {
        if (IsOver || IsDying) return;
        if (DeathSequence.Instance != null) DeathSequence.Instance.Play(killer, style, reason);
        else GameOver(reason);
    }

    public void GameOver(string reason)
    {
        if (IsOver) return;
        IsOver = true;
        won = false;
        endTitle = Loc.T("NIE ŻYJESZ", "YOU DIED");
        endText = reason;
        Time.timeScale = 0f;
        PlayerController.LockCursor(false);
    }

    public void Win(string title, string text)
    {
        if (IsOver) return;
        IsOver = true;
        won = true;
        endTitle = title;
        endText = text;
        Time.timeScale = 0f;
        PlayerController.LockCursor(false);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        var s = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        if (s.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(s.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(s.buildIndex);
    }

    // ---------------- HUD ----------------

    void EnsureStyles()
    {
        float k = Screen.height / 1080f;
        if (centerStyle != null && centerStyle.fontSize == Mathf.RoundToInt(28 * k)) return;
        centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.RoundToInt(28 * k) };
        centerStyle.normal.textColor = Color.white;
        bigStyle = new GUIStyle(centerStyle) { fontSize = Mathf.RoundToInt(72 * k), fontStyle = FontStyle.Bold };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * k) };
        smallStyle.normal.textColor = Color.white;
        noteStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = Mathf.RoundToInt(26 * k), fontStyle = FontStyle.Italic };
        noteStyle.normal.textColor = new Color(0.12f, 0.1f, 0.08f);
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Shadowed(Rect r, string text, GUIStyle style)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        GUI.color = Color.white;
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (MenuSystem.IsOpen || (PlayerController.CutsceneLock && !IsOver)) return;
        EnsureStyles();
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
        var p = PlayerController.Instance;

        if (p != null && p.IsHidden && !IsOver) DrawLockerOverlay(sw, sh);

        if (Time.unscaledTime < flashUntil)
        {
            float a = (flashUntil - Time.unscaledTime) / flashDuration;
            Fill(new Rect(0, 0, sw, sh), new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * a));
        }

        if (IsOver)
        {
            Fill(new Rect(0, 0, sw, sh), won ? new Color(0f, 0f, 0f, 0.88f) : new Color(0.18f, 0f, 0f, 0.88f));
            Shadowed(new Rect(0, sh * 0.28f, sw, sh * 0.15f), endTitle, bigStyle);
            Shadowed(new Rect(sw * 0.2f, sh * 0.45f, sw * 0.6f, sh * 0.3f), endText + Loc.T("\n\n[R] zagraj ponownie", "\n\n[R] play again"), centerStyle);
            return;
        }

        if (IsDying) return;

        if (noteOpen)
        {
            var r = new Rect(sw * 0.3f, sh * 0.12f, sw * 0.4f, sh * 0.76f);
            Fill(r, new Color(0.92f, 0.89f, 0.8f, 0.97f));
            GUI.Label(new Rect(r.x + 30 * k, r.y + 30 * k, r.width - 60 * k, r.height - 60 * k), noteText + Loc.T("\n\n[E] zamknij", "\n\n[E] close"), noteStyle);
            return;
        }

        var camc = Camcorder.Instance;
        bool viewing = camc != null && camc.Viewing;
        bool reading = IsReading;
        if (!viewing && !reading) Fill(new Rect(sw / 2 - 2, sh / 2 - 2, 4, 4), new Color(1f, 1f, 1f, 0.6f));

        if (!string.IsNullOrEmpty(prompt) && !reading)
            Shadowed(new Rect(0, sh * 0.55f, sw, 50 * k), prompt, centerStyle);
        if (Time.time < messageUntil && !string.IsNullOrEmpty(message))
            Shadowed(new Rect(sw * 0.15f, sh * 0.06f, sw * 0.7f, sh * 0.15f), message, centerStyle);

        if (reading) return;
        if (p != null && !viewing) DrawStatus(p, sh, k);
        if (!viewing) DrawSignal(sw, k);
        var story = StoryDirector.Instance;
        if (!viewing && story != null && !string.IsNullOrEmpty(story.Objective))
            Shadowed(new Rect(24 * k, 64 * k, sw * 0.5f, 30 * k), Loc.T("Cel: ", "Objective: ") + story.Objective, smallStyle);
        if (showDebug) DrawDebug(k);
        if (Cursor.lockState != CursorLockMode.Locked)
            Shadowed(new Rect(0, sh * 0.85f, sw, 40 * k), Loc.T("Kliknij w okno gry, aby sterować", "Click the game window to play"), centerStyle);
    }

    void DrawStatus(PlayerController p, float sh, float k)
    {
        float x = 24 * k, w = 220 * k, h = 10 * k;
        float y = sh - 150 * k;
        GUI.Label(new Rect(x, y, 400 * k, 30 * k), Loc.T("Kondycja", "Stamina"), smallStyle);
        Fill(new Rect(x, y + 28 * k, w, h), new Color(0f, 0f, 0f, 0.5f));
        Fill(new Rect(x, y + 28 * k, w * p.Stamina01, h), new Color(0.9f, 0.9f, 0.9f, 0.8f));

        y += 45 * k;
        float bat = p.Flashlight != null ? p.Flashlight.Battery01 : 0f;
        GUI.Label(new Rect(x, y, 400 * k, 30 * k), Loc.T("Latarka [F]", "Flashlight [F]"), smallStyle);
        Fill(new Rect(x, y + 28 * k, w, h), new Color(0f, 0f, 0f, 0.5f));
        Fill(new Rect(x, y + 28 * k, w * bat, h), bat < 0.15f ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(1f, 0.9f, 0.5f, 0.8f));

        var inv = p.Inventory;
        string items = Loc.T("Części: ", "Parts: ") + $"{inv.parts}/{partsRequired}";
        if (Camcorder.Instance != null) items += Loc.T("     [PPM] Kamera", "     [RMB] Camera") + $" ({Camcorder.Instance.Battery01 * 100f:0}%)";
        if (inv.hasTracker) items += Loc.T("     [Tab/Q] Wykrywacz", "     [Tab/Q] Tracker");
        if (inv.hasWeapon) items += Loc.T("     [LPM] Gaśnica", "     [LMB] Extinguisher") + $" ({inv.weaponCharges})";
        GUI.Label(new Rect(x, y + 45 * k, 900 * k, 30 * k), items, smallStyle);
    }

    void DrawDebug(float k)
    {
        float y = 10 * k;
        GUI.Label(new Rect(10 * k, y, 900 * k, 30 * k), "[F1] debug", smallStyle);
        foreach (var s in StalkerAI.All)
        {
            y += 26 * k;
            GUI.Label(new Rect(10 * k, y, 1200 * k, 30 * k),
                $"{s.name}: {s.CurrentState} | widzi: {(s.CanSeePlayer ? "TAK" : "nie")} | podejrzenie: {s.Suspicion:0.00} | napięcie: {s.Menace:0} | dystans: {s.DistanceToPlayer:0.0} m",
                smallStyle);
        }
    }

    /// <summary>Wskaźnik zasięgu w rogu ekranu – przez całą grę: BRAK ZASIĘGU.</summary>
    void DrawSignal(float sw, float k)
    {
        float x = sw - 190f * k, y = 24f * k;
        for (int i = 0; i < 4; i++)
        {
            float bh = (8f + i * 7f) * k;
            var r = new Rect(x + i * 11f * k, y + 30f * k - bh, 7f * k, bh);
            Fill(r, new Color(1f, 1f, 1f, 0.18f));
        }
        bool blink = Mathf.Repeat(Time.unscaledTime, 1.6f) < 1.1f;
        if (blink) Fill(new Rect(x - 4f * k, y + 14f * k, 52f * k, 3f * k), new Color(0.9f, 0.15f, 0.1f, 0.85f));
        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(x + 52f * k, y + 6f * k, 160f * k, 30f * k), Loc.T("BRAK ZASIĘGU", "NO SIGNAL"), smallStyle);
        GUI.color = Color.white;
    }

    void DrawLockerOverlay(float sw, float sh)
    {
        var c = new Color(0f, 0f, 0f, 0.94f);
        int n = 4;
        float slitH = sh * 0.035f, gap = sh * 0.03f;
        float total = n * slitH + (n - 1) * gap;
        float y = (sh - total) / 2f;
        float slitW = sw * 0.55f, x0 = (sw - slitW) / 2f;
        Fill(new Rect(0, 0, sw, y), c);
        for (int i = 0; i < n; i++)
        {
            Fill(new Rect(0, y, x0, slitH), c);
            Fill(new Rect(x0 + slitW, y, sw - x0 - slitW, slitH), c);
            y += slitH;
            if (i < n - 1) { Fill(new Rect(0, y, sw, gap), c); y += gap; }
        }
        Fill(new Rect(0, y, sw, sh - y), c);
        Shadowed(new Rect(0, sh * 0.85f, sw, 40), Loc.T("[E] wyjdź z kryjówki     [SPACJA] wstrzymaj oddech", "[E] leave hiding spot     [SPACE] hold breath"), centerStyle);
        var p = PlayerController.Instance;
        if (p != null)
        {
            float k = sh / 1080f, w = 320f * k, h = 8f * k;
            var r = new Rect((sw - w) / 2f, sh * 0.85f + 44f * k, w, h);
            Fill(r, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Color bc = p.IsHoldingBreath ? new Color(0.55f, 0.8f, 1f, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
            if (p.Breath01 < 0.3f) bc = new Color(1f, 0.3f, 0.2f, 0.9f);
            Fill(new Rect(r.x, r.y, w * p.Breath01, h), bc);
        }
    }
}
