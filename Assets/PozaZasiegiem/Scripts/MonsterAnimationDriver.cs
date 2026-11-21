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
    [Tooltip("Uniesienie rąk przy ataku. Powyżej ~60° wagi skinningu entity.fbx odrywają tułów od nóg.")]
    [SerializeField] private float attackArmRaise = 55f;

    [Header("Horn response when the monster spots the player")]
    [SerializeField] private Vector3 hornAlertEuler = new Vector3(-8f, 0f, 18f);
    [SerializeField] private float alertBlendSpeed = 8f;
    [SerializeField] private float alertTwitchDegrees = 3f;

    private Quaternion bodyRest, headRest;
    private Quaternion[] legRest, shinRest, armRest, hornRest;
    private Vector3[] legAxis, shinAxis, armAxis;
    private float[] legPhase, armPhase;
    private Vector3 bodyLeanAxis, headNodAxis, headYawAxis;
    private Vector3 rootPositionRest;
    private MotionMode mode;
    private float phase, pipeTime, alert, alertTarget, moveBlend;

    // v6: miednica = tułów + kości, z których wychodzą nogi (w entity.fbx nogi NIE są dziećmi tułowia),
    // więc podskok/kucanie przesuwa je razem i tułów nie odrywa się od nóg.
    private Transform[] pelvis = new Transform[0];
    private Vector3[] pelvisRest = new Vector3[0];
    private float thighLen = 0.35f, shinLen = 0.3f;
    private float speedInput = -1f;              // m/s z AI (SetSpeed); < 0 = stałe tempo trybu
    private float ampS, armAmpS, leanS, crouchS, attackS;

    public MotionMode Mode => mode;

    /// <summary>Prędkość potwora w m/s – tempo kroku dopasowuje się do niej (stopy nie ślizgają się).</summary>
    public void SetSpeed(float metersPerSecond) { speedInput = Mathf.Max(0f, metersPerSecond); }

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
        if (body != null) bodyRest = body.localRotation;
        if (head != null) headRest = head.localRotation;
        rootPositionRest = transform.localPosition;
        legRest = Cache(legs);
        shinRest = Cache(shins);
        armRest = Cache(arms);
        hornRest = Cache(horns);
        CachePelvisAndLegLengths();

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

    private void CachePelvisAndLegLengths()
    {
        var list = new System.Collections.Generic.List<Transform>();
        if (body != null) list.Add(body);
        if (legs != null)
            foreach (var l in legs)
            {
                if (l == null || l.parent == null || l.parent == transform) continue;
                var p = l.parent;
                bool underPelvis = false;
                foreach (var q in list) if (p == q || p.IsChildOf(q)) underPelvis = true;
                if (!underPelvis) list.Add(p);
            }
        pelvis = list.ToArray();
        pelvisRest = new Vector3[pelvis.Length];
        for (int i = 0; i < pelvis.Length; i++) pelvisRest[i] = pelvis[i].localPosition;

        // długości udo/łydka w metrach świata (do dopasowania tempa kroku i kucania)
        float t = 0f, s = 0f; int n = 0;
        for (int i = 0; legs != null && i < legs.Length; i++)
        {
            if (legs[i] == null || shins == null || i >= shins.Length || shins[i] == null) continue;
            t += Vector3.Distance(legs[i].position, shins[i].position);
            s += shins[i].childCount > 0 ? Vector3.Distance(shins[i].position, shins[i].GetChild(0).position) : 0.8f * t / (n + 1);
            n++;
        }
        if (n > 0 && t > 1e-3f && s > 1e-3f) { thighLen = t / n; shinLen = s / n; }
    }

    /// <summary>Kucanie bez wbijania stóp w podłogę: kąt uda do przodu i łydki do tyłu,
    /// przy których biodro opada o <paramref name="drop"/>, a stopa zostaje pod biodrem.</summary>
    private void CrouchAngles(float drop, out float thighFwd, out float kneeBend)
    {
        thighFwd = 0f; kneeBend = 0f;
        if (drop <= 1e-4f) return;
        float target = thighLen + shinLen - drop;
        float lo = 0f, hi = Mathf.PI * 0.45f;
        for (int it = 0; it < 18; it++)
        {
            float a = (lo + hi) * 0.5f;
            float sb = Mathf.Clamp(thighLen / shinLen * Mathf.Sin(a), -1f, 1f);
            float h = thighLen * Mathf.Cos(a) + shinLen * Mathf.Sqrt(1f - sb * sb);
            if (h > target) lo = a; else hi = a;
        }
        float aa = (lo + hi) * 0.5f;
        float b = Mathf.Asin(Mathf.Clamp(thighLen / shinLen * Mathf.Sin(aa), -1f, 1f));
        thighFwd = aa * Mathf.Rad2Deg;
        kneeBend = (aa + b) * Mathf.Rad2Deg;
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
        float cadence = mode == MotionMode.Sprint || attack ? 2.8f : mode == MotionMode.Jog ? 2.15f : mode == MotionMode.Walk ? 1.35f : 0f;
        float amplitude = mode == MotionMode.Sprint ? sprintAmplitude : mode == MotionMode.Jog ? jogAmplitude : walkAmplitude;
        bool moving = mode == MotionMode.Walk || mode == MotionMode.Jog || mode == MotionMode.Sprint;
        // Tempo kroku z prędkości: w podporze stopa przesuwa się o 2·L·sin(A) w pół cyklu,
        // więc v = 4·L·sin(A)·f  →  f = v / (4·L·sin(A)). Dzięki temu stopy nie ślizgają się po podłodze.
        if (moving && speedInput >= 0f)
        {
            float stride = 4f * (thighLen + shinLen) * Mathf.Sin(amplitude * Mathf.Deg2Rad);
            cadence = Mathf.Clamp(speedInput / Mathf.Max(0.05f, stride), 0.6f, 3.4f);
        }
        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 4f);
        phase += dt * cadence * Mathf.PI * 2f;
        if (phase > 1000f) phase -= Mathf.PI * 2f * 100f;
        alert = Mathf.MoveTowards(alert, attack ? 1f : alertTarget, alertBlendSpeed * dt);
        // Płynne przejścia między trybami wygładzają SKALARY (amplitudę, pochylenie, kucanie),
        // a nie gotowe obroty – wygładzanie obrotów spóźniało i tłumiło ruch cykliczny nóg i rąk.
        float k = 1f - Mathf.Exp(-blendSpeed * dt);
        ampS = Mathf.Lerp(ampS, amplitude * moveBlend, k);
        armAmpS = Mathf.Lerp(armAmpS, amplitude * moveBlend * (mode == MotionMode.Sprint ? 0.85f : 0.55f), k);
        attackS = Mathf.MoveTowards(attackS, attack ? 1f : 0f, dt * (attack ? 6f : 3f));

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

        leanS = Mathf.Lerp(leanS, lean, k);
        crouchS = mode == MotionMode.PipeEnter ? crouch : Mathf.MoveTowards(crouchS, crouch, dt * 0.9f);

        // Biodro opada, gdy nogi są rozkroczone (noga podporowa zostaje na podłodze); w sprincie lekki wyskok.
        float legSpan = thighLen + shinLen;
        float stanceDrop = legSpan * (1f - Mathf.Cos(ampS * Mathf.Abs(Mathf.Sin(phase)) * Mathf.Deg2Rad));
        float hop = mode == MotionMode.Sprint ? Mathf.Abs(Mathf.Cos(phase)) * sprintBob * moveBlend : 0f;
        Vector3 worldOffset = transform.up * (hop - stanceDrop - crouchS);
        for (int i = 0; i < pelvis.Length; i++)
        {
            if (pelvis[i] == null) continue;
            var par = pelvis[i].parent;
            pelvis[i].localPosition = pelvisRest[i] + (par != null ? par.InverseTransformVector(worldOffset) : worldOffset);
        }

        if (body != null)
        {
            float breathing = Mathf.Sin(Time.time * 1.6f) * (moving ? 0.6f : 1.4f);
            float sway = Mathf.Sin(phase) * 4f * moveBlend;
            Quaternion pose = Quaternion.AngleAxis(leanS + breathing, bodyLeanAxis) * Quaternion.AngleAxis(sway, Quaternion.Inverse(body.rotation) * transform.up);
            body.localRotation = bodyRest * pose;
        }

        AnimateLegs(ampS, crouchS);
        AnimateArms(armAmpS);

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

    private void AnimateLegs(float amplitude, float crouchDrop)
    {
        if (legs == null || legRest == null) return;
        CrouchAngles(crouchDrop, out float crouchThigh, out float crouchKnee);
        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null) continue;
            float p = phase + legPhase[i];
            float swing = Mathf.Sin(p) * amplitude + crouchThigh;
            legs[i].localRotation = legRest[i] * Quaternion.AngleAxis(swing, legAxis[i]);
            if (shins != null && i < shins.Length && shins[i] != null)
            {
                // Kolano zgina się w fazie PRZENOSZENIA (udo idzie do przodu: cos > 0, najmocniej pod biodrem),
                // a w podporze noga jest prosta – wcześniej było odwrotnie i stopa sunęła po podłodze.
                float bend = Mathf.Max(0f, Mathf.Cos(p)) * kneeBend * (amplitude / Mathf.Max(1f, walkAmplitude)) + crouchKnee;
                shins[i].localRotation = shinRest[i] * Quaternion.AngleAxis(-bend, shinAxis[i]);
            }
        }
    }

    private void AnimateArms(float amplitude)
    {
        if (arms == null || armRest == null) return;
        for (int i = 0; i < arms.Length; i++)
        {
            if (arms[i] == null) continue;
            float walkSwing = Mathf.Sin(phase + armPhase[i]) * amplitude;
            float attackSwing = attackArmRaise + Mathf.Sin(Time.time * 20f + i) * 6f;
            float swing = Mathf.Lerp(walkSwing, attackSwing, Mathf.SmoothStep(0f, 1f, attackS));
            arms[i].localRotation = armRest[i] * Quaternion.AngleAxis(swing, armAxis[i]);
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
