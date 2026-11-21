using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Kopia renderera AERO musi wskazywać na naszą kopię materiału mgły we WSZYSTKICH feature'ach
/// (także w feature'ze oświetlenia, który wpisuje do materiału liczbę świateł). Inaczej mgła nie widzi lamp.</summary>
public class AeroSetupTests
{
    Material A, B, Other;
    UniversalRendererData rd;

    [SetUp]
    public void Make()
    {
        A = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AeroOriginal" };
        B = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AeroCopy" };
        Other = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Other" };
        rd = ScriptableObject.CreateInstance<UniversalRendererData>();
    }

    [TearDown]
    public void Clean()
    {
        foreach (var f in rd.rendererFeatures) Object.DestroyImmediate(f);
        Object.DestroyImmediate(rd); Object.DestroyImmediate(A); Object.DestroyImmediate(B); Object.DestroyImmediate(Other);
    }

    FullScreenPassRendererFeature Pass(Material m)
    {
        var f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        f.passMaterial = m;
        rd.rendererFeatures.Add(f);
        return f;
    }

    [Test]
    public void FogPassMaterial_IsRetargeted()
    {
        var f = Pass(A);
        Assert.AreEqual(1, LevelBuilder.RetargetFogMaterial(rd, A, B));
        Assert.AreSame(B, f.passMaterial);
    }

    [Test]
    public void OtherMaterialWithSameShader_IsRetargeted()
    {
        // feature oświetlenia AERO trzyma INNY wariant materiału mgły (ten sam shader)
        var variant = new Material(A) { name = "AeroVariant" };
        var f = Pass(variant);
        LevelBuilder.RetargetFogMaterial(rd, A, B);
        Assert.AreSame(B, f.passMaterial);
        Object.DestroyImmediate(variant);
    }

    [Test]
    public void MaterialWithDifferentShader_IsLeftAlone()
    {
        var f = Pass(Other);
        Assert.AreEqual(0, LevelBuilder.RetargetFogMaterial(rd, A, B));
        Assert.AreSame(Other, f.passMaterial);
    }

    [Test]
    public void TuneAeroMaterial_DoesNotThrowOnForeignShader()
    {
        Assert.DoesNotThrow(() => LevelBuilder.TuneAeroMaterial(Other));
    }

    [Test]
    public void TuneAeroMaterial_SetsTunedValues()
    {
        var sh = Shader.Find("Mirza/AERO/Volumetric Fog");
        if (sh == null) Assert.Ignore("AERO nie jest zaimportowany");
        var m = new Material(sh);
        LevelBuilder.TuneAeroMaterial(m);
        // 0.25 zalewało obraz poświatą, gdy gracz świeci latarką (rozproszenie 115° tuż przed kamerą)
        Assert.AreEqual(0.12f, m.GetFloat("_Density"), 1e-4f);
        Assert.AreEqual(0f, m.GetFloat("_Enable_Shadow_Colour"), 1e-4f);
        Assert.AreEqual(25f, m.GetFloat("_Max_Distance"), 1e-4f);
        Assert.AreEqual(0.15f, m.GetFloat("_Ambient_Light_Scale"), 1e-4f);
        // wydajność (pomiar 1080p przy automatach: 45 ms → ~9 ms na klatkę)
        Assert.AreEqual(10f, m.GetFloat("_Steps"), 1e-4f);
        Assert.AreEqual(0f, m.GetFloat("_Enable_Additional_Light_Self_Shadow"), 1e-4f);
        Assert.AreEqual(0f, m.GetFloat("_ENABLE_ADDITIONAL_LIGHT_SHADOWS"), 1e-4f);
        Assert.IsFalse(m.IsKeywordEnabled("_ENABLE_ADDITIONAL_LIGHT_SHADOWS"));
        Object.DestroyImmediate(m);
    }
}
