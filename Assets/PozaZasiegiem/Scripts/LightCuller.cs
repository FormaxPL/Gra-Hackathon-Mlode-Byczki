using UnityEngine;

/// <summary>
/// Optymalizacja świateł: karta liczy tylko to, co jest przed graczem.
///  - Światła daleko (poza mgłą) są całkiem wyłączane.
///  - Cienie liczą się tylko dla świateł, których obszar jest w kadrze i nie jest zasłonięty ścianą
///    (cień to najdroższa część światła). Światła tuż obok gracza zawsze mają cień.
/// Siatki (ściany, meble) ukrywa za nas Occlusion Culling wypalany przez generator poziomu.
/// </summary>
public class LightCuller : MonoBehaviour
{
    public float interval = 0.2f;
    [Tooltip("Dalej niż tyle (+ zasięg światła) światło jest wyłączone")]
    public float farDistance = 34f;
    [Tooltip("Bliżej niż tyle cień zawsze jest liczony")]
    public float alwaysShadowDistance = 7f;
    public bool cullingEnabled = true;

    readonly Plane[] planes = new Plane[6];
    float next;
    int cursor;

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + interval;
        var cam = Camera.main;
        if (cam == null) return;
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        Vector3 eye = cam.transform.position;
        var all = LightCategory.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var lc = all[i];
            if (lc == null || lc.category == 0 || lc.neverCull || lc.Light == null) continue;
            bool far, shadowCull;
            Evaluate(lc, eye, out far, out shadowCull);
            if (far != lc.farCulled || shadowCull != lc.shadowCulled)
            {
                lc.farCulled = far;
                lc.shadowCulled = shadowCull;
                lc.Apply();
            }
        }
    }

    void Evaluate(LightCategory lc, Vector3 eye, out bool far, out bool shadowCull)
    {
        far = false;
        shadowCull = false;
        if (!cullingEnabled) return;
        var l = lc.Light;
        Vector3 p = lc.transform.position;
        float range = l.range;
        float d = Vector3.Distance(eye, p);
        far = d > farDistance + range;
        if (far) { shadowCull = true; return; }
        if (d < alwaysShadowDistance) return;

        // obszar oświetlany: dla reflektora przesunięty w kierunku świecenia
        Vector3 center = l.type == LightType.Spot ? p + lc.transform.forward * range * 0.45f : p;
        var b = new Bounds(center, Vector3.one * range * 1.3f);
        if (!GeometryUtility.TestPlanesAABB(planes, b)) { shadowCull = true; return; }

        // czy widać lampę albo miejsce, na które świeci?
        if (!Blocked(eye, p)) return;
        Vector3 land = center;
        if (Physics.Raycast(p, lc.transform.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            land = hit.point - lc.transform.forward * 0.25f;
        if (!Blocked(eye, land)) return;
        shadowCull = true;
    }

    static bool Blocked(Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.5f) return false;
        return Physics.Raycast(a, dir / len, len - 0.35f, ~0, QueryTriggerInteraction.Ignore);
    }
}
