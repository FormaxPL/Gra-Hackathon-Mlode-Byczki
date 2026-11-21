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
                material.SetInteger(LightCountId, frameData.Get<UniversalLightData>().additionalLightsCount);
        }
    }
}
