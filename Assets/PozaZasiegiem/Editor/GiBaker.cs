using UnityEngine;

/// <summary>
/// Oświetlenie globalne (Adaptive Probe Volumes): które światła i obiekty biorą udział w wypalaniu,
/// przygotowanie sceny i wypalanie scenariuszy „Prad” / „Awaria”.
/// </summary>
public static class GiBaker
{
    // obiekty ruchome / sterowane skryptem – ich światło nie może być wypalone (odbicie zostałoby w miejscu)
    static readonly System.Type[] MovingOrScripted =
    {
        typeof(FlickerLight), typeof(SpinLight), typeof(HangingSway), typeof(IdleSway), typeof(SwingDoor),
        typeof(StripCurtain), typeof(PlayerController), typeof(StalkerAI), typeof(Pickup), typeof(Note), typeof(PipeHatch),
    };

    /// <summary>Światło jest Mixed (odbicia wypalone, bezpośrednie i cienie na żywo): stała lampa (kat. 1)
    /// albo świecący obiekt (kat. 2), nie wykluczone i nie pod niczym ruchomym ani fabularnym.</summary>
    public static bool ShouldBake(Light l)
    {
        if (l == null) return false;
        var lc = l.GetComponent<LightCategory>();
        if (lc == null || lc.excludeFromBake || (lc.category != 1 && lc.category != 2)) return false;
        return !UnderAny(l.transform, MovingOrScripted);
    }

    static bool UnderAny(Transform t, System.Type[] types)
    {
        foreach (var type in types)
            if (t.GetComponentInParent(type, true) != null) return true;
        return false;
    }
}
