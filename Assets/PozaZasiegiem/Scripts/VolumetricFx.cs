using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Znacznik efektów wolumetrycznych (snopy światła, mgła, kurz w powietrzu).
/// Ustawienia mogą je wyłączyć na słabszych komputerach.
/// </summary>
public class VolumetricFx : MonoBehaviour
{
    public static readonly List<VolumetricFx> All = new List<VolumetricFx>();
    public static bool Enabled = true;

    void Awake()
    {
        All.Add(this);
        if (!Enabled) gameObject.SetActive(false);
    }

    void OnDestroy() => All.Remove(this);

    public static void SetEnabled(bool on)
    {
        Enabled = on;
        foreach (var v in All) if (v != null) v.gameObject.SetActive(on);
    }
}
