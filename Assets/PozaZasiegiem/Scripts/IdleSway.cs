using UnityEngine;

/// <summary>Delikatne "oddychanie" postaci tła (agenci, przyjaciele w przerywnikach).</summary>
public class IdleSway : MonoBehaviour
{
    public float amount = 2f;
    public float speed = 1.2f;
    Quaternion rest;
    float seed;

    void Awake()
    {
        rest = transform.localRotation;
        seed = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t) * amount * 0.5f, Mathf.Sin(t * 0.37f) * amount, Mathf.Sin(t * 0.71f) * amount * 0.4f);
    }
}
