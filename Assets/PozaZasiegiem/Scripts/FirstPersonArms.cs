using UnityEngine;

/// <summary>
/// Ręce w widoku z pierwszej osoby: prawa trzyma latarkę, lewa wykrywacz ruchu (Tab/Q),
/// gaśnicę albo kamerę (PPM). Bujają się przy chodzeniu i "spóźniają" za ruchem myszy.
/// HandInteraction może przejąć lewą rękę (podnoszenie przedmiotów, trzymanie notatki).
/// </summary>
public class FirstPersonArms : MonoBehaviour
{
    public Transform rightArm;
    public Transform leftArm;
    public GameObject trackerModel;
    public GameObject extinguisherModel;
    public GameObject camcorderModel;
    public ParticleSystem spray;

    [Header("Pozycje lewej ręki (lokalnie względem kamery)")]
    public Vector3 leftHiddenPos = new Vector3(-0.3f, -0.8f, 0.2f);
    public Vector3 leftTrackerPos = new Vector3(-0.12f, -0.25f, 0.36f);
    public Vector3 leftTrackerRot = new Vector3(-50f, 12f, 0f);
    public Vector3 leftWeaponPos = new Vector3(-0.24f, -0.34f, 0.42f);
    public Vector3 leftWeaponRot = new Vector3(0f, 6f, 0f);
    public Vector3 leftCamPos = new Vector3(-0.02f, -0.08f, 0.16f);
    public Vector3 leftCamRot = new Vector3(0f, 0f, 0f);

    [HideInInspector] public float rightLower; // 0..1 – opuszczenie prawej ręki (np. przy czytaniu)

    PlayerController pc;
    MotionTracker tracker;
    Camcorder camcorder;
    Vector3 rightBasePos;
    Quaternion rightBaseRot;
    Vector2 sway;
    float bobT, kick, run01, lean;
    bool leftOverride;
    Vector3 overridePos;
    Quaternion overrideRot = Quaternion.identity;

    void Awake()
    {
        pc = GetComponentInParent<PlayerController>();
        tracker = GetComponentInParent<MotionTracker>();
        camcorder = GetComponentInParent<Camcorder>();
        if (rightArm != null) { rightBasePos = rightArm.localPosition; rightBaseRot = rightArm.localRotation; }
        if (leftArm != null) leftArm.localPosition = leftHiddenPos;
        if (camcorderModel != null) camcorderModel.SetActive(false);
    }

    public void PlaySpray()
    {
        kick = 1f;
        if (spray != null) spray.Play();
    }

    public void SetLeftOverride(Vector3 localPos, Quaternion localRot)
    {
        leftOverride = true;
        overridePos = localPos;
        overrideRot = localRot;
    }

    public void ClearLeftOverride() => leftOverride = false;

    void LateUpdate()
    {
        if (pc == null) return;
        float dt = Time.deltaTime;
        bool hidden = pc.IsHidden;
        float camUp = camcorder != null ? camcorder.Raise01 : 0f;
        bool viewing = camUp > 0.95f;

        Vector2 look = (!GameManager.Blocked && Cursor.lockState == CursorLockMode.Locked) ? GameInput.Look : Vector2.zero;
        sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(-look * 0.004f, 0.05f), dt * 8f);

        float spd = pc.PlanarSpeed;
        bobT += dt * spd * 2.2f;
        float k = Mathf.Clamp01(spd / 3f);
        Vector3 bob = new Vector3(Mathf.Cos(bobT * 0.5f) * 0.012f, Mathf.Sin(bobT) * 0.01f, 0f) * k;
        run01 = Mathf.MoveTowards(run01, pc.IsRunning ? 1f : 0f, dt * 4f);
        bob *= 1f + run01 * 1.2f;
        // w biegu ręce pracują naprzemiennie (pompowanie) i chowają się lekko w dół
        float pump = Mathf.Sin(bobT) * run01;
        Vector3 runR = new Vector3(0.02f * run01, -0.05f * run01 + pump * 0.045f, pump * 0.05f);
        Vector3 runL = new Vector3(-0.02f * run01, -0.05f * run01 - pump * 0.045f, -pump * 0.05f);
        lean = Mathf.Lerp(lean, -GameInput.Move.x * 4f, dt * 6f);
        // oddech / zmęczenie – delikatne unoszenie rąk w spoczynku
        float breathe = Mathf.Sin(Time.time * 1.1f * (1f + pc.Exertion * 0.4f)) * 0.003f * (1f + pc.Exertion);
        bob += new Vector3(0f, breathe, 0f);
        kick = Mathf.MoveTowards(kick, 0f, dt * 2.5f);
        Vector3 swayV = new Vector3(sway.x, sway.y, 0f);

        // ---------- prawa ręka (latarka) ----------
        if (rightArm != null)
        {
            Vector3 target = rightBasePos + swayV + bob + runR;
            if (hidden || viewing) target += Vector3.down * 0.6f;
            else target += Vector3.down * (0.3f * camUp + 0.2f * rightLower);
            rightArm.localPosition = Vector3.Lerp(rightArm.localPosition, target, dt * 12f);
            rightArm.localRotation = rightBaseRot * Quaternion.Euler(sway.y * 120f + pump * 12f, -sway.x * 120f, lean + pump * 4f);
        }

        // ---------- lewa ręka ----------
        if (leftArm == null) return;
        bool showCam = !hidden && camUp > 0.01f && !viewing;
        bool free = !hidden && camUp <= 0.01f && !leftOverride;
        bool showTracker = free && tracker != null && tracker.Active;
        bool showWeapon = free && !showTracker && pc.Inventory.hasWeapon;
        SetActive(camcorderModel, showCam);
        SetActive(trackerModel, showTracker);
        SetActive(extinguisherModel, showWeapon);

        Vector3 pos = leftHiddenPos;
        Quaternion rot = Quaternion.identity;
        float speed = 10f;
        if (showCam)
        {
            pos = Vector3.Lerp(leftHiddenPos, leftCamPos, Mathf.SmoothStep(0f, 1f, camUp));
            rot = Quaternion.Euler(leftCamRot);
            speed = 30f;
        }
        else if (!hidden && !viewing && leftOverride)
        {
            pos = overridePos;
            rot = overrideRot;
            speed = 20f;
        }
        else if (showTracker) { pos = leftTrackerPos; rot = Quaternion.Euler(leftTrackerRot); }
        else if (showWeapon) { pos = leftWeaponPos + Vector3.forward * kick * 0.08f; rot = Quaternion.Euler(leftWeaponRot); }

        pos += swayV * 0.8f + bob * 0.8f + (showCam ? Vector3.zero : runL);
        if (!showCam) rot = rot * Quaternion.Euler(-pump * 12f, 0f, lean);
        leftArm.localPosition = Vector3.Lerp(leftArm.localPosition, pos, dt * speed);
        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, rot, dt * speed);
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
