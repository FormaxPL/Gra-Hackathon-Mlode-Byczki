using NUnit.Framework;
using UnityEngine;

/// <summary>Trailer (20–30 s, jumpscare na końcu, potem tytuł), komendy konsoli i straszny ambient.</summary>
public class TrailerTests
{
    static float[] Samples(AudioClip c)
    {
        var d = new float[c.samples];
        c.GetData(d, 0);
        return d;
    }

    static float Peak(AudioClip c)
    {
        float p = 0f;
        foreach (var s in Samples(c)) p = Mathf.Max(p, Mathf.Abs(s));
        return p;
    }

    [Test]
    public void Trailer_LastsBetween20And30Seconds()
    {
        Assert.GreaterOrEqual(TrailerDirector.TotalLength, 20f);
        Assert.LessOrEqual(TrailerDirector.TotalLength, 30f);
    }

    [Test]
    public void Jumpscare_IsAtTheEnd_FollowedOnlyByBlackAndTitle()
    {
        Assert.AreEqual(TrailerDirector.Shots.Length - 3, TrailerDirector.HeroShot);
        float heroEnd = TrailerDirector.ShotStart(TrailerDirector.HeroShot + 1);
        Assert.Greater(heroEnd / TrailerDirector.TotalLength, 0.75f);
    }

    [Test]
    public void Keys_InterpolatesAndClamps()
    {
        float[] t = { 0f, 1f, 2f };
        float[] v = { 0f, 10f, -10f };
        Assert.AreEqual(0f, TrailerDirector.Keys(-1f, t, v));
        Assert.AreEqual(10f, TrailerDirector.Keys(1f, t, v), 0.001f);
        Assert.AreEqual(5f, TrailerDirector.Keys(0.5f, t, v), 0.001f);
        Assert.AreEqual(-10f, TrailerDirector.Keys(5f, t, v));
    }

    [Test]
    public void LaunchArgument_StartsTrailer()
    {
        Assert.IsTrue(DevConsole.WantsTrailerOnLaunch(new[] { "Gra.exe", "-trailer" }));
        Assert.IsTrue(DevConsole.WantsTrailerOnLaunch(new[] { "Gra.exe", "-TRAILER" }));
        Assert.IsFalse(DevConsole.WantsTrailerOnLaunch(new[] { "Gra.exe", "-screen-width", "1920" }));
        Assert.IsFalse(DevConsole.WantsTrailerOnLaunch(null));
    }

    [Test]
    public void AmbientArgument_Parses()
    {
        Assert.IsTrue(DevConsole.ParseAmbientArg("off", out bool? en, out float? vol));
        Assert.AreEqual(false, en);
        Assert.IsNull(vol);
        Assert.IsTrue(DevConsole.ParseAmbientArg("0,4", out en, out vol));
        Assert.AreEqual(0.4f, vol.Value, 0.0001f);
        Assert.IsFalse(DevConsole.ParseAmbientArg("glosno", out _, out _));
        Assert.IsFalse(DevConsole.ParseAmbientArg("3", out _, out _));
    }

    [Test]
    public void Console_EmptyAndUnknownCommands()
    {
        Assert.AreEqual("", DevConsole.Execute("   "));
        StringAssert.Contains("help", DevConsole.Execute("xyz"));
        StringAssert.Contains("trailer", DevConsole.Execute("help"));
    }

    [Test]
    public void CreepyAmbience_IsAudible_AndLoopsWithoutClick()
    {
        var c = ProceduralAudio.CreepyAmbience(6f);
        var d = Samples(c);
        Assert.Greater(Peak(c), 0.1f);
        Assert.LessOrEqual(Peak(c), 1f);
        Assert.Less(Mathf.Abs(d[0] - d[d.Length - 1]), 0.05f);
    }

    [Test]
    public void AmbientEvents_AreAudible_AndHaveTailForReverb()
    {
        foreach (var c in new[] { ProceduralAudio.DistantKnocks(11), ProceduralAudio.MetalGroan(5), ProceduralAudio.Whisper(3, 2f) })
        {
            var d = Samples(c);
            Assert.Greater(Peak(c), 0.05f, c.name);
            Assert.LessOrEqual(Peak(c), 1f, c.name);
            // ostatnie 0.5 s to cisza – pogłos ma czas wybrzmieć
            int from = d.Length - c.frequency / 2;
            float tail = 0f;
            for (int i = from; i < d.Length; i++) tail = Mathf.Max(tail, Mathf.Abs(d[i]));
            Assert.Less(tail, 0.02f, c.name);
        }
    }

    [Test]
    public void TitleBoom_IsLoud()
    {
        Assert.Greater(Peak(ProceduralAudio.TitleBoom()), 0.5f);
    }
}
