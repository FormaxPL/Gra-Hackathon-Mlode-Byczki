using UnityEngine;

/// <summary>
/// Proceduralna animacja postaci z prostych brył (bez Animatora i bez modelu).
/// Używana przez ciało gracza (Human) i przez potwora (Creature).
/// Konwencja: każdy staw to pusty obiekt (pivot), a kość/bryła jest jego dzieckiem i "wisi" w dół (-Y).
/// Prędkość liczona jest z ruchu obiektu, więc działa z CharacterController, NavMeshAgent
/// i ruchem w wentylacji.
/// </summary>
public class ProceduralRig : MonoBehaviour
{
    public enum Style { Human, Creature }
    public Style style = Style.Human;

    [Header("Stawy")]
    public Transform hips;
    public Transform spine, neck, head;
    public Transform thighL, shinL, footL, thighR, shinR, footR;
    public Transform upperArmL, forearmL, upperArmR, forearmR;
    [Tooltip("Żuchwa (opcjonalnie) – otwiera się przy wrzasku")]
    public Transform jaw;

    [Header("Wymiary nóg (stopy trzymają się podłogi)")]
    public float thighLength = 0.46f;
    public float shinLength = 0.46f;
    public float footHeight = 0.07f;

    [Header("Chód")]
    public float strideLength = 1.4f;
    public float legSwing = 28f;
    public float kneeBend = 40f;
    public float armSwing = 22f;

    [Header("Postawa bazowa (stopnie)")]
    public float baseThigh = 0f;
    public float baseShin = 0f;
    public float baseFoot = 0f;
    public float baseSpine = 0f;
    public float baseUpperArm = 0f;
    public float baseForearm = -10f;
    public float armSpread = 6f;

    [HideInInspector] public float crouch;     // 0..1 (gracz)
    [HideInInspector] public float climb;      // 0..1 ręce w górę (wspinanie do wentylacji)
    [HideInInspector] public float reach;      // 0..1 ręce do przodu (pościg)
    [HideInInspector] public float headPitch;  // pochylenie głowy gracza
    [HideInInspector] public Transform lookTarget; // potwór patrzy na cel
    [HideInInspector] public float scream;     // 0..1 otwarta paszcza + drżenie głowy (jumpscare)
    [HideInInspector] public float headRoll;   // dodatkowy obrót głowy w stopniach (jumpscare)

    Vector3 lastPos;
    float phase, speed, twitchTimer, climbS, reachS;
    Quaternion twitch = Quaternion.identity;

    void OnEnable() => lastPos = transform.position;

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        if (Time.deltaTime <= 0f) return;

        Vector3 d = transform.position - lastPos;
        d.y = 0f;
        lastPos = transform.position;
        float instant = d.magnitude / dt;
        if (instant > 15f) instant = 0f; // teleport
        speed = Mathf.Lerp(speed, instant, dt * 8f);

        bool creature = style == Style.Creature;
        float amt = Mathf.Clamp01(speed / 1.5f);
        phase += speed / Mathf.Max(0.1f, strideLength) * Mathf.PI * dt;
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
        climbS = Mathf.MoveTowards(climbS, climb, dt * 3f);
        reachS = Mathf.MoveTowards(reachS, reach, dt * 2.5f);

        // ---------- NOGI ----------
        float crouchThigh = -75f * crouch, crouchShin = 110f * crouch;
        float tL = baseThigh - legSwing * amt * s + crouchThigh;
        float tR = baseThigh + legSwing * amt * s + crouchThigh;
        float kL = baseShin + kneeBend * amt * Mathf.Max(0f, c) + crouchShin;
        float kR = baseShin + kneeBend * amt * Mathf.Max(0f, -c) + crouchShin;
        SetX(thighL, tL); SetX(thighR, tR);
        SetX(shinL, kL); SetX(shinR, kR);
        // stopa mniej więcej równolegle do podłogi
        SetX(footL, baseFoot - (tL + kL) * 0.9f);
        SetX(footR, baseFoot - (tR + kR) * 0.9f);

        // Biodra na wysokości nogi podporowej => stopy na ziemi
        if (hips != null)
        {
            float h = Mathf.Max(LegExtent(tL, kL), LegExtent(tR, kR));
            Vector3 hp = hips.localPosition;
            hp.y = h;
            hips.localPosition = hp;
        }

        // ---------- TUŁÓW ----------
        float breathe = Mathf.Sin(Time.time * (creature ? 1.1f : 1.6f)) * (creature ? 3f : 1.2f);
        float lean = baseSpine + crouch * 22f + (creature ? Mathf.Lerp(0f, 18f, Mathf.Clamp01(speed / 4f)) : 4f * amt);
        if (spine != null)
            spine.localRotation = Quaternion.Euler(lean + breathe, s * 3f * amt, creature ? Mathf.Sin(phase * 0.5f) * 5f * amt : 0f);

        // ---------- RĘCE ----------
        float armL = baseUpperArm + armSwing * amt * s;
        float armR = baseUpperArm - armSwing * amt * s;
        if (creature)
        {
            float jitter = (Mathf.PerlinNoise(Time.time * 2.3f, 0.3f) - 0.5f) * 14f;
            armL += jitter;
            armR -= jitter;
        }
        float reachAngle = -80f, climbAngle = -165f;
        armL = Mathf.Lerp(Mathf.Lerp(armL, reachAngle + s * 10f, reachS), climbAngle, climbS);
        armR = Mathf.Lerp(Mathf.Lerp(armR, reachAngle - s * 10f, reachS), climbAngle, climbS);
        SetXZ(upperArmL, armL, -armSpread);
        SetXZ(upperArmR, armR, armSpread);
        float fore = baseForearm - 12f * amt * (0.5f + 0.5f * s);
        SetX(forearmL, Mathf.Lerp(fore, -25f, reachS));
        SetX(forearmR, Mathf.Lerp(baseForearm - 12f * amt * (0.5f - 0.5f * s), -25f, reachS));

        // ---------- GŁOWA ----------
        if (head == null) return;
        if (!creature)
        {
            head.localRotation = Quaternion.Euler(Mathf.Clamp(headPitch * 0.7f - lean * 0.5f, -60f, 60f), 0f, 0f);
            return;
        }

        twitchTimer -= dt;
        if (twitchTimer <= 0f)
        {
            twitchTimer = Random.Range(0.3f, 2.2f);
            twitch = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-35f, 35f), Random.Range(-45f, 45f));
        }
        Quaternion target = twitch;
        if (lookTarget != null && head.parent != null)
        {
            Vector3 to = lookTarget.position - head.position;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion world = Quaternion.LookRotation(to, Vector3.up);
                Quaternion local = Quaternion.Inverse(head.parent.rotation) * world;
                target = local * Quaternion.Euler(0f, 0f, twitch.eulerAngles.z * 0.3f);
            }
        }
        if (Mathf.Abs(headRoll) > 0.01f) target = target * Quaternion.Euler(0f, 0f, headRoll);
        if (scream > 0.01f)
            target = target * Quaternion.Euler(Random.Range(-7f, 7f) * scream, Random.Range(-9f, 9f) * scream, Random.Range(-6f, 6f) * scream);
        float follow = scream > 0.01f || Mathf.Abs(headRoll) > 0.01f ? 30f : (lookTarget != null ? 10f : 22f);
        head.localRotation = Quaternion.Slerp(head.localRotation, target, dt * follow);
        if (jaw != null)
            jaw.localRotation = Quaternion.Euler(scream * 38f + Mathf.Sin(Time.time * 37f) * 5f * scream, 0f, 0f);
    }

    float LegExtent(float thighDeg, float shinDeg)
    {
        float a1 = thighDeg * Mathf.Deg2Rad;
        float a2 = (thighDeg + shinDeg) * Mathf.Deg2Rad;
        return thighLength * Mathf.Cos(a1) + shinLength * Mathf.Cos(a2) + footHeight;
    }

    static void SetX(Transform t, float x)
    {
        if (t != null) t.localRotation = Quaternion.Euler(x, 0f, 0f);
    }

    static void SetXZ(Transform t, float x, float z)
    {
        if (t != null) t.localRotation = Quaternion.Euler(x, 0f, z);
    }
}
