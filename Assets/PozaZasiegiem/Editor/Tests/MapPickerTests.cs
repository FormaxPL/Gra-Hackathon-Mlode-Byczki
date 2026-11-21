using NUnit.Framework;

/// <summary>Przy „Rozpocznij grę” losowana jest jedna z gotowych map, mniej więcej równo.</summary>
public class MapPickerTests
{
    [Test]
    public void Pick_AlwaysReturnsOneOfTheMaps()
    {
        var r = new System.Random(7);
        for (int i = 0; i < 200; i++) CollectionAssert.Contains(MapPicker.Maps, MapPicker.Pick(r));
    }

    [Test]
    public void Pick_IsRoughlyUniform()
    {
        var r = new System.Random(1);
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < 3000; i++) { var m = MapPicker.Pick(r); counts[m] = (counts.TryGetValue(m, out int c) ? c : 0) + 1; }
        foreach (var m in MapPicker.Maps) Assert.That(counts[m], Is.InRange(800, 1200), m);
    }

    [Test] public void MainMenu_IsMenuScene() => Assert.IsTrue(MapPicker.IsMenuScene("MainMenu"));
    [Test] public void Map_IsNotMenuScene() => Assert.IsFalse(MapPicker.IsMenuScene("TestLevel_B"));
    [Test] public void ThreeMaps() => CollectionAssert.AreEqual(new[] { "TestLevel_A", "TestLevel_B", "TestLevel_C" }, MapPicker.Maps);
}
