using System;
using UnityEngine;

/// <summary>
/// Globalny "hałas". Gracz (kroki, bieg, gaśnica) wywołuje Emit, Stalker nasłuchuje.
/// Możecie dodać hałas np. przy upuszczeniu przedmiotu: NoiseSystem.Emit(pozycja, 8f);
/// </summary>
public static class NoiseSystem
{
    public static event Action<Vector3, float> OnNoise;

    public static void Emit(Vector3 position, float radius)
    {
        if (radius <= 0f) return;
        OnNoise?.Invoke(position, radius);
    }
}
