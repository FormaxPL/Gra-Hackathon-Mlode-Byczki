using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Mgła AERO może być włączona tylko wtedy, gdy jej shader i renderer są kompletne
/// (projekt bez zaimportowanego AERO nie może dostać czarnego ekranu).</summary>
public class AeroFogTests
{
    [Test]
    public void NullMaterial_IsNotUsable() => Assert.IsFalse(AeroFog.IsMaterialUsable(null));

    [Test]
    public void ErrorShaderMaterial_IsNotUsable()
    {
        var m = new Material(Shader.Find("Hidden/InternalErrorShader"));
        Assert.IsFalse(AeroFog.IsMaterialUsable(m));
        Object.DestroyImmediate(m);
    }

    [Test]
    public void UrpLitMaterial_IsUsable()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Assert.IsTrue(AeroFog.IsMaterialUsable(m));
        Object.DestroyImmediate(m);
    }

    [Test]
    public void RendererWithNullFeature_HasMissingFeatures()
    {
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        rd.rendererFeatures.Add(null);
        Assert.IsTrue(AeroFog.HasMissingFeatures(rd));
        Object.DestroyImmediate(rd);
    }

    [Test]
    public void RendererWithoutFeatures_HasNoMissingFeatures()
    {
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        Assert.IsFalse(AeroFog.HasMissingFeatures(rd));
        Object.DestroyImmediate(rd);
    }
}
