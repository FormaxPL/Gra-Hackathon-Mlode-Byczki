using UnityEngine;

/// <summary>
/// Lightweight procedural animation driver for the generic Bone.* rig in entity.fbx.
/// Assign the semantic bones in the Inspector, then call SetMode/SetAlert from AI.
///
/// Oryginał: kolega z drużyny. v5 (integracja z grą):
///  - osie zamachu liczone automatycznie z kierunku kości (rig z Blendera ma różnie obrócone kości),
///    więc nogi i ręce chodzą naprzemiennie niezależnie od orientacji kości,
///  - kolana (shins) uginają się przy kroku,
///  - tryb Attack (rzut na ofiarę) dla jumpscare'a,
///  - podskok/kucanie liczone w metrach świata (model może być dowolnie przeskalowany),
///  - Configure(...) – generator poziomu podpina kości sam.
/// Publiczne API (SetMode, SetAlert, EnterPipe, MotionMode) bez zmian.
/// </summary>
public sealed class MonsterAnimationDriver : MonoBehaviour
{
    public enum MotionMode { Idle, Walk, Jog, Sprint, LockerWait, PipeEnter, Attack }

    [Header("Rig mapping — drag the matching transforms here")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform head;
    [SerializeField] private Transform[] legs = new Transform[0];
    [SerializeField] private Transform[] shins = new Transform[0];
    [SerializeField] private Transform[] arms = new Transform[0];
    [SerializeField] private Transform[] horns = new Transform[0];

    [Header("Movement")]
    [SerializeField] private MotionMode startMode = MotionMode.Idle;
    [Tooltip("Liczy osie zamachu z kierunku kości (zalecane). Wyłączone = legSwingAxis/armSwingAxis jak w oryginale.")]
    [SerializeField] private bool autoAxes = true;
    [SerializeField] private Vector3 legSwingAxis = Vector3.right;
    [SerializeField] private Vector3 armSwingAxis = Vector3.right;
    [SerializeField] private float walkAmplitude = 22f;
    [SerializeField] private float jogAmplitude = 32f;
    [SerializeField] private float sprintAmplitude = 44f;
    [SerializeField] private float kneeBend = 35f;
    [SerializeField] private float blendSpeed = 8f;
    [Tooltip("Wysokość podskoku przy sprincie (metry)")]
    [SerializeField] private float sprintBob = 0.07f;

    [Header("Locker / pipe poses (X = pochylenie do przodu w stopniach)")]
    [SerializeField] private Vector3 lockerLeanEuler = new Vector3(14f, 0f, 0f);
    [SerializeField] private float lockerCrouch = 0.18f;
    [SerializeField] private Vector3 pipeLeanEuler = new Vector3(52f, 0f, 0f);
    [SerializeField] private float pipeCrouch = 0.32f;
    [SerializeField] private float pipeDuration = 1.35f;
    [SerializeField] private bool moveRootIntoPipe;
    [SerializeField] private float pipeTravelDistance = 0.7f;

    [Header("Attack (jumpscare)")]
    [SerializeField] private float attackLean = 24f;
    [SerializeField] private float attackArmRaise = 95f;

    [Header("Horn response when the monster spots the player")]
    [SerializeField] private Vector3 hornAlertEuler = new Vector3(-8f, 0f, 18f);
    [SerializeField] private float alertBlendSpeed = 8f;
    [SerializeField] private float alertTwitchDegrees = 3f;

    private Quaternion bodyRest, headRest;
    private Quaternion[] legRest, shinRest, armRest, hornRest;
    private Vector3[] legAxis, shinAxis, armAxis;
    private float[] legPhase, armPhase;
    private Vector3 bodyLeanAxis, headNodAxis, headYawAxis;
    private Vector3 bodyPositionRest;
    private Vector3 rootPositionRest;
    private MotionMode mode;
    private float phase, pipeTime, alert, alertTarget, moveBlend;

    public MotionMode Mode => mode;

    /// <summary>Podpięcie kości z kodu (generator poziomu). Wywołaj w edytorze albo przed Awake.</summary>
    public void Configure(Transform bodyBone, Transform headBone, Transform[] legBones, Transform[] shinBones, Transform[] armBones, Transform[] hornBones)
    {
        body = bodyBone;
        head = headBone;
        legs = legBones ?? new Transform[0];
        shins = shinBones ?? new Transform[0];
        arms = armBones ?? new Transform[0];
        horns = hornBones ?? new Transform[0];
        if (Application.isPlaying) CacheRestPose();
    }

    private void Awake()
    {
        CacheRestPose();
        mode = startMode;
    }

    private void CacheRestPose()
    {
        if (body != null) { bodyRest = body.localRotation; bodyPositionRest = body.localPosition; }
        if (head != null) headRest = head.localRotation;
        rootPositionRest = transform.localPosition;
        legRest = Cache(legs);
        shinRest = Cache(shins);
        armRest = Cache(arms);
        hornRest = Cache(horns);

        Vector3 fwd = transform.forward, right = transform.right, up = transform.up;
        legAxis = SwingAxes(legs, legSwingAxis, fwd);
        shinAxis = SwingAxes(shins, legSwingAxis, fwd);
        armAxis = SwingAxes(arms, armSwingAxis, fwd);
        legPhase = SidePhases(legs, right, 0f);
        armPhase = SidePhases(arms, right, Mathf.PI);
        bodyLeanAxis = body != null ? LocalTiltAxis(body, fwd, Vector3.right) : Vector3.right;
        headNodAxis = head != null ? LocalTiltAxis(head, fwd, Vector3.right) : Vector3.right;
        headYawAxis = head != null ? (Quaternion.Inverse(head.rotation) * up).normalized : Vector3.up;
    }

    private static Quaternion[] Cache(Transform[] bones)
    {
        if (bones == null) return new Quaternion[0];
        var result = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++) result[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        return result;
    }

    /// <summary>Kierunek kości w świecie (do pierwszego dziecka albo lokalne Y).</summary>
    private static Vector3 Along(Transform b)
    {
        if (b.childCount > 0)
        {
            Vector3 d = b.GetChild(0).position - b.position;
            if (d.sqrMagnitude > 1e-8f) return d.normalized;
        }
        return b.up;
    }

    /// <summary>Oś (w lokalnym układzie kości), wokół której dodatni kąt przechyla kość "do przodu".</summary>
    private Vector3 LocalTiltAxis(Transform b, Vector3 forward, Vector3 fallback)
    {
        if (!autoAxes) return fallback.normalized;
        Vector3 a = Along(b);
        Vector3 w = Vector3.Cross(a, forward);
        if (w.sqrMagnitude < 1e-6f) w = Vector3.Cross(a, transform.up);
        if (w.sqrMagnitude < 1e-6f) return fallback.normalized;
        return (Quaternion.Inverse(b.rotation) * w.normalized).normalized;
    }

    private Vector3[] SwingAxes(Transform[] bones, Vector3 manual, Vector3 forward)
    {
        var r = new Vector3[bones != null ? bones.Length : 0];
        for (int i = 0; i < r.Length; i++)
            r[i] = bones[i] != null ? LocalTiltAxis(bones[i], forward, manual) : manual.normalized;
        return r;
    }

    /// <summary>Lewa strona faza 0, prawa π (+ przesunięcie dla rąk) – chód naprzemienny.</summary>
    private float[] SidePhases(Transform[] bones, Vector3 right, float offset)
    {
        var r = new float[bones != null ? bones.Length : 0];
        for (int i = 0; i < r.Length; i++)
        {
            bool isRight = bones[i] != null
                ? Vector3.Dot(bones[i].position - transform.position, right) > 0f
                : i % 2 == 1;
            if (!autoAxes) isRight = i % 2 == 1;
            r[i] = (isRight ? Mathf.PI : 0f) + offset;
        }
        return r;
    }

    /// <summary>Set from the enemy AI's locomotion state.</summary>
    public void SetMode(MotionMode next)
    {
        if (next == mode) return;
        if (next == MotionMode.PipeEnter)
        {
            pipeTime = 0f;
            rootPositionRest = transform.localPosition;
        }
        mode = next;
    }

    /// <summary>Call when line of sight to the player is acquired or lost.</summary>
    public void SetAlert(bool spotted) { alertTarget = spotted ? 1f : 0f; }

    /// <summary>Play the short crawl-in pose, then return to idle.</summary>
    public void EnterPipe() { SetMode(MotionMode.PipeEnter); }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool attack = mode == MotionMode.Attack;
        float speed = mode == MotionMode.Sprint || attack ? 2.8f : mode == MotionMode.Jog ? 2.15f : mode == MotionMode.Walk ? 1.35f : 0f;
        float amplitude = mode == MotionMode.Sprint ? sprintAmplitude : mode == MotionMode.Jog ? jogAmplitude : walkAmplitude;
        bool moving = mode == MotionMode.Walk || mode == MotionMode.Jog || mode == MotionMode.Sprint;
        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 4f);
        phase += dt * speed * Mathf.PI * 2f;
        alert = Mathf.MoveTowards(alert, attack ? 1f : alertTarget, alertBlendSpeed * dt);

        float crouch = mode == MotionMode.LockerWait ? lockerCrouch : 0f;
        float lean = mode == MotionMode.LockerWait ? lockerLeanEuler.x : attack ? attackLean : 0f;
        if (mode == MotionMode.Sprint) lean = 12f;
        if (mode == MotionMode.PipeEnter)
        {
            pipeTime += dt;
            float pipeBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pipeTime / pipeDuration));
            crouch = Mathf.Lerp(0f, pipeCrouch, pipeBlend);
            lean = Mathf.Lerp(0f, pipeLeanEuler.x, pipeBlend);
            if (moveRootIntoPipe)
                transform.localPosition = rootPositionRest + (transform.localRotation * Vector3.forward) * (pipeTravelDistance * pipeBlend);
            if (pipeTime >= pipeDuration)
            {
                mode = MotionMode.Idle;
                if (moveRootIntoPipe) rootPositionRest = transform.localPosition;
            }
        }

        if (body != null)
        {
            float breathing = Mathf.Sin(Time.time * 1.6f) * (moving ? 0.6f : 1.4f);
            float bob = Mathf.Abs(Mathf.Sin(phase)) * (mode == MotionMode.Sprint ? sprintBob : 0.03f) * moveBlend;
            float sway = Mathf.Sin(phase) * 4f * moveBlend;
            Quaternion pose = Quaternion.AngleAxis(lean + breathing, bodyLeanAxis) * Quaternion.AngleAxis(sway, Quaternion.Inverse(body.rotation) * transform.up);
            body.localRotation = Quaternion.Slerp(body.localRotation, bodyRest * pose, blendSpeed * dt);
            Vector3 worldOffset = transform.up * (bob - crouch);
            Vector3 localOffset = body.parent != null ? body.parent.InverseTransformVector(worldOffset) : worldOffset;
            body.localPosition = Vector3.Lerp(body.localPosition, bodyPositionRest + localOffset, blendSpeed * dt);
        }

        AnimateLegs(amplitude * moveBlend, dt);
        AnimateArms(amplitude * moveBlend * (mode == MotionMode.Sprint ? 0.85f : 0.55f), attack, dt);

        if (head != null)
        {
            float scan = mode == MotionMode.LockerWait ? Mathf.Sin(Time.time * 0.65f) * 18f : Mathf.Sin(Time.time * 0.8f) * 4f;
            float twitch = alert * Mathf.Sin(Time.time * (attack ? 23f : 5f)) * (attack ? 9f : 4f);
            float nod = attack ? -18f : mode == MotionMode.LockerWait ? 10f : 0f;
            Quaternion pose = Quaternion.AngleAxis(scan + twitch, headYawAxis) * Quaternion.AngleAxis(nod, headNodAxis);
            head.localRotation = Quaternion.Slerp(head.localRotation, headRest * pose, blendSpeed * dt);
        }
        AnimateHorns(dt);
    }

    private void AnimateLegs(float amplitude, float dt)
    {
        if (legs == null || legRest == null) return;
        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null) continue;
            float s = Mathf.Sin(phase + legPhase[i]);
            float swing = s * amplitude;
            legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, legRest[i] * Quaternion.AngleAxis(swing, legAxis[i]), blendSpeed * dt);
            if (shins != null && i < shins.Length && shins[i] != null)
            {
                // kolano zgina się, gdy noga idzie do tyłu i przy przenoszeniu
                float bend = Mathf.Max(0f, -Mathf.Cos(phase + legPhase[i])) * kneeBend * (amplitude / Mathf.Max(1f, walkAmplitude));
                shins[i].localRotation = Quaternion.Slerp(shins[i].localRotation, shinRest[i] * Quaternion.AngleAxis(-bend, shinAxis[i]), blendSpeed * dt);
            }
        }
    }

    private void AnimateArms(float amplitude, bool attack, float dt)
    {
        if (arms == null || armRest == null) return;
        for (int i = 0; i < arms.Length; i++)
        {
            if (arms[i] == null) continue;
            float swing = attack
                ? attackArmRaise + Mathf.Sin(Time.time * 20f + i) * 6f
                : Mathf.Sin(phase + armPhase[i]) * amplitude;
            arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, armRest[i] * Quaternion.AngleAxis(swing, armAxis[i]), blendSpeed * dt * (attack ? 2f : 1f));
        }
    }

    private void AnimateHorns(float dt)
    {
        if (horns == null || hornRest == null) return;
        for (int i = 0; i < horns.Length; i++)
        {
            if (horns[i] == null) continue;
            float side = (i % 2 == 0) ? 1f : -1f;
            float twitch = alert * Mathf.Sin(Time.time * 16f + i) * alertTwitchDegrees;
            Vector3 pose = new Vector3(hornAlertEuler.x, hornAlertEuler.y, hornAlertEuler.z * side);
            pose += new Vector3(twitch, 0f, twitch * side * 0.5f);
            Quaternion target = hornRest[i] * Quaternion.Euler(pose * alert);
            horns[i].localRotation = Quaternion.Slerp(horns[i].localRotation, target, alertBlendSpeed * dt);
        }
    }
}
