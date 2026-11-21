using NUnit.Framework;
using UnityEngine;

/// <summary>Pozorne odbicie latarki: słabnie z odległością trafienia, bierze kolor powierzchni
/// i nigdy nie wchodzi w ścianę ani za kamerę.</summary>
public class FlashlightBounceTests
{
    [Test]
    public void Intensity_FallsWithDistance()
    {
        float near = FlashlightBounce.Intensity(1f, 26f, 2f), far = FlashlightBounce.Intensity(10f, 26f, 2f);
        Assert.Greater(near, far);
        Assert.Greater(far, 0f);
    }

    [Test] public void Intensity_IsZeroAtRange() => Assert.AreEqual(0f, FlashlightBounce.Intensity(26f, 26f, 2f), 1e-5f);
    [Test] public void Intensity_NeverExceedsBase() => Assert.LessOrEqual(FlashlightBounce.Intensity(0.05f, 26f, 2f), 2f);

    [Test]
    public void DefaultBaseIntensity_IsWeakEnoughNotToGlowInFog()
    {
        var go = new GameObject("FL", typeof(Light), typeof(Flashlight));
        var b = go.AddComponent<FlashlightBounce>();
        // przy 1.2 źródło punktowe świeciło w mgle AERO jak kula; 0.35 zostawia tylko odblask
        Assert.LessOrEqual(b.baseIntensity, 0.35f);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Tint_TakesSurfaceColour()
    {
        var c = FlashlightBounce.Tint(Color.white, Color.red);
        Assert.Greater(c.r, c.g);
    }

    [Test]
    public void Tint_OnBlackSurface_KeepsSomeLight()
    {
        var c = FlashlightBounce.Tint(Color.white, Color.black);
        Assert.GreaterOrEqual(c.maxColorComponent, 0.15f);
    }

    [Test]
    public void Placement_CloseToWall_StaysInFrontOfIt()
    {
        var hit = new Vector3(0f, 1f, 0.2f);
        var p = FlashlightBounce.Placement(hit, Vector3.back, Vector3.back, 0.2f);
        Assert.LessOrEqual(Vector3.Distance(p, hit), 0.2f * 0.5f + 1e-4f);
        Assert.Less(p.z, hit.z); // przed ścianą, po stronie kamery
    }

    [Test]
    public void Placement_FarFromWall_Is30cmAlongNormal()
    {
        var hit = new Vector3(0f, 1f, 8f);
        var p = FlashlightBounce.Placement(hit, Vector3.back, Vector3.back, 8f);
        Assert.AreEqual(0.3f, Vector3.Distance(p, hit), 1e-4f);
    }
}
