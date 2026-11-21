using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Generator wybiera renderer AERO w wersji PC i zmienia tylko assety URP, których gra faktycznie używa.</summary>
public class LevelBuilderAeroTests
{
    [Test]
    public void PcRenderer_ScoresHigherThanMobile()
    {
        int pc = LevelBuilder.ScoreAeroRenderer("Assets/Mirza/AERO - Volumetric Fog and Mist/Settings/PC_Renderer.asset", true);
        int mobile = LevelBuilder.ScoreAeroRenderer("Assets/Mirza/AERO - Volumetric Fog and Mist/Settings/Mobile_Renderer.asset", true);
        Assert.Greater(pc, mobile);
    }

    [Test]
    public void RendererWithLightingFeature_ScoresHigher()
    {
        Assert.Greater(LevelBuilder.ScoreAeroRenderer("Assets/X/PC_Renderer.asset", true),
                       LevelBuilder.ScoreAeroRenderer("Assets/X/PC_Renderer.asset", false));
    }

    [Test]
    public void UsedPipelineAssets_ContainCurrentPipeline()
    {
        var used = LevelBuilder.UsedPipelineAssets();
        Assert.Contains(GraphicsSettings.currentRenderPipeline, used);
    }

    [Test]
    public void UsedPipelineAssets_SkipThirdPartyCopies()
    {
        foreach (var a in LevelBuilder.UsedPipelineAssets())
            StringAssert.DoesNotStartWith("Assets/Mirza", UnityEditor.AssetDatabase.GetAssetPath(a));
    }
}
