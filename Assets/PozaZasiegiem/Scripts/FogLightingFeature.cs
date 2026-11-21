using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Przekazuje materiałowi mgły pełnoekranowej dane o oświetleniu kamery: liczbę widocznych świateł dodatkowych
/// i kolor ambientu. Shader mgły AERO tego potrzebuje, żeby lampy świeciły w mgle.
/// Zastępuje odpowiednik z paczki AERO w kopii renderera (generator) – nasz skrypt jest w repozytorium,
/// więc projekt bez AERO nie dostaje błędu „missing RendererFeatures”.
/// </summary>
public class FogLightingFeature : ScriptableRendererFeature
{
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    public Material material;

    /// <summary>Ile świateł dodatkowych bierze udział w mgle (0 = wszystkie). Shader AERO liczy w każdym kroku
    /// mgły każde światło po kolei – przy ~90 światłach w kadrze mgła kosztowała 45 ms/klatkę w 1080p.</summary>
    public static int MaxFogLights = 12;

    public static int FogLightCount(int visible, int max) => max > 0 ? Mathf.Min(visible, max) : visible;

    LightingDataPass pass;

    public override void Create() => pass = new LightingDataPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;
        pass.renderPassEvent = renderPassEvent;
        pass.material = material;
        renderer.EnqueuePass(pass);
    }

    class LightingDataPass : ScriptableRenderPass
    {
        static readonly int LightCountId = Shader.PropertyToID("_AdditionalLightCount");
        static readonly int AmbientId = Shader.PropertyToID("_AmbientLighting");
        public Material material;

        // wartości materiału są po stronie CPU – wpisujemy je przy nagrywaniu grafu, bez własnego przebiegu GPU
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            material.SetColor(AmbientId, RenderSettings.ambientLight * RenderSettings.ambientIntensity);
            if (frameData.Contains<UniversalLightData>())
                material.SetInteger(LightCountId, FogLightCount(frameData.Get<UniversalLightData>().additionalLightsCount, MaxFogLights));
        }
    }
}
