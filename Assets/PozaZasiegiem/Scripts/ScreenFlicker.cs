using UnityEngine;

/// <summary>Migotanie światła ekranu (stare telewizory/monitory CRT) – zmienia jasność i odcień.</summary>
[RequireComponent(typeof(Light))]
public class ScreenFlicker : MonoBehaviour
{
    public float speed = 9f;
    public float amount = 0.45f;

    Light l;
    float baseIntensity, seed;
    Color baseColor;

    void Awake()
    {
        l = GetComponent<Light>();
        baseIntensity = l.intensity;
        baseColor = l.color;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(Time.time * speed, seed);
        float jump = Random.value < 0.02f ? Random.Range(0.3f, 1.3f) : 1f;
        l.intensity = baseIntensity * Mathf.Lerp(1f - amount, 1f + amount * 0.5f, n) * jump;
        l.color = Color.Lerp(baseColor, baseColor * new Color(0.8f, 1f, 1.15f), Mathf.PerlinNoise(seed, Time.time * 2f));
    }
}
