using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scenariusze wypalonego oświetlenia globalnego (Adaptive Probe Volumes): z prądem i w czasie awarii.
/// Jedyne miejsce z nazwami scenariuszy – używa ich i edytor (wypalanie), i gra (awaria prądu).
/// Każda metoda jest bezpieczna: bez wypalonych danych albo bez APV po prostu nic nie robi.
/// </summary>
public static class GiScenario
{
    public const string Power = "Prad";
    public const string Outage = "Awaria";

    /// <summary>APV działa i ma wczytane dane wypalania.</summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                var r = ProbeReferenceVolume.instance;
                return r != null && r.isInitialized && r.currentBakingSet != null && r.DataHasBeenLoaded();
            }
            catch { return false; }
        }
    }

    static bool Has(string scenario)
    {
        var set = ProbeReferenceVolume.instance.currentBakingSet;
        if (set == null) return false;
        foreach (var s in set.lightingScenarios) if (s == scenario) return true;
        return false;
    }

    /// <summary>Natychmiast przełącza na scenariusz (np. razem z mrugnięciem lamp).</summary>
    public static void Set(string scenario)
    {
        try
        {
            if (!IsAvailable || !Has(scenario)) return;
            var r = ProbeReferenceVolume.instance;
            if (r.lightingScenario != scenario) r.lightingScenario = scenario;
            r.BlendLightingScenario(null, 0f);
        }
        catch (System.Exception e) { Debug.LogWarning("[GI] " + e.Message); }
    }

    /// <summary>Mieszanie bieżącego scenariusza z innym: factor 0 = bieżący, 1 = <paramref name="to"/>.</summary>
    public static void Blend(string to, float factor)
    {
        try
        {
            if (!IsAvailable || !Has(to)) return;
            ProbeReferenceVolume.instance.BlendLightingScenario(to, Mathf.Clamp01(factor));
        }
        catch (System.Exception e) { Debug.LogWarning("[GI] " + e.Message); }
    }
}
