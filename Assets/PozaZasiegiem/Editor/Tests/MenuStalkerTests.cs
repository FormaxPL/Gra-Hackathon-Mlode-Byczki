using NUnit.Framework;

/// <summary>Potwór w menu co 12–25 s losuje zachowanie, nigdy to samo dwa razy z rzędu.</summary>
public class MenuStalkerTests
{
    [Test]
    public void NextAct_NeverRepeats_AndUsesAllThree()
    {
        var r = new System.Random(11);
        var seen = new System.Collections.Generic.HashSet<MenuStalkerAct>();
        var last = MenuStalkerAct.StandFar;
        for (int i = 0; i < 1000; i++)
        {
            var a = MenuStalkerDirector.NextAct(last, r);
            Assert.AreNotEqual(last, a);
            seen.Add(a);
            last = a;
        }
        Assert.AreEqual(3, seen.Count);
    }

    [Test]
    public void NextDelay_Is12To25Seconds()
    {
        var r = new System.Random(12);
        for (int i = 0; i < 1000; i++) Assert.That(MenuStalkerDirector.NextDelay(r), Is.InRange(12f, 25f));
    }
}
