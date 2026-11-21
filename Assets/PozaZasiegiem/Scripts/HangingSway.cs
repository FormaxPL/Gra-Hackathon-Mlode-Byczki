using UnityEngine;

/// <summary>Delikatne bujanie wiszących rzeczy (tusze w chłodni, lampy).</summary>
public class HangingSway : MonoBehaviour
{
    public float angle = 4f;
    public float speed = 0.7f;

    float seed;
    Quaternion baseRot;

    void Awake()
    {
        seed = Random.value * 100f;
        baseRot = transform.localRotation;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * angle, Mathf.Sin(t * 0.37f) * angle * 2f, Mathf.Cos(t * 0.8f) * angle);
    }
}
