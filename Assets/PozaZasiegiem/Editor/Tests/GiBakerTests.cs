using NUnit.Framework;
using UnityEngine;

/// <summary>Do wypalania GI trafiają tylko stałe lampy i świecące obiekty. Światła fabularne, migające, obrotowe
/// i przyczepione do czegoś ruchomego zostają na żywo (inaczej ich odbicie świeciłoby, gdy są zgaszone).</summary>
public class GiBakerTests
{
    readonly System.Collections.Generic.List<GameObject> made = new System.Collections.Generic.List<GameObject>();

    GameObject Make(int cat, params System.Type[] extra)
    {
        var g = new GameObject("L", typeof(Light));
        made.Add(g);
        g.AddComponent<LightCategory>().category = cat;
        foreach (var t in extra) g.AddComponent(t);
        return g;
    }

    GameObject Parent(System.Type t)
    {
        var p = new GameObject("P");
        made.Add(p);
        p.AddComponent(t);
        return p;
    }

    [TearDown]
    public void Clean()
    {
        foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
        made.Clear();
    }

    [Test] public void CeilingLamp_IsBaked() => Assert.IsTrue(GiBaker.ShouldBake(Make(1).GetComponent<Light>()));
    [Test] public void GlowingObject_IsBaked() => Assert.IsTrue(GiBaker.ShouldBake(Make(2).GetComponent<Light>()));
    [Test] public void Flashlight_IsNotBaked() => Assert.IsFalse(GiBaker.ShouldBake(Make(0).GetComponent<Light>()));

    [Test]
    public void NoCategory_IsNotBaked()
    {
        var g = new GameObject("L", typeof(Light)); made.Add(g);
        Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>()));
    }

    [Test] public void Flicker_IsNotBaked() => Assert.IsFalse(GiBaker.ShouldBake(Make(1, typeof(FlickerLight)).GetComponent<Light>()));

    [Test]
    public void Excluded_IsNotBaked()
    {
        var g = Make(1);
        g.GetComponent<LightCategory>().excludeFromBake = true;
        Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>()));
    }

    [Test]
    public void UnderSpinLight_IsNotBaked()
    {
        var g = Make(2);
        g.transform.SetParent(Parent(typeof(SpinLight)).transform);
        Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>()));
    }

    [Test]
    public void UnderHangingSway_IsNotBaked()
    {
        var g = Make(1);
        g.transform.SetParent(Parent(typeof(HangingSway)).transform);
        Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>()));
    }
}
