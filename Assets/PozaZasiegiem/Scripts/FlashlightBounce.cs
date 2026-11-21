using UnityEngine;

/// <summary>
/// Pozorne odbicie światła latarki: słabe światło punktowe tuż przed miejscem, w które trafia snop,
/// w kolorze trafionej powierzchni. Latarka jest dynamiczna, więc nie ma jej w wypalonym GI – to tani zamiennik.
/// </summary>
[RequireComponent(typeof(Flashlight))]
public class FlashlightBounce : MonoBehaviour
{
    [Tooltip("Jasność odbłysku przy trafieniu tuż przed latarką. Wyżej źródło punktowe świeci w mgle AERO jak kula.")]
    public float baseIntensity = 0.35f;
    [Tooltip("Zasięg światła odbłysku (m)")]
    public float bounceRange = 3f;

    Flashlight flashlight;
    Light bounce;
    readonly RaycastHit[] hits = new RaycastHit[8];
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        flashlight = GetComponent<Flashlight>();
        var go = new GameObject("Odbłysk latarki (GI)");
        go.transform.SetParent(transform, false);
        bounce = go.AddComponent<Light>();
        bounce.type = LightType.Point;
        bounce.range = bounceRange;
        bounce.shadows = LightShadows.None;
        bounce.lightmapBakeType = LightmapBakeType.Realtime;
        bounce.enabled = false;
        var lc = go.AddComponent<LightCategory>();
        lc.category = 0;
        lc.powered = false;
        lc.excludeFromBake = true;
    }

    void LateUpdate()
    {
        bool on = flashlight != null && flashlight.IsOn && flashlight.Range > 0f;
        if (on && TryHit(out RaycastHit hit))
        {
            float power = Blackout.Active && Blackout.Instance != null ? Blackout.Instance.flashlightPower : 1f;
            bounce.intensity = Intensity(hit.distance, flashlight.Range, baseIntensity) * power;
            bounce.color = Tint(Color.white, SurfaceColor(hit));
            bounce.transform.position = Placement(hit.point, hit.normal, -transform.forward, hit.distance);
            bounce.enabled = bounce.intensity > 0.001f;
        }
        else bounce.enabled = false;
    }

    bool TryHit(out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(transform.position, transform.forward, hits, flashlight.Range, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = hits[i]; }
        }
        return bestDist < float.MaxValue;
    }

    static Color SurfaceColor(RaycastHit hit)
    {
        var r = hit.collider.GetComponent<Renderer>();
        var m = r != null ? r.sharedMaterial : null;
        return m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : new Color(0.5f, 0.5f, 0.5f);
    }

    /// <summary>Jasność odbłysku: pełna tuż przed latarką, liniowo do zera na końcu zasięgu latarki.</summary>
    public static float Intensity(float hitDistance, float range, float baseIntensity)
    {
        if (range <= 0f) return 0f;
        return baseIntensity * Mathf.Clamp01(1f - hitDistance / range);
    }

    /// <summary>Kolor odbłysku: światło latarki × kolor powierzchni, rozjaśniony (min. 15% światła nawet od czerni).</summary>
    public static Color Tint(Color lightColor, Color surface)
    {
        var lifted = Color.Lerp(surface, Color.white, 0.15f);
        var c = lightColor * lifted;
        c.a = 1f;
        return c;
    }

    /// <summary>Położenie odbłysku: przed powierzchnią wzdłuż normalnej, 0,3 m, ale przy bliskiej ścianie
    /// najwyżej połowa odległości trafienia (nie może wyjść za kamerę).</summary>
    public static Vector3 Placement(Vector3 hitPoint, Vector3 normal, Vector3 toCamera, float hitDistance)
    {
        var n = Vector3.Dot(normal, toCamera) >= 0f ? normal : -normal; // zawsze po stronie kamery
        return hitPoint + n.normalized * Mathf.Min(0.3f, hitDistance * 0.5f);
    }
}
