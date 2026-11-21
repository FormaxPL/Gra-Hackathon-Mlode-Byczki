using UnityEngine;

/// <summary>
/// Gracz FPP: chodzenie, bieg (z kondycją), kucanie, kroki robiące hałas,
/// chowanie się w kryjówkach, wstrzymywanie oddechu, trzęsienie kamery.
/// Ciało gracza (nogi, tułów, cień) animuje ProceduralRig w obiekcie "Body".
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }
    /// <summary>Przerywnik filmowy steruje kamerą – gracz i jego kamera stoją.</summary>
    public static bool CutsceneLock;
    [System.NonSerialized] public bool infiniteStamina;

    [Header("Referencje")]
    public Transform cameraPivot;
    [Tooltip("Kamera (dziecko cameraPivot) – tu działa trzęsienie")]
    public Transform cameraTransform;
    [Tooltip("Ciało 3D gracza – chowane, gdy siedzisz w szafce")]
    public GameObject bodyRoot;

    [Header("Ruch")]
    public float walkSpeed = 3f;
    public float runSpeed = 5.5f;
    public float crouchSpeed = 1.6f;
    public float gravity = -20f;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;
    public float eyeBelowTop = 0.15f;

    [Header("Kamera")]
    public float mouseSensitivity = 2f;

    [Header("Kondycja (sekundy biegu)")]
    public float staminaMax = 4f;
    public float staminaRegen = 0.6f;

    [Header("Oddech (w kryjówce, Spacja)")]
    public float breathMax = 6f;

    [Header("Hałas kroków (promień w metrach – tyle słyszy Stalker)")]
    public float walkNoise = 5f;
    public float runNoise = 14f;
    public float crouchNoise = 0f;

    public float Stamina { get; private set; }
    public float Stamina01 => Stamina / staminaMax;
    public float Breath { get; private set; }
    public float Breath01 => Breath / breathMax;
    public bool IsHoldingBreath { get; private set; }
    public float LastGaspTime { get; private set; } = -999f;
    public bool IsHidden { get; private set; }
    public float HideTime { get; private set; }
    public HidingSpot CurrentSpot { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }
    public float PlanarSpeed { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float Yaw => yaw;
    public float Pitch => pitch;
    public PlayerInventory Inventory { get; private set; }
    public Flashlight Flashlight { get; private set; }
    public bool FlashlightOn => Flashlight != null && Flashlight.IsOn;
    public Vector3 EyePosition => cameraPivot.position;

    CharacterController cc;
    ProceduralRig rig;
    float yaw, pitch, vy, stepDist, bobT, hideYaw, crouch01, shake, gaspBlockUntil;
    int hideFrame;
    bool exhausted;
    AudioSource sfx, pantSrc;
    AudioClip gaspClip;
    AudioClip[][] steps;
    float moveBlend, runBlend, rollTarget, nodTarget, roll, nod, exertion;
    Vector3 bobOffset;

    /// <summary>0..1 – zmęczenie po biegu (zadyszka, mocniejsze kołysanie).</summary>
    public float Exertion => exertion;

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        Inventory = GetComponent<PlayerInventory>();
        if (Inventory == null) Inventory = gameObject.AddComponent<PlayerInventory>();
        Flashlight = GetComponentInChildren<Flashlight>();
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        if (bodyRoot != null) rig = bodyRoot.GetComponent<ProceduralRig>();
        Stamina = staminaMax;
        Breath = breathMax;
        yaw = transform.eulerAngles.y;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        steps = new AudioClip[3][];
        for (int sf = 0; sf < 3; sf++)
        {
            steps[sf] = new AudioClip[3];
            for (int k = 0; k < 3; k++) steps[sf][k] = ProceduralAudio.Footstep(sf, 100 + sf * 10 + k);
        }
        pantSrc = gameObject.AddComponent<AudioSource>();
        pantSrc.playOnAwake = false;
        pantSrc.spatialBlend = 0f;
        pantSrc.loop = true;
        pantSrc.clip = ProceduralAudio.Panting();
        pantSrc.volume = 0f;
        gaspClip = ProceduralAudio.Noise(0.6f, 0.25f, "Gasp");
    }

    void Start() { if (!MenuSystem.IsOpen) LockCursor(true); }

    public static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Update()
    {
        if (CutsceneLock) { IsHoldingBreath = false; PlanarSpeed = 0f; Velocity = Vector3.zero; return; }
        if (GameManager.Blocked) { IsHoldingBreath = false; PlanarSpeed = 0f; Velocity = Vector3.zero; return; }
        HandleLook();
        if (IsHidden)
        {
            HandleBreath();
            if (GameInput.InteractDown && Time.frameCount > hideFrame) ExitHiding();
            return;
        }
        IsHoldingBreath = false;
        Breath = Mathf.Min(breathMax, Breath + Time.deltaTime * 1.5f);
        HandleMove();
    }

    void LateUpdate()
    {
        if (cameraTransform == null || CutsceneLock) return;
        float dt = Time.unscaledDeltaTime;
        var gm = GameManager.Instance;
        bool over = gm != null && gm.IsOver;
        if (over) shake = 0f;
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
        Vector3 shakePos = Vector3.zero;
        Quaternion shakeRot = Quaternion.identity;
        if (shake > 0.001f)
        {
            float s = shake * shake;
            shakePos = Random.insideUnitSphere * s * 0.06f;
            shakeRot = Quaternion.Euler(Random.Range(-1f, 1f) * s * 2.5f, Random.Range(-1f, 1f) * s * 2.5f, Random.Range(-1f, 1f) * s * 1.5f);
        }

        // ---------- immersja: przechył, kiwanie, oddech, drżenie ze strachu ----------
        if (IsHidden || GameManager.Blocked) { rollTarget = 0f; nodTarget = 0f; bobOffset = Vector3.zero; }
        roll = Mathf.Lerp(roll, rollTarget, dt * 8f);
        nod = Mathf.Lerp(nod, nodTarget, dt * 10f);
        float t = Time.unscaledTime;
        float breathAmp = IsHoldingBreath ? 0.15f : (1f + exertion * 0.9f);
        float breathRate = 1.1f * (1f + exertion * 0.45f);
        float breathV = Mathf.Sin(t * breathRate) * 0.003f * breathAmp;
        float breathPitch = Mathf.Sin(t * breathRate) * 0.18f * breathAmp;
        float fear = FearFeedback.Instance != null ? FearFeedback.Instance.Fear : 0f;
        float jit = fear * fear * 0.45f;
        float jx = (Mathf.PerlinNoise(t * 3f, 1.7f) - 0.5f) * jit;
        float jy = (Mathf.PerlinNoise(2.3f, t * 3f) - 0.5f) * jit;
        if (over) { breathV = 0f; breathPitch = 0f; jx = jy = 0f; }
        cameraTransform.localPosition = new Vector3(bobOffset.x, breathV, 0f) + shakePos;
        cameraTransform.localRotation = Quaternion.Euler(nod + breathPitch + jx, jy, roll) * shakeRot;

        // zadyszka po biegu
        if (pantSrc != null)
        {
            float target = (over || IsHoldingBreath || MenuSystem.IsOpen) ? 0f : Mathf.Clamp01((exertion - 0.45f) / 0.55f) * 0.22f;
            pantSrc.volume = Mathf.MoveTowards(pantSrc.volume, target, dt * 0.4f);
            if (pantSrc.volume > 0.01f && !pantSrc.isPlaying) pantSrc.Play();
            else if (pantSrc.volume <= 0.01f && pantSrc.isPlaying) pantSrc.Stop();
            pantSrc.pitch = 0.82f + exertion * 0.1f;
        }

        if (rig != null)
        {
            rig.crouch = crouch01;
            rig.headPitch = pitch;
        }
    }

    /// <summary>Ustawia kierunek patrzenia (np. po przerywniku).</summary>
    public void SetLook(float yawDeg, float pitchDeg)
    {
        yaw = yawDeg;
        pitch = Mathf.Clamp(pitchDeg, -85f, 85f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>Przeniesienie gracza (checkpoint, przerywniki).</summary>
    public void Teleport(Vector3 pos, float yawDeg)
    {
        if (cc != null) cc.enabled = false;
        transform.position = pos;
        if (cc != null) cc.enabled = true;
        vy = 0f;
        SetLook(yawDeg, 0f);
    }

    /// <summary>Przesuwanie gracza w przerywniku (bez kolizji i grawitacji).</summary>
    public void SetCutscenePosition(Vector3 pos)
    {
        if (cc != null && cc.enabled) cc.enabled = false;
        transform.position = pos;
    }

    public void SetControllerEnabled(bool on)
    {
        if (cc != null) cc.enabled = on;
    }

    /// <summary>Kamera wraca na swoje miejsce w głowie (po przerywniku).</summary>
    public void ResetCameraLocal()
    {
        if (cameraTransform == null) return;
        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
        if (cameraPivot != null && cc != null) cameraPivot.localPosition = new Vector3(0f, cc.height - eyeBelowTop, 0f);
    }

    /// <summary>Trzęsienie kamery 0..1 (np. gdy potwór zeskakuje z wentylacji obok).</summary>
    public void AddShake(float amount) => shake = Mathf.Clamp01(Mathf.Max(shake, amount));

    void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        Vector2 look = GameInput.Look * mouseSensitivity;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        if (IsHidden)
        {
            yaw = hideYaw + Mathf.Clamp(Mathf.DeltaAngle(hideYaw, yaw), -45f, 45f);
            pitch = Mathf.Clamp(pitch, -25f, 25f);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        float dt = Time.deltaTime;
        Vector2 input = GameInput.Move;
        Vector3 dir = transform.right * input.x + transform.forward * input.y;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        bool moving = dir.sqrMagnitude > 0.01f;

        IsCrouching = GameInput.Crouch;
        crouch01 = Mathf.MoveTowards(crouch01, IsCrouching ? 1f : 0f, dt * 4f);
        float targetH = IsCrouching ? crouchHeight : standHeight;
        cc.height = Mathf.MoveTowards(cc.height, targetH, dt * 4f);
        cc.center = Vector3.up * (cc.height * 0.5f);

        IsRunning = GameInput.Run && moving && !IsCrouching && !exhausted && input.y > 0f;
        if (IsRunning)
        {
            if (!infiniteStamina) Stamina -= dt;
            if (Stamina <= 0f) { Stamina = 0f; exhausted = true; }
        }
        else
        {
            Stamina = Mathf.Min(staminaMax, Stamina + dt * staminaRegen);
            if (exhausted && Stamina > staminaMax * 0.4f) exhausted = false;
        }

        float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * dt;
        cc.Move((dir * speed + Vector3.up * vy) * dt);
        Vector3 v = cc.velocity;
        v.y = 0f;
        PlanarSpeed = v.magnitude;
        Velocity = v;

        // zmęczenie: rośnie przy biegu, powoli opada
        float exTarget = IsRunning ? 1f - Stamina01 * 0.5f : Mathf.Clamp01(1f - Stamina01);
        // zadyszka narasta wolno i szybko mija (v5: słabsza – mniej bujania obrazem)
        exertion = Mathf.MoveTowards(exertion, exTarget * 0.8f, dt * (exTarget > exertion ? 0.3f : 0.35f));

        // Kroki: hałas + dźwięk zależny od podłogi co ~1.6 m
        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 4f);
        runBlend = Mathf.MoveTowards(runBlend, IsRunning ? 1f : 0f, dt * 3f);
        if (moving)
        {
            bobT += dt * speed * 2.2f;
            stepDist += dt * speed;
            if (stepDist >= 1.6f)
            {
                stepDist = 0f;
                float noise = IsCrouching ? crouchNoise : IsRunning ? runNoise : walkNoise;
                NoiseSystem.Emit(transform.position, noise);
                PlayStep(IsCrouching ? 0.12f : IsRunning ? 0.6f : 0.3f);
            }
        }

        // kołysanie głowy: w dół przy każdym kroku, na boki co dwa kroki; mocniej w biegu
        float crouchK = IsCrouching ? 0.5f : 1f;
        float ampV = Mathf.Lerp(0.02f, 0.038f, runBlend) * crouchK * moveBlend;
        float ampX = Mathf.Lerp(0.018f, 0.03f, runBlend) * crouchK * moveBlend;
        float bobV = Mathf.Cos(bobT * 2f) * ampV;
        bobOffset = new Vector3(Mathf.Sin(bobT) * ampX, 0f, 0f);
        rollTarget = Mathf.Sin(bobT) * Mathf.Lerp(0.5f, 1.1f, runBlend) * moveBlend - input.x * Mathf.Lerp(1.5f, 2.2f, runBlend);
        nodTarget = Mathf.Cos(bobT * 2f) * Mathf.Lerp(0.25f, 0.7f, runBlend) * moveBlend;
        cameraPivot.localPosition = new Vector3(0f, cc.height - eyeBelowTop + bobV, 0f);
    }

    void PlayStep(float volume)
    {
        int surface = 0;
        if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            var r = hit.collider.GetComponent<Renderer>();
            string n = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
            if (n.Contains("Freezer")) surface = 2;
            else if (n.Contains("Backrooms") || n.Contains("Office")) surface = 1;
        }
        var set = steps[surface];
        sfx.pitch = Random.Range(0.88f, 1.12f);
        sfx.PlayOneShot(set[Random.Range(0, set.Length)], volume * (surface == 1 ? 0.8f : 1f));
    }

    void HandleBreath()
    {
        float dt = Time.deltaTime;
        bool want = GameInput.HoldBreath && Time.time >= gaspBlockUntil;
        if (want)
        {
            Breath -= dt;
            if (Breath <= 0f) { Breath = 0f; Gasp(); return; }
        }
        else
        {
            Breath = Mathf.Min(breathMax, Breath + dt * 1.2f);
        }
        IsHoldingBreath = want && Breath > 0f;
    }

    void Gasp()
    {
        IsHoldingBreath = false;
        LastGaspTime = Time.time;
        gaspBlockUntil = Time.time + 2.5f;
        sfx.pitch = 0.8f;
        sfx.PlayOneShot(gaspClip, 0.9f);
        NoiseSystem.Emit(transform.position, 7f);
        if (GameManager.Instance != null) GameManager.Instance.ShowMessage(Loc.T("*łapiesz gwałtownie powietrze*", "*you gasp for air*"), 1.5f);
    }

    public void EnterHiding(HidingSpot spot)
    {
        if (IsHidden || spot == null) return;
        IsHidden = true;
        CurrentSpot = spot;
        HideTime = Time.time;
        hideFrame = Time.frameCount;
        cc.enabled = false;
        transform.position = spot.InsidePosition;
        hideYaw = yaw = spot.transform.eulerAngles.y;
        pitch = 0f;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.identity;
        cameraPivot.localPosition = new Vector3(0f, spot.cameraHeight, 0f);
        if (Flashlight != null) Flashlight.SetOn(false);
        if (bodyRoot != null) bodyRoot.SetActive(false);
        PlanarSpeed = 0f;
        Velocity = Vector3.zero;
    }

    public void ExitHiding()
    {
        if (!IsHidden) return;
        transform.position = CurrentSpot.ExitPosition;
        IsHidden = false;
        IsHoldingBreath = false;
        CurrentSpot = null;
        cc.enabled = true;
        if (bodyRoot != null) bodyRoot.SetActive(true);
    }
}
