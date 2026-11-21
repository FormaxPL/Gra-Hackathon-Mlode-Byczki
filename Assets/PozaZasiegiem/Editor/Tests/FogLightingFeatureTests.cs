using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Kopia renderera mgły nie może zawierać skryptów z paczki AERO (nie ma jej w repozytorium),
/// bo bez AERO URP zgłasza „missing RendererFeatures”. Feature oświetlenia AERO zastępujemy naszym.</summary>
public class FogLightingFeatureTests
{
    const string Dir = "Assets/PozaZasiegiem/Editor/Tests/_tmp";
    const string Path = Dir + "/TestRenderer.asset";
    Material mat;

    [SetUp]
    public void Make()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/PozaZasiegiem/Editor/Tests", "_tmp");
        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    }

    [TearDown]
    public void Clean()
    {
        AssetDatabase.DeleteAsset(Dir);
        Object.DestroyImmediate(mat);
    }

    UniversalRendererData RendererWith(System.Type featureType)
    {
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rd, Path);
        var f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
        f.name = featureType.Name;
        AssetDatabase.AddObjectToAsset(f, rd);
        var so = new SerializedObject(rd);
        var list = so.FindProperty("m_RendererFeatures");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = f;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long id);
        var map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize = 1;
        map.GetArrayElementAtIndex(0).longValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return rd;
    }

    [Test]
    public void FogLightingFeature_IsRendererFeature()
    {
        Assert.IsTrue(typeof(ScriptableRendererFeature).IsAssignableFrom(typeof(FogLightingFeature)));
    }

    [Test]
    public void AeroLightingFeature_IsReplacedByOurs()
    {
        var aeroType = System.Type.GetType("Mirza.AERO.FullscreenCustomLightingFeature, Assembly-CSharp");
        if (aeroType == null) Assert.Ignore("AERO nie jest zaimportowany");
        var rd = RendererWith(aeroType);

        Assert.AreEqual(1, LevelBuilder.ReplaceAeroLightingFeature(rd, mat));

        Assert.AreEqual(1, rd.rendererFeatures.Count);
        var ours = rd.rendererFeatures[0] as FogLightingFeature;
        Assert.IsNotNull(ours);
        Assert.AreSame(mat, ours.material);
        Assert.IsFalse(AeroFog.HasMissingFeatures(rd));
        var so = new SerializedObject(rd);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ours, out string _, out long id);
        Assert.AreEqual(id, so.FindProperty("m_RendererFeatureMap").GetArrayElementAtIndex(0).longValue);
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(Path))
            Assert.AreNotEqual(aeroType, sub.GetType(), "stary feature AERO został w assecie");
    }

    [Test]
    public void OtherFeatures_AreLeftAlone()
    {
        var rd = RendererWith(typeof(FullScreenPassRendererFeature));
        Assert.AreEqual(0, LevelBuilder.ReplaceAeroLightingFeature(rd, mat));
        Assert.IsInstanceOf<FullScreenPassRendererFeature>(rd.rendererFeatures[0]);
    }
}
