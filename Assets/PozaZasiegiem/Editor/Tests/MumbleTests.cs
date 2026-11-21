using NUnit.Framework;
using UnityEngine;

/// <summary>Postacie „mówią” mamrotaniem (bez aktorów głosowych): długość zależy od tekstu,
/// wysokość od postaci, ten sam tekst brzmi zawsze tak samo.</summary>
public class MumbleTests
{
    static float[] Samples(AudioClip c)
    {
        var d = new float[c.samples];
        c.GetData(d, 0);
        return d;
    }

    [Test]
    public void LongerText_GivesLongerMumble()
    {
        var shortC = ProceduralAudio.Mumble("Hej.", 130f, 5f);
        var longC = ProceduralAudio.Mumble("Nagrywasz? Dobra... to tutaj. Stary magazyn, podobno cała zmiana zniknęła.", 130f, 5f);
        Assert.Greater(longC.length, shortC.length);
    }

    [Test]
    public void Mumble_NeverExceedsMaxDuration()
    {
        var c = ProceduralAudio.Mumble(new string('a', 500), 130f, 2f);
        Assert.LessOrEqual(c.length, 2f + 0.01f);
    }

    [Test]
    public void Mumble_IsAudible()
    {
        float peak = 0f;
        foreach (var s in Samples(ProceduralAudio.Mumble("Ciii... słyszysz?", 220f, 5f))) peak = Mathf.Max(peak, Mathf.Abs(s));
        Assert.Greater(peak, 0.1f);
        Assert.LessOrEqual(peak, 1f);
    }

    [Test]
    public void SameText_SoundsTheSame()
    {
        var a = Samples(ProceduralAudio.Mumble("Telefon... brak zasięgu.", 140f, 5f));
        var b = Samples(ProceduralAudio.Mumble("Telefon... brak zasięgu.", 140f, 5f));
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void EmptyText_GivesNoClip() => Assert.IsNull(ProceduralAudio.Mumble("", 140f, 5f));
}
