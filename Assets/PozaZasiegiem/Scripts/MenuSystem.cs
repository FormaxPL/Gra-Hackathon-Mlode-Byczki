using UnityEngine;

/// <summary>
/// Menu główne, pauza (Esc) i ustawienia – rysowane przez OnGUI, bez Canvasa.
/// Gra stoi (Time.timeScale = 0), dopóki menu jest otwarte.
/// </summary>
public class MenuSystem : MonoBehaviour
{
    public static MenuSystem Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.page != Page.None;
    /// <summary>Ustawiane przy restarcie po śmierci – pomija menu główne.</summary>
    public static bool skipMainMenuOnce;

    public bool showMainMenuOnStart = true;

    enum Page { None, Main, Pause, Settings, Controls }
    Page page, back;
    bool gameStarted;
    Vector2 scroll;
    GUIStyle title, subtitle, button, label, value, small;
    float lastScale;
    Texture2D bg;
    AudioSource src;
    AudioClip clickClip;

    void Awake()
    {
        Instance = this;
        GameSettings.Load();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        clickClip = ProceduralAudio.Beep(900f, 0.04f);
    }

    /// <summary>Scena menu głównego (korytarz Backrooms z potworem) – czas w niej płynie, potwór się rusza.</summary>
    static bool InMenuScene => MapPicker.IsMenuScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

    void Start()
    {
        GameSettings.ApplyAll(true);
        // menu główne jest osobną sceną; mapa zaczyna od razu od intro
        if (InMenuScene) Open(Page.Main);
        else StartGame();
        skipMainMenuOnce = false;
    }

    void Open(Page p)
    {
        page = p;
        Time.timeScale = InMenuScene ? 1f : 0f;
        AudioListener.pause = p == Page.Pause;
        PlayerController.LockCursor(false);
    }

    public static void LoadScene(string name)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        // w edytorze scena może nie być w Build Settings – wczytaj po ścieżce
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets(name + " t:Scene"))
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            return;
        }
#endif
        UnityEngine.SceneManagement.SceneManager.LoadScene(name);
    }

    void Close()
    {
        page = Page.None;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PlayerController.LockCursor(true);
    }

    void StartGame()
    {
        if (InMenuScene)
        {
            // każde uruchomienie gry = losowy układ pomieszczeń
            StoryDirector.checkpoint = false;
            StoryDirector.introSeen = false;
            LoadScene(MapPicker.Pick(new System.Random()));
            return;
        }
        Close();
        if (!gameStarted)
        {
            gameStarted = true;
            if (StoryDirector.Instance != null) StoryDirector.Instance.OnGameStart();
            else if (GameManager.Instance != null) GameManager.Instance.ShowIntro();
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!GameInput.EscapeDown || DevConsole.IsOpen) return;
        if (page == Page.Settings || page == Page.Controls) { page = back; return; }
        if (page == Page.Pause) { Close(); return; }
        if (page != Page.None) return;
        if (gm != null && (gm.IsOver || gm.IsDying || gm.IsReading)) return;
        Open(Page.Pause);
    }

    // ===================== RYSOWANIE =====================

    void Styles()
    {
        float k = Screen.height / 1080f;
        if (title != null && Mathf.Abs(lastScale - k) < 0.001f) return;
        lastScale = k;
        title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(92 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
        subtitle = new GUIStyle(title) { fontSize = Mathf.RoundToInt(24 * k), fontStyle = FontStyle.Italic };
        subtitle.normal.textColor = new Color(0.8f, 0.25f, 0.2f);
        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(30 * k), alignment = TextAnchor.MiddleCenter };
        var dark = Tex(new Color(0.05f, 0.05f, 0.06f, 0.85f));
        var hover = Tex(new Color(0.35f, 0.05f, 0.04f, 0.95f));
        button.normal.background = dark; button.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        button.hover.background = hover; button.hover.textColor = Color.white;
        button.active.background = hover; button.active.textColor = Color.white;
        button.focused.background = dark;
        label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * k), alignment = TextAnchor.MiddleLeft };
        label.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        value = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        value.normal.textColor = Color.white;
        small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(18 * k), alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        bg = Tex(Color.white);
    }

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, bg);
        GUI.color = Color.white;
    }

    bool Button(Rect r, string text)
    {
        bool b = GUI.Button(r, text, button);
        if (b) src.PlayOneShot(clickClip, 0.5f);
        return b;
    }

    void OnGUI()
    {
        if (page == Page.None) return;
        Styles();
        GUI.depth = -10;
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;

        // ciemne tło z winietą i migającym tytułem
        // w scenie menu tło tylko lekko przyciemnione – widać korytarz i to, co w nim chodzi
        bool menuScene = InMenuScene && page == Page.Main;
        Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, menuScene ? 0.25f : page == Page.Main ? 0.72f : 0.8f));
        if (page == Page.Main || page == Page.Pause)
        {
            float flick = Random.value < 0.03f ? 0.4f : 1f;
            GUI.color = new Color(1f, 1f, 1f, flick);
            GUI.Label(new Rect(0, sh * 0.12f, sw, 120 * k), "THE BACKROOMS: ISOLATION", title);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, sh * 0.12f + 115 * k, sw, 40 * k),
                Loc.T("BRAK ZASIĘGU  •  nie przestawaj nasłuchiwać", "NO SIGNAL  •  keep listening"), subtitle);
        }

        float bw = 420 * k, bh = 64 * k, x = menuScene ? sw * 0.06f : (sw - bw) / 2f, y = sh * 0.42f, gap = 82 * k;
        switch (page)
        {
            case Page.Main:
                if (Button(new Rect(x, y, bw, bh), Loc.T("Rozpocznij grę", "Start game"))) StartGame();
                if (Button(new Rect(x, y + gap, bw, bh), Loc.T("Ustawienia", "Settings"))) { back = Page.Main; page = Page.Settings; }
                if (Button(new Rect(x, y + gap * 2, bw, bh), Loc.T("Sterowanie", "Controls"))) { back = Page.Main; page = Page.Controls; }
                if (Button(new Rect(x, y + gap * 3, bw, bh), Loc.T("Wyjdź z gry", "Quit game"))) Quit();
                GUI.Label(new Rect(0, sh - 60 * k, sw, 40 * k),
                    Loc.T("Słuchawki zalecane.  Esc – pauza", "Headphones recommended.  Esc – pause"), small);
                break;
            case Page.Pause:
                if (Button(new Rect(x, y, bw, bh), Loc.T("Wróć do gry", "Resume"))) Close();
                if (Button(new Rect(x, y + gap, bw, bh), Loc.T("Ustawienia", "Settings"))) { back = Page.Pause; page = Page.Settings; }
                if (Button(new Rect(x, y + gap * 2, bw, bh), Loc.T("Sterowanie", "Controls"))) { back = Page.Pause; page = Page.Controls; }
                if (Button(new Rect(x, y + gap * 3, bw, bh), Loc.T("Zacznij od nowa", "Restart"))) Restart(true);
                if (Button(new Rect(x, y + gap * 4, bw, bh), Loc.T("Menu główne", "Main menu"))) Restart(false);
                if (Button(new Rect(x, y + gap * 5, bw, bh), Loc.T("Wyjdź z gry", "Quit game"))) Quit();
                break;
            case Page.Settings:
                DrawSettings(sw, sh, k);
                break;
            case Page.Controls:
                DrawControls(sw, sh, k);
                break;
        }
    }

    void Restart(bool skipMenu)
    {
        skipMainMenuOnce = skipMenu;
        StoryDirector.checkpoint = false;
        if (!skipMenu) StoryDirector.introSeen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (!skipMenu) { LoadScene(MapPicker.MenuScene); return; } // „Menu główne” = scena menu
        if (GameManager.Instance != null) GameManager.Instance.Restart(); // ta sama mapa
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ===================== STEROWANIE =====================

    static readonly string[,] ControlRows =
    {
        { "W A S D", "Ruch", "Move" },
        { "Mysz", "Rozglądanie się", "Look around" },
        { "Shift", "Bieg – głośny, potwór go słyszy", "Run – loud, the creature hears it" },
        { "Ctrl / C", "Kucanie – ciche skradanie", "Crouch – quiet sneaking" },
        { "E", "Podnieś / użyj / czytaj / schowaj się w szafce", "Pick up / use / read / hide in a locker" },
        { "Spacja (trzymaj)", "Wstrzymaj oddech w kryjówce", "Hold your breath while hiding" },
        { "F", "Latarka", "Flashlight" },
        { "PPM", "Kamera (podnieś / opuść)", "Camera (raise / lower)" },
        { "F / N (z kamerą)", "Noktowizja", "Night vision" },
        { "Kółko myszy", "Zoom kamery", "Camera zoom" },
        { "Tab / Q (trzymaj)", "Wykrywacz ruchu", "Motion tracker" },
        { "LPM", "Gaśnica – odstrasza potwora", "Extinguisher – scares the creature off" },
        { "Esc", "Pauza", "Pause" },
    };

    void DrawControls(float sw, float sh, float k)
    {
        GUI.Label(new Rect(0, 30 * k, sw, 80 * k), Loc.T("STEROWANIE", "CONTROLS"), new GUIStyle(title) { fontSize = Mathf.RoundToInt(56 * k) });
        rowW = Mathf.Min(1100 * k, sw * 0.9f);
        rowX = (sw - rowW) / 2f;
        rowH = 46 * k;
        rowY = 130 * k;
        row = 0;
        var keyStyle = new GUIStyle(value) { fontStyle = FontStyle.Bold };
        for (int i = 0; i < ControlRows.GetLength(0); i++)
        {
            var r = NextRow();
            Fill(r, new Color(1f, 1f, 1f, 0.04f));
            GUI.Label(new Rect(r.x + 16, r.y, r.width * 0.32f, r.height), ControlRows[i, 0], keyStyle);
            GUI.Label(new Rect(r.x + r.width * 0.36f, r.y, r.width * 0.62f, r.height), Loc.T(ControlRows[i, 1], ControlRows[i, 2]), label);
        }
        float bw = 300 * k, bh = 58 * k;
        if (Button(new Rect((sw - bw) / 2f, Mathf.Min(sh - bh - 20 * k, rowY + row * (rowH + 6 * k) + 16 * k), bw, bh), Loc.T("Wróć", "Back")))
            page = back;
    }

    // ===================== USTAWIENIA =====================

    int row;
    float rowY, rowX, rowW, rowH;

    void DrawSettings(float sw, float sh, float k)
    {
        GUI.Label(new Rect(0, 30 * k, sw, 80 * k), Loc.T("USTAWIENIA", "SETTINGS"), new GUIStyle(title) { fontSize = Mathf.RoundToInt(56 * k) });
        rowW = Mathf.Min(1100 * k, sw * 0.9f);
        rowX = (sw - rowW) / 2f;
        rowH = 46 * k;
        rowY = 120 * k;
        row = 0;
        bool changed = false;
        bool resChanged = false;

        var res = GameSettings.Resolutions();
        int ri = GameSettings.resolutionIndex < 0 || GameSettings.resolutionIndex >= res.Count ? CurrentResIndex(res) : GameSettings.resolutionIndex;
        string[] modes = { Loc.T("Pełny ekran", "Fullscreen"), Loc.T("Okno bez ramek", "Borderless"), Loc.T("Okno", "Windowed") };
        string[] aa = { Loc.T("Wyłączony", "Off"), "FXAA", "SMAA", "TAA", "MSAA 4x" };
        string[] sh3 = { Loc.T("Niskie (tylko latarka)", "Low (flashlight only)"), Loc.T("Średnie (+ lampy)", "Medium (+ lamps)"), Loc.T("Wysokie (wszystko)", "High (everything)") };
        string[] onOff = { Loc.T("Wyłączone", "Off"), Loc.T("Włączone", "On") };
        string[] fps = { "30", "60", "120", "144", Loc.T("Bez limitu", "Unlimited") };

        int lang = Choice(Loc.T("Język", "Language"), Loc.Lang, Loc.Names);
        if (lang != Loc.Lang) { GameSettings.language = lang; Loc.Lang = lang; changed = true; }

        var resNames = new string[res.Count];
        for (int i = 0; i < res.Count; i++) resNames[i] = res[i].width + " x " + res[i].height;
        int nr = Choice(Loc.T("Rozdzielczość", "Resolution"), ri, resNames);
        if (nr != ri) { GameSettings.resolutionIndex = nr; resChanged = true; }
        int dm = Choice(Loc.T("Tryb wyświetlania", "Display mode"), GameSettings.displayMode, modes);
        if (dm != GameSettings.displayMode) { GameSettings.displayMode = dm; resChanged = true; }
        int q = Choice(Loc.T("Jakość grafiki", "Graphics quality"), Mathf.Clamp(GameSettings.quality, 0, QualitySettings.names.Length - 1), QualitySettings.names);
        if (q != GameSettings.quality) { GameSettings.quality = q; changed = true; }
        int a = Choice(Loc.T("Antyaliasing", "Anti-aliasing"), GameSettings.antialiasing, aa);
        if (a != GameSettings.antialiasing) { GameSettings.antialiasing = a; changed = true; }
        int s = Choice(Loc.T("Cienie", "Shadows"), GameSettings.shadows, sh3);
        if (s != GameSettings.shadows) { GameSettings.shadows = s; changed = true; }
        int v = Choice(Loc.T("Mgła i snopy światła", "Volumetric fog & light shafts"), GameSettings.volumetrics ? 1 : 0, onOff);
        if ((v == 1) != GameSettings.volumetrics) { GameSettings.volumetrics = v == 1; changed = true; }
        int b = Choice(Loc.T("Poświata (bloom)", "Bloom glow"), GameSettings.bloom ? 1 : 0, onOff);
        if ((b == 1) != GameSettings.bloom) { GameSettings.bloom = b == 1; changed = true; }
        int vs = Choice("VSync", GameSettings.vsync ? 1 : 0, onOff);
        if ((vs == 1) != GameSettings.vsync) { GameSettings.vsync = vs == 1; changed = true; }
        int f = Choice(Loc.T("Limit FPS", "FPS limit"), GameSettings.fpsLimitIndex, fps);
        if (f != GameSettings.fpsLimitIndex) { GameSettings.fpsLimitIndex = f; changed = true; }

        float br = Slider(Loc.T("Jasność", "Brightness"), GameSettings.brightness, -1f, 1f, (GameSettings.brightness * 100f).ToString("0"));
        if (!Mathf.Approximately(br, GameSettings.brightness)) { GameSettings.brightness = br; changed = true; }
        float fv = Slider(Loc.T("Pole widzenia", "Field of view"), GameSettings.fov, 60f, 95f, GameSettings.fov.ToString("0") + "°");
        if (!Mathf.Approximately(fv, GameSettings.fov)) { GameSettings.fov = Mathf.Round(fv); changed = true; }
        float se = Slider(Loc.T("Czułość myszy", "Mouse sensitivity"), GameSettings.sensitivity, 0.3f, 6f, GameSettings.sensitivity.ToString("0.0"));
        if (!Mathf.Approximately(se, GameSettings.sensitivity)) { GameSettings.sensitivity = se; changed = true; }
        float vo = Slider(Loc.T("Głośność", "Volume"), GameSettings.volume, 0f, 1f, (GameSettings.volume * 100f).ToString("0") + "%");
        if (!Mathf.Approximately(vo, GameSettings.volume)) { GameSettings.volume = vo; changed = true; }
        if (AeroFog.Instance != null)
        {
            float fd = Slider(Loc.T("Gęstość mgły (AERO)", "Fog density (AERO)"), GameSettings.fogDensity, 0.2f, 2.5f, (GameSettings.fogDensity * 100f).ToString("0") + "%");
            if (!Mathf.Approximately(fd, GameSettings.fogDensity)) { GameSettings.fogDensity = fd; changed = true; }
        }

        if (changed || resChanged)
        {
            GameSettings.ApplyAll(resChanged);
            GameSettings.Save();
        }

        float bw = 300 * k, bh = 58 * k;
        if (Button(new Rect((sw - bw) / 2f, Mathf.Min(sh - bh - 20 * k, rowY + row * (rowH + 6 * k) + 16 * k), bw, bh), Loc.T("Wróć", "Back")))
            page = back;
    }

    static int CurrentResIndex(System.Collections.Generic.List<Resolution> res)
    {
        for (int i = 0; i < res.Count; i++)
            if (res[i].width == Screen.width && res[i].height == Screen.height) return i;
        return res.Count - 1;
    }

    Rect NextRow()
    {
        var r = new Rect(rowX, rowY + row * (rowH + 6 * lastScale), rowW, rowH);
        row++;
        return r;
    }

    int Choice(string name, int current, string[] options)
    {
        var r = NextRow();
        Fill(r, new Color(1f, 1f, 1f, 0.04f));
        GUI.Label(new Rect(r.x + 16, r.y, r.width * 0.45f, r.height), name, label);
        float bw = r.height * 1.1f;
        float vx = r.x + r.width * 0.5f, vw = r.width * 0.5f - 16;
        int n = Mathf.Max(1, options.Length);
        current = Mathf.Clamp(current, 0, n - 1);
        if (Button(new Rect(vx, r.y + 4, bw, r.height - 8), "<")) current = (current - 1 + n) % n;
        GUI.Label(new Rect(vx + bw, r.y, vw - bw * 2f, r.height), options.Length > 0 ? options[current] : "-", value);
        if (Button(new Rect(vx + vw - bw, r.y + 4, bw, r.height - 8), ">")) current = (current + 1) % n;
        return current;
    }

    float Slider(string name, float current, float min, float max, string shown)
    {
        var r = NextRow();
        Fill(r, new Color(1f, 1f, 1f, 0.04f));
        GUI.Label(new Rect(r.x + 16, r.y, r.width * 0.45f, r.height), name, label);
        float vx = r.x + r.width * 0.5f, vw = r.width * 0.5f - 16;
        float result = GUI.HorizontalSlider(new Rect(vx, r.y + r.height * 0.42f, vw - 110 * lastScale, r.height * 0.3f), current, min, max);
        GUI.Label(new Rect(vx + vw - 100 * lastScale, r.y, 100 * lastScale, r.height), shown, value);
        return result;
    }
}
