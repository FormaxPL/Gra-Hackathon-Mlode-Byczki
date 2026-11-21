using System.Reflection;
using UnityEngine;

/// <summary>
/// Integracja z darmowym assetem "AERO – Volumetric Fog and Mist" (Mirza Beig, URP).
/// Generator poziomu kopiuje renderer URP z AERO jako "PZ_AeroRenderer" i dopisuje go do listy
/// rendererów assetu URP. Tu tylko przełączamy kamerę między zwykłym rendererem a tym z mgłą
/// (ustawienia: "Mgła wolumetryczna") i skalujemy gęstość mgły w materiale.
/// Wszystko przez refleksję – projekt kompiluje się też bez AERO i bez URP.
/// </summary>
public class AeroFog : MonoBehaviour
{
    public static AeroFog Instance { get; private set; }

    [Tooltip("Materiał mgły AERO (kopia w Generated/)")]
    public Material fogMaterial;
    [Tooltip("Właściwości gęstości w materiale (znalezione przez generator) i ich wartości bazowe")]
    public string[] densityProperties = new string[0];
    public float[] densityBase = new float[0];
    public const string RendererName = "PZ_AeroRenderer";

    void Awake() => Instance = this;

    void OnDestroy()
    {
        // przywróć bazową gęstość w assecie (w edytorze zmiany materiału zostają po Play)
        SetDensity(1f);
    }

    /// <summary>Mgła AERO jest dostępna tylko wtedy, gdy materiał ma działający shader, a renderer z mgłą
    /// nie ma brakujących feature'ów. Bez zaimportowanego AERO (assetu nie ma w repozytorium) zostaje zwykły renderer.</summary>
    public static bool Available
    {
        get
        {
            if (Instance == null || !IsMaterialUsable(Instance.fogMaterial)) return false;
            var list = RendererList();
            int idx = FindRendererIndex();
            return list != null && idx >= 0 && !HasMissingFeatures(list[idx]);
        }
    }

    /// <summary>Materiał nadaje się do renderowania: shader istnieje, jest wspierany i nie jest shaderem błędu.</summary>
    public static bool IsMaterialUsable(Material m)
    {
        return m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader";
    }

    /// <summary>Renderer URP ma na liście feature'ów pusty wpis (np. skrypt AERO, którego nie ma w projekcie).</summary>
    public static bool HasMissingFeatures(Object rendererData)
    {
        if (rendererData == null) return true;
        var p = rendererData.GetType().GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.Instance);
        if (p == null || !(p.GetValue(rendererData) is System.Collections.IList l)) return false;
        foreach (var o in l)
            if (!(o is Object uo) || uo == null) return true;
        return false;
    }

    static Object[] RendererList()
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp == null) return null;
        for (var t = rp.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(rp) as Object[];
        }
        return null;
    }

    public static int FindRendererIndex()
    {
        var list = RendererList();
        if (list == null) return -1;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].name.StartsWith(RendererName)) return i;
        return -1;
    }

    /// <summary>Włącza/wyłącza mgłę AERO na kamerze (domyślnie na głównej).</summary>
    public static void Apply(bool on, Camera cam = null)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        int idx = on && Available ? FindRendererIndex() : -1;
        foreach (var c in cam.GetComponents<Component>())
        {
            if (c == null || c.GetType().Name != "UniversalAdditionalCameraData") continue;
            var m = c.GetType().GetMethod("SetRenderer", BindingFlags.Public | BindingFlags.Instance);
            if (m != null) { try { m.Invoke(c, new object[] { idx }); } catch { } }
        }
    }

    public void SetDensity(float multiplier)
    {
        if (fogMaterial == null) return;
        for (int i = 0; i < densityProperties.Length && i < densityBase.Length; i++)
            if (fogMaterial.HasProperty(densityProperties[i]))
                fogMaterial.SetFloat(densityProperties[i], densityBase[i] * multiplier);
    }
}
