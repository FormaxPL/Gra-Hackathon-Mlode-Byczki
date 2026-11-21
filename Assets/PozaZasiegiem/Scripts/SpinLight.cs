using UnityEngine;

/// <summary>Obracający się element (kogut alarmowy, wirujący portal...).</summary>
public class SpinLight : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 220f;
    [Tooltip("Pulsowanie jasności światła na tym obiekcie (0 = brak)")]
    public float pulse;

    Light l;
    float baseIntensity;

    void Awake()
    {
        l = GetComponentInChildren<Light>();
        if (l != null) baseIntensity = l.intensity;
    }

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        if (l != null && pulse > 0f) l.intensity = baseIntensity * (1f - pulse * 0.5f + pulse * 0.5f * Mathf.Sin(Time.time * 6f));
    }
}
