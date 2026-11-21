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

    // ---------- które obiekty biorą udział w GI (statyczne, nieprzezroczyste, nieruchome) ----------

    Renderer Mesh(Shader shader, int queue = -1)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        made.Add(g);
        var m = new Material(shader);
        if (queue >= 0) m.renderQueue = queue;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        return r;
    }

    [Test] public void OpaqueStaticMesh_ContributesGI() => Assert.IsTrue(GiBaker.ContributesGI(Mesh(Shader.Find("Universal Render Pipeline/Lit"))));
    [Test] public void TransparentMesh_DoesNotContributeGI() => Assert.IsFalse(GiBaker.ContributesGI(Mesh(Shader.Find("Universal Render Pipeline/Lit"), 3000)));

    [Test]
    public void Particles_DoNotContributeGI()
    {
        var g = new GameObject("P", typeof(ParticleSystem)); made.Add(g);
        Assert.IsFalse(GiBaker.ContributesGI(g.GetComponent<ParticleSystemRenderer>()));
    }

    [Test]
    public void SkinnedMesh_DoesNotContributeGI()
    {
        var g = new GameObject("P", typeof(SkinnedMeshRenderer)); made.Add(g);
        Assert.IsFalse(GiBaker.ContributesGI(g.GetComponent<SkinnedMeshRenderer>()));
    }

    [Test]
    public void MeshUnderIdleSway_DoesNotContributeGI()
    {
        var r = Mesh(Shader.Find("Universal Render Pipeline/Lit"));
        r.transform.SetParent(Parent(typeof(IdleSway)).transform);
        Assert.IsFalse(GiBaker.ContributesGI(r));
    }

    [Test]
    public void MeshUnderPickup_DoesNotContributeGI()
    {
        var r = Mesh(Shader.Find("Universal Render Pipeline/Lit"));
        r.transform.SetParent(Parent(typeof(Pickup)).transform);
        Assert.IsFalse(GiBaker.ContributesGI(r));
    }

    // ---------- scenariusz „Awaria”: które wypalane światła gasną ----------

    Light BakedLamp(bool powered)
    {
        var g = Make(1);
        g.GetComponent<LightCategory>().powered = powered;
        var l = g.GetComponent<Light>();
        l.lightmapBakeType = LightmapBakeType.Mixed;
        return l;
    }

    [Test] public void PoweredMixedLamp_IsOffInOutage() => Assert.IsTrue(GiBaker.OffInOutage(BakedLamp(true)));
    [Test] public void EmergencyLight_StaysOnInOutage() => Assert.IsFalse(GiBaker.OffInOutage(BakedLamp(false)));

    [Test]
    public void RealtimeLight_IsNotTouchedByOutageBake()
    {
        var l = BakedLamp(true);
        l.lightmapBakeType = LightmapBakeType.Realtime;
        Assert.IsFalse(GiBaker.OffInOutage(l));
    }

    [Test]
    public void LampUnderBlackoutImmune_StaysOnInOutage()
    {
        var l = BakedLamp(true);
        l.transform.SetParent(Parent(typeof(BlackoutImmune)).transform);
        Assert.IsFalse(GiBaker.OffInOutage(l));
    }

    // ---------- przebudowa mapy usuwa stare dane wypalania (leżą obok zestawu, nie w podfolderze) ----------

    [Test]
    public void DeleteBakingSet_RemovesSetAndItsDataFiles_KeepsOthers()
    {
        const string dir = "Assets/PozaZasiegiem/Editor/Tests/_gitmp";
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/PozaZasiegiem/Editor/Tests", "_gitmp");
        try
        {
            string set = dir + "/S.asset";
            UnityEditor.AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UnityEngine.Rendering.ProbeVolumeBakingSet>(), set);
            foreach (var f in new[] { "S-Prad.CellData.bytes", "S.CellSharedData.bytes", "Other.bytes" })
                System.IO.File.WriteAllBytes(dir + "/" + f, new byte[] { 1 });
            UnityEditor.AssetDatabase.Refresh();

            GiBaker.DeleteBakingSet(set);

            Assert.IsFalse(System.IO.File.Exists(set));
            Assert.IsFalse(System.IO.File.Exists(dir + "/S-Prad.CellData.bytes"));
            Assert.IsFalse(System.IO.File.Exists(dir + "/S.CellSharedData.bytes"));
            Assert.IsTrue(System.IO.File.Exists(dir + "/Other.bytes"));
        }
        finally { UnityEditor.AssetDatabase.DeleteAsset(dir); }
    }

    [Test]
    public void UnderHangingSway_IsNotBaked()
    {
        var g = Make(1);
        g.transform.SetParent(Parent(typeof(HangingSway)).transform);
        Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>()));
    }
}
