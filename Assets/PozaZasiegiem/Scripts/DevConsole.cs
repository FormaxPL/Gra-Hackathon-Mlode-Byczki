using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Konsola komend – klawisz ` (tylda, pod Esc). Działa w menu i na mapach.
///   trailer        – odtwarza trailer gry (też: „zwiastun”)
///   ambient on/off – włącza / wyłącza straszny ambient
///   ambient 0.5    – głośność ambientu 0..1
///   menu           – powrót do menu głównego
///   help           – lista komend
/// Trailer da się też odpalić bez konsoli: argument „-trailer” przy uruchomieniu zbudowanej gry
/// albo w edytorze „Poza Zasiegiem → Odtworz trailer” (też: -executeMethod TrailerMenu.Play).
/// </summary>
public class DevConsole : MonoBehaviour
{
    public const string TrailerArg = "-trailer";
#if UNITY_EDITOR
    /// <summary>Flaga ustawiana przez menu edytora – trailer rusza od razu po wejściu w Play.</summary>
    public const string EditorTrailerFlag = "PozaZasiegiem.TrailerOnPlay";
#endif

    public static DevConsole Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    static bool launchChecked;

    bool open, focus;
    string input = "";
    readonly List<string> lines = new List<string>();
    float savedScale = 1f;
    CursorLockMode savedLock;
    bool savedVisible;
    GUIStyle lineStyle, fieldStyle;
    float styleK;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance == null)
        {
            var go = new GameObject("Konsola komend");
            DontDestroyOnLoad(go);
            go.AddComponent<DevConsole>();
        }
        if (launchChecked) return;
        launchChecked = true;
        bool trailer = WantsTrailerOnLaunch(System.Environment.GetCommandLineArgs());
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool(EditorTrailerFlag, false))
        {
            UnityEditor.SessionState.SetBool(EditorTrailerFlag, false);
            trailer = true;
        }
#endif
        if (trailer) TrailerDirector.Request();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        lines.Add(Loc.T("Konsola komend. Wpisz „help”.", "Command console. Type \"help\"."));
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // =====================================================================
    //  KOMENDY
    // =====================================================================

    public static bool WantsTrailerOnLaunch(string[] args)
    {
        if (args == null) return false;
        foreach (var a in args)
            if (a != null && (a.Equals(TrailerArg, System.StringComparison.OrdinalIgnoreCase)
                              || a.Equals("--trailer", System.StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    /// <summary>Argument komendy „ambient”: on/off albo głośność 0..1. False = nie rozpoznano.</summary>
    public static bool ParseAmbientArg(string arg, out bool? enable, out float? volume)
    {
        enable = null;
        volume = null;
        if (string.IsNullOrEmpty(arg)) return false;
        switch (arg.ToLowerInvariant())
        {
            case "on": case "wl": case "wlacz": case "1": enable = true; return true;
            case "off": case "wyl": case "wylacz": case "0": enable = false; return true;
        }
        if (float.TryParse(arg.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v) && v >= 0f && v <= 1f)
        {
            volume = v;
            return true;
        }
        return false;
    }

    static string HelpText => Loc.T(
        "trailer – trailer gry  |  ambient on/off/0..1 – straszny ambient  |  menu – menu główne  |  help",
        "trailer – game trailer  |  ambient on/off/0..1 – creepy ambience  |  menu – main menu  |  help");

    /// <summary>Wykonuje komendę i zwraca odpowiedź do wypisania w konsoli.</summary>
    public static string Execute(string line)
    {
        var parts = (line ?? "").Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";
        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1] : null;
        switch (cmd)
        {
            case "help": case "pomoc": case "?":
                return HelpText;
            case "trailer": case "zwiastun":
                if (!Application.isPlaying) return Loc.T("Trailer działa tylko w trakcie gry.", "The trailer only works while playing.");
                TrailerDirector.Request();
                return Loc.T("Trailer: start.", "Trailer: starting.");
            case "ambient":
                if (arg == null)
                    return Loc.T($"Ambient: {(AmbientScape.Enabled ? "włączony" : "wyłączony")}, głośność {AmbientScape.Volume:0.##}",
                                 $"Ambience: {(AmbientScape.Enabled ? "on" : "off")}, volume {AmbientScape.Volume:0.##}");
                if (!ParseAmbientArg(arg, out bool? en, out float? vol))
                    return Loc.T("Użycie: ambient on | off | 0..1", "Usage: ambient on | off | 0..1");
                if (en.HasValue) AmbientScape.Enabled = en.Value;
                if (vol.HasValue) { AmbientScape.Volume = vol.Value; AmbientScape.Enabled = vol.Value > 0f; }
                if (Application.isPlaying) AmbientScape.Ensure();
                return Loc.T($"Ambient: {(AmbientScape.Enabled ? "włączony" : "wyłączony")}, głośność {AmbientScape.Volume:0.##}",
                             $"Ambience: {(AmbientScape.Enabled ? "on" : "off")}, volume {AmbientScape.Volume:0.##}");
            case "menu":
                if (!Application.isPlaying) return "";
                StoryDirector.checkpoint = false;
                StoryDirector.introSeen = false;
                MenuSystem.LoadScene(MapPicker.MenuScene);
                return "Menu.";
            default:
                return Loc.T($"Nieznana komenda: {parts[0]}. Wpisz „help”.", $"Unknown command: {parts[0]}. Type \"help\".");
        }
    }

    /// <summary>Komendy, które zmieniają scenę – konsola zamyka się przed ich wykonaniem.</summary>
    static bool ChangesScene(string line)
    {
        var c = (line ?? "").Trim().ToLowerInvariant();
        return c.StartsWith("trailer") || c.StartsWith("zwiastun") || c == "menu";
    }

    // =====================================================================
    //  OKNO
    // =====================================================================

    void Open()
    {
        open = true;
        focus = true;
        input = "";
        savedScale = Time.timeScale;
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        open = false;
        Time.timeScale = savedScale;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    void Submit()
    {
        string line = input.Trim();
        input = "";
        if (line.Length == 0) return;
        lines.Add("> " + line);
        if (ChangesScene(line)) Close();
        string res = Execute(line);
        if (!string.IsNullOrEmpty(res)) lines.Add(res);
        while (lines.Count > 30) lines.RemoveAt(0);
    }

    void Styles()
    {
        float k = Mathf.Max(0.6f, Screen.height / 1080f);
        if (lineStyle != null && Mathf.Abs(k - styleK) < 0.001f) return;
        styleK = k;
        lineStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * k), wordWrap = true };
        lineStyle.normal.textColor = new Color(0.85f, 0.95f, 0.8f);
        fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(22 * k) };
    }

    static bool IsToggleKey(KeyCode k) => k == KeyCode.BackQuote || k == KeyCode.Tilde;

    void OnGUI()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (IsToggleKey(e.keyCode))
            {
                if (open) Close(); else if (!TrailerDirector.Playing) Open();
                e.Use();
                return;
            }
            if (open && e.keyCode == KeyCode.Escape) { Close(); e.Use(); return; }
            if (open && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { Submit(); e.Use(); return; }
        }
        if (!open) return;

        Styles();
        GUI.depth = -100;
        float k = styleK, sw = Screen.width, h = Screen.height * 0.4f;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(0, 0, sw, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float lineH = 26f * k, y = h - 54f * k;
        for (int i = lines.Count - 1; i >= 0 && y > 4f; i--)
        {
            y -= lineH;
            GUI.Label(new Rect(14 * k, y, sw - 28 * k, lineH), lines[i], lineStyle);
        }

        GUI.SetNextControlName("PZConsoleInput");
        string next = GUI.TextField(new Rect(10 * k, h - 44 * k, sw - 20 * k, 36 * k), input, fieldStyle);
        input = next.Replace("`", "").Replace("~", "");
        if (focus) { GUI.FocusControl("PZConsoleInput"); focus = false; }
    }
}
