using UnityEngine;

/// <summary>
/// Plastikowe paski w przejściu do chłodni. Każdy pasek to wahadło zawieszone u góry:
/// gracz albo potwór rozpychają je przechodząc, a potem paski bujają się i wracają.
/// Lokalnie: oś X = szerokość przejścia, oś Z = kierunek przejścia, pivot = górna belka.
/// Kołyszące się paski zdradzają też, że coś właśnie przeszło...
/// </summary>
public class StripCurtain : MonoBehaviour
{
    [Tooltip("Zawiasy pasków (dzieci) – obracane wokół lokalnej osi X")]
    public Transform[] strips;
    public float stripHalfWidth = 0.075f;
    public float length = 2.4f;
    public float stiffness = 6f;
    public float damping = 1.3f;
    public float maxAngle = 78f;
    [Tooltip("Lekki przeciąg zimnego powietrza")]
    public float draft = 0.25f;

    float[] ang, vel, baseX;
    AudioSource src;
    AudioClip rustle;
    float nextRustle;

    void Awake()
    {
        int n = strips != null ? strips.Length : 0;
        ang = new float[n];
        vel = new float[n];
        baseX = new float[n];
        for (int i = 0; i < n; i++) baseX[i] = strips[i] != null ? strips[i].localPosition.x : 0f;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 15f;
        rustle = ProceduralAudio.PlasticRustle();
    }

    void Update()
    {
        if (strips == null || strips.Length == 0) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        var pc = PlayerController.Instance;
        if (pc != null && !pc.IsHidden) Push(pc.transform.position, pc.Velocity, 0.35f, 1.8f, dt);
        foreach (var s in StalkerAI.All)
            if (s != null && !s.InPipes) Push(s.transform.position, s.Velocity, 0.4f, 2.3f, dt);

        float maxRad = maxAngle * Mathf.Deg2Rad;
        for (int i = 0; i < strips.Length; i++)
        {
            if (strips[i] == null) continue;
            float flutter = (Mathf.PerlinNoise(Time.time * 0.7f + i * 0.37f, i * 1.3f) - 0.5f) * draft;
            float acc = -stiffness * Mathf.Sin(ang[i]) - damping * vel[i] + flutter;
            vel[i] += acc * dt;
            ang[i] = Mathf.Clamp(ang[i] + vel[i] * dt, -maxRad, maxRad);
            strips[i].localRotation = Quaternion.Euler(ang[i] * Mathf.Rad2Deg, 0f, Mathf.Sin(ang[i]) * 6f);
        }
    }

    void Push(Vector3 worldPos, Vector3 worldVel, float radius, float height, float dt)
    {
        Vector3 lp = transform.InverseTransformPoint(worldPos);
        if (Mathf.Abs(lp.z) > radius + 1.2f) return;
        if (lp.y > 0.3f || lp.y + height < -length) return;
        Vector3 lv = transform.InverseTransformDirection(worldVel);
        float hc = Mathf.Clamp(-(lp.y + height * 0.55f), 0.4f, length); // wysokość kontaktu pod belką
        bool hit = false;
        for (int i = 0; i < strips.Length; i++)
        {
            if (Mathf.Abs(lp.x - baseX[i]) > stripHalfWidth + radius) continue;
            float zAt = -hc * Mathf.Sin(ang[i]);
            float dz = zAt - lp.z;
            if (Mathf.Abs(dz) >= radius) continue;
            float side = Mathf.Abs(lv.z) > 0.15f ? Mathf.Sign(lv.z) : (dz >= 0f ? 1f : -1f);
            float targetZ = lp.z + side * radius;
            float target = -Mathf.Asin(Mathf.Clamp(targetZ / hc, -0.97f, 0.97f));
            vel[i] = Mathf.Clamp((target - ang[i]) / dt, -6f, 6f) * 0.6f;
            ang[i] = target;
            hit = true;
        }
        if (hit && Time.time >= nextRustle)
        {
            nextRustle = Time.time + 0.5f;
            src.pitch = Random.Range(0.85f, 1.15f);
            src.PlayOneShot(rustle, Mathf.Clamp01(0.35f + lv.magnitude * 0.15f));
        }
    }
}
