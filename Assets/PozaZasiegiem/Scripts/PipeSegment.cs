using UnityEngine;

/// <summary>Odcinek rury, który drży, gdy coś się w nim przeciska.</summary>
public class PipeSegment : MonoBehaviour
{
    [Tooltip("Końce odcinka w świecie (oś rury)")]
    public Vector3 a, b;

    Vector3 basePos;
    float shake;
    bool init;

    void Awake()
    {
        basePos = transform.localPosition;
        init = true;
        enabled = false; // Update tylko podczas drżenia
    }

    public void Shake(float amount)
    {
        if (!init) { basePos = transform.localPosition; init = true; }
        shake = Mathf.Clamp01(Mathf.Max(shake, amount));
        enabled = true;
    }

    void Update()
    {
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
        if (shake <= 0.001f)
        {
            transform.localPosition = basePos;
            enabled = false;
            return;
        }
        transform.localPosition = basePos + Random.insideUnitSphere * 0.025f * shake;
    }

    public float DistanceTo(Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
        return Vector3.Distance(p, a + ab * t);
    }
}
