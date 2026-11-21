using NUnit.Framework;

/// <summary>Przełączanie scenariuszy nie może nigdy wywrócić gry – także gdy scena nie ma wypalonych danych
/// (świeżo przebudowana generatorem) albo APV jest wyłączone.</summary>
public class GiScenarioTests
{
    [Test] public void PowerScenario_IsPrad() => Assert.AreEqual("Prad", GiScenario.Power);
    [Test] public void OutageScenario_IsAwaria() => Assert.AreEqual("Awaria", GiScenario.Outage);

    [Test] public void Set_NeverThrows() => Assert.DoesNotThrow(() => GiScenario.Set(GiScenario.Outage));

    [Test] public void Blend_NeverThrows() => Assert.DoesNotThrow(() => GiScenario.Blend(GiScenario.Power, 0.5f));

    [Test] public void Set_UnknownScenario_NeverThrows() => Assert.DoesNotThrow(() => GiScenario.Set("NieMaTakiego"));

    [Test]
    public void IsAvailable_DoesNotThrow()
    {
        bool _;
        Assert.DoesNotThrow(() => _ = GiScenario.IsAvailable);
    }
}
