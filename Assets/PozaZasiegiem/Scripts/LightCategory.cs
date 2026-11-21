using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kategoria światła – ustawienia cieni, optymalizacja (LightCuller) i awaria zasilania (Blackout).
/// 0 = latarka gracza, 1 = lampy sufitowe, 2 = świecące obiekty (TV, automaty, lodówki, znaki...).
/// Cienie: Niskie = tylko 0, Średnie = 0-1, Wysokie = wszystkie.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightCategory : MonoBehaviour
{
    public static readonly List<LightCategory> All = new List<LightCategory>();
    public static int ShadowLevel = 2;

    public int category = 1;
    [Tooltip("Zasilane z sieci – gaśnie przy awarii prądu (Blackout)")]
    public bool powered = true;
    [Tooltip("Nie wyłączaj przez optymalizację (np. światła w przerywnikach)")]
    public bool neverCull;

    public Light Light => l;
    /// <summary>LightCuller: cień niepotrzebny (światło za ścianą / za plecami).</summary>
    [System.NonSerialized] public bool shadowCulled;
    /// <summary>LightCuller: światło za daleko – cały obiekt wyłączony.</summary>
    [System.NonSerialized] public bool farCulled;
    /// <summary>Wyłączone fabularnie (np. światła tunelu przed pościgiem).</summary>
    [System.NonSerialized] public bool scriptedOff;

    Light l;
    LightShadows original;
    bool registered;

    void Awake()
    {
        l = GetComponent<Light>();
        original = l.shadows;
        if (!registered) { All.Add(this); registered = true; }
        Apply();
    }

    void OnDestroy() => All.Remove(this);

    public void Apply()
    {
        if (l == null) return;
        int allowed = ShadowLevel <= 0 ? 0 : ShadowLevel == 1 ? 1 : 99;
        bool shadowOk = category <= allowed && !shadowCulled;
        l.shadows = shadowOk ? original : LightShadows.None;

        bool active = !farCulled && !scriptedOff && !(powered && Blackout.Active);
        if (gameObject.activeSelf != active) gameObject.SetActive(active);
    }

    public static void SetShadowLevel(int level)
    {
        ShadowLevel = level;
        ApplyAll();
    }

    public static void ApplyAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) All[i].Apply();
    }
}
