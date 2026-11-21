using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Ustawienia gry zapisywane w PlayerPrefs i stosowane na żywo:
/// język, rozdzielczość, tryb ekranu, jakość, antyaliasing, cienie, efekty wolumetryczne,
/// poświata (bloom), jasność, pole widzenia, VSync, limit FPS, czułość myszy, głośność.
/// </summary>
public static class GameSettings
{
    public static int language = Loc.PL;
    public static int resolutionIndex = -1;   // -1 = obecna
    public static int displayMode = 0;        // 0 pełny ekran, 1 okno bez ramek, 2 okno
    public static int quality = -1;           // -1 = domyślna
    public static int antialiasing = 1;       // 0 wył, 1 FXAA, 2 SMAA, 3 TAA, 4 MSAA 4x
    public static int shadows = 2;            // 0 niskie, 1 średnie, 2 wysokie
    public static bool volumetrics = true;
    public static bool bloom = true;
    public static float brightness = 0f;      // -1..1
    public static float fov = 72f;
    public static bool vsync = true;
    public static int fpsLimitIndex = 4;      // 30,60,120,144,bez limitu
    public static float sensitivity = 2f;
    public static float volume = 1f;
    public static float fogDensity = 1f;      // mnożnik gęstości mgły AERO

    public static readonly int[] FpsLimits = { 30, 60, 120, 144, -1 };
    static bool loaded;

    public static List<Resolution> Resolutions()
    {
        var list = new List<Resolution>();
        var seen = new HashSet<long>();
        foreach (var r in Screen.resolutions)
        {
            long key = (long)r.width * 100000 + r.height;
            if (seen.Add(key)) list.Add(r);
        }
        if (list.Count == 0) list.Add(new Resolution { width = Screen.width, height = Screen.height });
        return list;
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        language = PlayerPrefs.GetInt("pz_lang", Loc.PL);
        resolutionIndex = PlayerPrefs.GetInt("pz_res", -1);
        displayMode = PlayerPrefs.GetInt("pz_display", 0);
        quality = PlayerPrefs.GetInt("pz_quality", QualitySettings.GetQualityLevel());
        antialiasing = PlayerPrefs.GetInt("pz_aa", 1);
        shadows = PlayerPrefs.GetInt("pz_shadows", 2);
        volumetrics = PlayerPrefs.GetInt("pz_vol", 1) == 1;
        bloom = PlayerPrefs.GetInt("pz_bloom", 1) == 1;
        brightness = PlayerPrefs.GetFloat("pz_bright", 0f);
        fov = PlayerPrefs.GetFloat("pz_fov", 72f);
        vsync = PlayerPrefs.GetInt("pz_vsync", 1) == 1;
        fpsLimitIndex = PlayerPrefs.GetInt("pz_fps", 4);
        sensitivity = PlayerPrefs.GetFloat("pz_sens", 2f);
        volume = PlayerPrefs.GetFloat("pz_volume", 1f);
        fogDensity = PlayerPrefs.GetFloat("pz_fog", 1f);
        Loc.Lang = language;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("pz_lang", language);
        PlayerPrefs.SetInt("pz_res", resolutionIndex);
        PlayerPrefs.SetInt("pz_display", displayMode);
        PlayerPrefs.SetInt("pz_quality", quality);
        PlayerPrefs.SetInt("pz_aa", antialiasing);
        PlayerPrefs.SetInt("pz_shadows", shadows);
        PlayerPrefs.SetInt("pz_vol", volumetrics ? 1 : 0);
        PlayerPrefs.SetInt("pz_bloom", bloom ? 1 : 0);
        PlayerPrefs.SetFloat("pz_bright", brightness);
        PlayerPrefs.SetFloat("pz_fov", fov);
        PlayerPrefs.SetInt("pz_vsync", vsync ? 1 : 0);
        PlayerPrefs.SetInt("pz_fps", fpsLimitIndex);
        PlayerPrefs.SetFloat("pz_sens", sensitivity);
        PlayerPrefs.SetFloat("pz_volume", volume);
        PlayerPrefs.SetFloat("pz_fog", fogDensity);
        PlayerPrefs.Save();
    }

    /// <summary>Stosuje wszystko poza rozdzielczością (ta tylko na żądanie, żeby ekran nie mrugał).</summary>
    public static void ApplyAll(bool includeResolution)
    {
        Loc.Lang = language;
        if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(quality, true);
        if (includeResolution) ApplyResolution();
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        int fps = FpsLimits[Mathf.Clamp(fpsLimitIndex, 0, FpsLimits.Length - 1)];
        Application.targetFrameRate = vsync ? -1 : fps;
        ApplyAntialiasing();
        LightCategory.SetShadowLevel(shadows);
        VolumetricFx.SetEnabled(volumetrics);
        AeroFog.Apply(volumetrics);
        if (AeroFog.Instance != null) AeroFog.Instance.SetDensity(fogDensity);
        VolumeUtil.SetWeight(VolumeUtil.Find("GlowVolume"), bloom ? 1f : 0f);
        VolumeUtil.SetWeight(VolumeUtil.Find("BrightnessUpVolume"), Mathf.Max(0f, brightness) * 0.6f);
        VolumeUtil.SetWeight(VolumeUtil.Find("BrightnessDownVolume"), Mathf.Max(0f, -brightness) * 0.6f);
        if (Camcorder.Instance != null) Camcorder.Instance.SetBaseFov(fov);
        else if (Camera.main != null) Camera.main.fieldOfView = fov;
        if (PlayerController.Instance != null) PlayerController.Instance.mouseSensitivity = sensitivity;
        AudioListener.volume = volume;
    }

    public static void ApplyResolution()
    {
        var list = Resolutions();
        FullScreenMode mode = displayMode == 0 ? FullScreenMode.ExclusiveFullScreen
            : displayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (resolutionIndex >= 0 && resolutionIndex < list.Count)
            Screen.SetResolution(list[resolutionIndex].width, list[resolutionIndex].height, mode);
        else
            Screen.fullScreenMode = mode;
    }

    static void ApplyAntialiasing()
    {
        var cam = Camera.main;
        int msaa = antialiasing == 4 ? 4 : 1;
        QualitySettings.antiAliasing = msaa > 1 ? msaa : 0;

        // URP: tryb AA kamery i MSAA w assecie (przez refleksję – działa też bez URP)
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null)
        {
            var p = rp.GetType().GetProperty("msaaSampleCount", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { try { p.SetValue(rp, msaa); } catch { } }
        }
        if (cam == null) return;
        foreach (var c in cam.GetComponents<Component>())
        {
            if (c == null || c.GetType().Name != "UniversalAdditionalCameraData") continue;
            var prop = c.GetType().GetProperty("antialiasing", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.PropertyType.IsEnum) continue;
            int mode = antialiasing >= 1 && antialiasing <= 3 ? antialiasing : 0;
            if (!System.Enum.IsDefined(prop.PropertyType, mode)) mode = 1;
            try { prop.SetValue(c, System.Enum.ToObject(prop.PropertyType, mode)); } catch { }
        }
        cam.allowMSAA = msaa > 1;
    }
}
