using NUnit.Framework;

/// <summary>Losowe awarie: co 3–6 min, na 20–45 s, tylko w spokojnej eksploracji.</summary>
public class RandomBlackoutsTests
{
    [Test]
    public void Interval_Is3To6Minutes()
    {
        var r = new System.Random(3);
        for (int i = 0; i < 1000; i++) Assert.That(RandomBlackouts.NextInterval(r), Is.InRange(180f, 360f));
    }

    [Test]
    public void Duration_Is20To45Seconds()
    {
        var r = new System.Random(4);
        for (int i = 0; i < 1000; i++) Assert.That(RandomBlackouts.NextDuration(r), Is.InRange(20f, 45f));
    }

    [Test] public void Starts_InCalmExploration() => Assert.IsTrue(RandomBlackouts.CanStart(true, false, false, false, false));
    [Test] public void NotOutsideExploration() => Assert.IsFalse(RandomBlackouts.CanStart(false, false, false, false, false));
    [Test] public void NotDuringCutscene() => Assert.IsFalse(RandomBlackouts.CanStart(true, true, false, false, false));
    [Test] public void NotDuringAnotherBlackout() => Assert.IsFalse(RandomBlackouts.CanStart(true, false, true, false, false));
    [Test] public void NotWhileReadingOrInMenu() => Assert.IsFalse(RandomBlackouts.CanStart(true, false, false, true, false));
    [Test] public void NotWhileHiding() => Assert.IsFalse(RandomBlackouts.CanStart(true, false, false, false, true));
}
