using System.Collections;
using UnityEngine;

/// <summary>
/// Fabuła i przerywniki:
///  1. INTRO: znajomi kręcą film w opuszczonym magazynie, podłoga się zapada, bohater spada
///     i budzi się w labiryncie zaplecza.
///  2. W połowie (2. część radiostacji) – awaria prądu (Blackout).
///  3. Radiostacja → kontakt z AGENCJĄ ECHO. Agencja odcina prąd, żeby odblokować bramę serwisową.
///     Trzeba się do niej przekraść w ciemności (noktowizja!).
///  4. Za bramą: tunel serwisowy. W pewnym momencie zapalają się czerwone światła, wyje syrena
///     i potwór rusza w pościg aż do końca.
///  5. Na końcu agenci ECHO trzymają otwarty portal. Odpędzają potwora reflektorami,
///     bohater przechodzi do prawdziwego świata – przerywnik końcowy i napisy.
/// Śmierć w pościgu = powrót do punktu kontrolnego przy bramie.
/// </summary>
public class StoryDirector : MonoBehaviour
{
    public static StoryDirector Instance { get; private set; }
    /// <summary>Punkt kontrolny przy bramie (przeżywa przeładowanie sceny).</summary>
    public static bool checkpoint;
    /// <summary>Intro już obejrzane w tej sesji (restart po śmierci go pomija).</summary>
    public static bool introSeen;

    public enum Phase { Explore, Escape, Chase, Ending }
    public Phase CurrentPhase { get; private set; }
    public string Objective { get; private set; }

    [Header("Powierzchnia (przerywniki)")]
    public GameObject surfaceRoot;
    public GameObject introSet;
    public GameObject endingSet;

    [Header("Intro")]
    public Transform introCamStart;
    public Transform introCamEnd;
    public Transform introLook;
    public Transform introHole;
    public Transform[] introFloorPieces = new Transform[0];
    public Transform[] friends = new Transform[0];
    public float fallDepth = 16f;

    [Header("Brama i pościg")]
    public GateController gate;
    public Transform gateCheckpoint;
    public Transform chaseStalkerSpawn;
    public Bounds chaseZone;
    public Bounds endZone;
    public GameObject tunnelLightsRoot;
    public Transform sirenPoint;

    [Header("Portal i agenci")]
    public Transform portalWalkPoint;
    public Transform agentsLook;
    public Light agentFloodlight;

    [Header("Zakończenie")]
    public Transform endCamStart;
    public Transform endCamEnd;
    public Transform endCamLook;
    public Transform endPlayerStart;
    public Transform endPlayerWalkTo;
    public Transform endPortal;

    // ---- stan ----
    bool midBlackoutDone, cutscene, skippable, rec, letterbox, credits;
    float fade, subtitleUntil, recStart;
    Color fadeColor = Color.black;
    string subtitle, speaker;
    Color speakerColor = Color.white;
    Coroutine routine;
    AudioSource sfx, radioSrc, sirenSrc, humSrc;
    GUIStyle subStyle, nameStyle, bigStyle, smallStyle;
    float styleK;
    Texture2D white;
    Camera cutCam;

    PlayerController P => PlayerController.Instance;

    void Awake()
    {
        Instance = this;
        PlayerController.CutsceneLock = false;
        if (surfaceRoot != null) surfaceRoot.SetActive(false);
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        radioSrc = gameObject.AddComponent<AudioSource>();
        radioSrc.playOnAwake = false;
        radioSrc.spatialBlend = 0f;
        humSrc = gameObject.AddComponent<AudioSource>();
        humSrc.playOnAwake = false;
        humSrc.spatialBlend = 0f;
        humSrc.loop = true;
        white = Texture2D.whiteTexture;
    }

    void Start()
    {
        SetTunnelLights(false);
        if (sirenPoint != null)
        {
            sirenSrc = sirenPoint.gameObject.AddComponent<AudioSource>();
            sirenSrc.playOnAwake = false;
            sirenSrc.loop = true;
            sirenSrc.spatialBlend = 0.6f;
            sirenSrc.maxDistance = 60f;
            sirenSrc.clip = ProceduralAudio.Siren();
            sirenSrc.volume = 0.5f;
        }
        if (string.IsNullOrEmpty(Objective) && CurrentPhase == Phase.Explore)
            Objective = Loc.T("Znajdź 3 części radiostacji", "Find 3 radio parts");
    }

    void SetTunnelLights(bool on)
    {
        if (tunnelLightsRoot == null) return;
        foreach (var lc in tunnelLightsRoot.GetComponentsInChildren<LightCategory>(true))
        {
            lc.scriptedOff = !on;
            lc.Apply();
        }
    }

    /// <summary>Wywoływane przez menu po "Rozpocznij grę" (albo od razu po restarcie).</summary>
    public void OnGameStart()
    {
        if (TrailerDirector.TryBegin()) { introSeen = true; return; } // komenda „trailer” – zamiast intro
        if (checkpoint) { ApplyCheckpoint(); return; }
        if (!introSeen && introCamStart != null) { routine = StartCoroutine(Intro()); return; }
        if (GameManager.Instance != null) GameManager.Instance.ShowIntro();
    }

    void ApplyCheckpoint()
    {
        introSeen = true;
        var p = P;
        var gm = GameManager.Instance;
        if (p != null && gm != null)
        {
            p.Inventory.parts = gm.partsRequired;
            p.Inventory.hasTracker = true;
            p.Inventory.hasWeapon = true;
            p.Inventory.weaponCharges = Mathf.Max(p.Inventory.weaponCharges, 3);
            if (gateCheckpoint != null) p.Teleport(gateCheckpoint.position, gateCheckpoint.eulerAngles.y);
        }
        if (gate != null) gate.Unlock(true);
        CurrentPhase = Phase.Escape;
        Objective = Loc.T("Biegnij tunelem do portalu Agencji ECHO", "Run through the tunnel to the ECHO portal");
        if (gm != null) gm.ShowMessage(Loc.T("Punkt kontrolny: brama serwisowa.\nPrzygotuj się do biegu.", "Checkpoint: service gate.\nGet ready to run."), 4f);
    }

    void Update()
    {
        var p = P;
        if (p == null) return;

        if (cutscene && skippable && !MenuSystem.IsOpen && GameInput.SkipDown) SkipIntro();
        if (cutscene) return;
        // po złapaniu przez potwora fabuła stoi – inaczej np. zakończenie ruszyłoby w trakcie ekranu śmierci
        var gm = GameManager.Instance;
        if (gm != null && (gm.IsOver || gm.IsDying)) return;

        switch (CurrentPhase)
        {
            case Phase.Explore:
                if (!midBlackoutDone && p.Inventory.parts >= 2)
                {
                    midBlackoutDone = true;
                    StartCoroutine(MidBlackout());
                }
                break;
            case Phase.Escape:
                if (chaseZone.size.sqrMagnitude > 0f && chaseZone.Contains(p.transform.position)) StartChase();
                break;
            case Phase.Chase:
                if (endZone.size.sqrMagnitude > 0f && endZone.Contains(p.transform.position)) routine = StartCoroutine(Ending());
                break;
        }
    }

    IEnumerator MidBlackout()
    {
        yield return new WaitForSeconds(Random.Range(3f, 6f));
        if (CurrentPhase != Phase.Explore || Blackout.Instance == null) yield break;
        Blackout.Instance.Begin(75f, true);
    }

    // =====================================================================
    //  RADIO → AGENCJA ECHO
    // =====================================================================

    public void OnRadioRepaired(Vector3 radioPos)
    {
        if (CurrentPhase != Phase.Explore) return;
        CurrentPhase = Phase.Escape;
        StartCoroutine(RadioSequence(radioPos));
    }

    IEnumerator RadioSequence(Vector3 radioPos)
    {
        var gm = GameManager.Instance;
        radioSrc.clip = ProceduralAudio.RadioVoice(22f, 77);
        radioSrc.volume = 0.5f;
        radioSrc.Play();
        Objective = Loc.T("Słuchaj radia", "Listen to the radio");
        Color echo = new Color(0.45f, 0.9f, 1f);
        yield return SayWait("ECHO", echo, "...szzz... Tu Agencja ECHO. Słyszymy cię, ocalały. Namierzyliśmy twój sygnał.",
            "...kssh... This is the ECHO Agency. We hear you, survivor. We've traced your signal.", 4.5f);
        yield return SayWait("ECHO", echo, "Jesteś poza zasięgiem naszego świata. Otwieramy przejście w tunelu za bramą serwisową.",
            "You're out of range of our world. We're opening a passage in the tunnel behind the service gate.", 4.5f);
        yield return SayWait("ECHO", echo, "Żeby zdjąć blokadę bramy, musimy odciąć całe zasilanie. Będzie zupełnie ciemno.",
            "To release the gate lock we have to cut all power. It will be completely dark.", 4f);

        if (Blackout.Instance != null) Blackout.Instance.Begin(-1f, false);
        if (gate != null) gate.Unlock(false);
        yield return new WaitForSeconds(1.5f);

        yield return SayWait("ECHO", echo, "Nie zapalaj latarki – to coś idzie do światła. Użyj kamery z noktowizją.",
            "Don't use the flashlight – that thing goes for the light. Use the camera's night vision.", 4.5f);
        yield return SayWait("ECHO", echo, "Idź do bramy. Usłyszysz sygnał i zobaczysz kogut. Skradaj się. Czekamy na końcu tunelu.",
            "Go to the gate. You'll hear a beacon and see a rotating light. Sneak. We're waiting at the end of the tunnel.", 5f);
        radioSrc.Stop();
        Objective = Loc.T("Przekradnij się do bramy serwisowej (sygnał dźwiękowy, kogut)", "Sneak to the service gate (beacon sound, rotating light)");
        if (gm != null) gm.ShowMessage(Loc.T("[PPM] kamera, [F] noktowizja. Latarka ściąga potwora!", "[RMB] camera, [F] night vision. The flashlight draws the creature!"), 5f);

        // potwór słyszał radio
        yield return new WaitForSeconds(4f);
        NoiseSystem.Emit(radioPos, 30f);
    }

    // =====================================================================
    //  POŚCIG
    // =====================================================================

    void StartChase()
    {
        CurrentPhase = Phase.Chase;
        checkpoint = true;
        var p = P;
        SetTunnelLights(true);
        if (sirenSrc != null) sirenSrc.Play();
        if (gate != null) gate.StopBeacon();
        var s = FindStalker();
        if (s != null && chaseStalkerSpawn != null) s.ForceChase(chaseStalkerSpawn.position, p.runSpeed * 0.98f);
        p.infiniteStamina = true;
        p.AddShake(0.6f);
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.Flash(new Color(0.6f, 0f, 0f, 0.5f), 0.6f);
            gm.ShowMessage(Loc.T("BIEGNIJ!", "RUN!"), 2.5f);
        }
        Objective = Loc.T("Uciekaj do portalu Agencji ECHO!", "Escape to the ECHO portal!");
    }

    static StalkerAI FindStalker() => StalkerAI.All.Count > 0 ? StalkerAI.All[0] : null;

    // =====================================================================
    //  PRZERYWNIKI – narzędzia
    // =====================================================================

    FirstPersonArms Arms => P != null ? P.GetComponentInChildren<FirstPersonArms>(true) : null;

    void BeginCutscene(bool hideArms, bool hideBody)
    {
        cutscene = true;
        letterbox = true;
        PlayerController.CutsceneLock = true;
        if (Camcorder.Instance != null) Camcorder.Instance.ForceLower();
        var arms = Arms;
        if (arms != null && hideArms) arms.gameObject.SetActive(false);
        if (P != null && P.bodyRoot != null && hideBody) P.bodyRoot.gameObject.SetActive(false);
    }

    void EndCutscene()
    {
        cutscene = false;
        skippable = false;
        letterbox = false;
        rec = false;
        fade = 0f;
        subtitle = null;
        var p = P;
        if (p != null)
        {
            p.ResetCameraLocal();
            p.SetControllerEnabled(true);
            var arms = Arms;
            if (arms != null) arms.gameObject.SetActive(true);
            if (p.bodyRoot != null) p.bodyRoot.gameObject.SetActive(true);
        }
        PlayerController.CutsceneLock = false;
        if (!MenuSystem.IsOpen) PlayerController.LockCursor(true);
    }

    void Say(string who, Color c, string pl, string en, float duration)
    {
        speaker = who;
        speakerColor = c;
        subtitle = Loc.T(pl, en);
        subtitleUntil = Time.time + duration;
        Mumble(who, subtitle, duration);
    }

    AudioSource voiceSrc;

    /// <summary>Wysokość „głosu” (Hz) postaci przy mamrotaniu.</summary>
    public static float VoicePitch(string who)
    {
        switch (who)
        {
            case "JACK": return 120f;
            case "MARY": return 225f;
            case "ECHO-1": return 105f;
            case "ECHO-2": return 160f;
            default: return 140f; // bohater (bez imienia)
        }
    }

    /// <summary>Postacie nie mają aktorów głosowych – mówią mamrotaniem, jak w grach indie.</summary>
    void Mumble(string who, string text, float duration)
    {
        var clip = ProceduralAudio.Mumble(text, VoicePitch(who), duration * 0.9f);
        if (clip == null) return;
        if (voiceSrc == null)
        {
            voiceSrc = gameObject.AddComponent<AudioSource>();
            voiceSrc.playOnAwake = false;
            voiceSrc.spatialBlend = 0f;
            voiceSrc.volume = 0.55f;
        }
        voiceSrc.Stop();
        voiceSrc.clip = clip;
        voiceSrc.Play();
    }

    IEnumerator SayWait(string who, Color c, string pl, string en, float duration)
    {
        Say(who, c, pl, en, duration);
        yield return new WaitForSeconds(duration);
    }

    static Vector3 Handheld(float t, float amount) =>
        new Vector3(Mathf.PerlinNoise(t * 0.9f, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t * 0.8f) - 0.5f, 0f) * amount;

    // =====================================================================
    //  INTRO
    // =====================================================================

    IEnumerator Intro()
    {
        var p = P;
        BeginCutscene(true, true);
        skippable = true;
        if (surfaceRoot != null) surfaceRoot.SetActive(true);
        if (introSet != null) introSet.SetActive(true);
        if (endingSet != null) endingSet.SetActive(false);
        Transform cam = p.cameraTransform;
        rec = true;
        recStart = Time.time;
        fade = 1f;
        fadeColor = Color.black;

        Color kuba = new Color(1f, 0.8f, 0.4f), ola = new Color(0.6f, 0.85f, 1f), me = new Color(0.85f, 0.85f, 0.85f);
        Vector3 a = introCamStart.position, b = introCamEnd != null ? introCamEnd.position : a;
        Vector3 look = introLook != null ? introLook.position : a + introCamStart.forward;
        Vector3 hole = introHole != null ? introHole.position : b + Vector3.down;

        // --- ujęcie 1: kamera z ręki, idziemy za znajomymi ---
        float T = 10f, t = 0f;
        bool said1 = false, said2 = false, creak = false;
        while (t < T)
        {
            t += Time.deltaTime;
            fade = Mathf.Clamp01(1f - t / 1.2f);
            float u = Mathf.SmoothStep(0f, 1f, t / T);
            Vector3 pos = Vector3.Lerp(a, b, u) + Vector3.up * Mathf.Sin(t * 6f) * 0.015f;
            float lookDown = Mathf.SmoothStep(0f, 1f, (t - 7.5f) / 2.5f);
            Vector3 target = Vector3.Lerp(look, hole, lookDown);
            Vector3 hh = Handheld(t, 2.2f);
            cam.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos) * Quaternion.Euler(hh.x, hh.y, hh.x * 0.6f));
            if (!said1 && t > 0.8f) { said1 = true; Say("JACK", kuba, "Nagrywasz? Dobra... to tutaj. Stary magazyn fabryki paliwa syntetycznego. Podobno cała nocna zmiana zniknęła tu bez śladu.", "Recording? Okay... this is it. The old synthetic fuel factory warehouse. They say the whole night shift vanished here without a trace.", 4.6f); }
            if (!said2 && t > 5.6f) { said2 = true; Say("MARY", ola, "Ciii... słyszysz? Coś stuka. Pod podłogą...", "Shh... hear that? Something's knocking. Under the floor...", 3.6f); }
            if (!creak && t > 6.8f) { creak = true; sfx.PlayOneShot(ProceduralAudio.Creak(31), 1f); }
            yield return null;
        }

        // --- ujęcie 2: podłoga się zapada ---
        sfx.PlayOneShot(ProceduralAudio.FloorBreak(), 1f);
        Say("JACK", kuba, "UWAŻAJ!!!", "WATCH OUT!!!", 2.2f);
        var vel = new Vector3[introFloorPieces.Length];
        var spin = new Vector3[introFloorPieces.Length];
        for (int i = 0; i < vel.Length; i++)
        {
            vel[i] = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-1f, 0.3f), Random.Range(-0.6f, 0.6f));
            spin[i] = Random.insideUnitSphere * 120f;
        }
        Vector3 start = cam.position;
        Quaternion startRot = cam.rotation;
        float fallV = 0f, drop = 0f;
        t = 0f;
        while (t < 2.3f)
        {
            float dt = Time.deltaTime;
            t += dt;
            for (int i = 0; i < introFloorPieces.Length; i++)
            {
                if (introFloorPieces[i] == null || t < 0.25f + i * 0.05f) continue;
                vel[i] += Physics.gravity * dt;
                introFloorPieces[i].position += vel[i] * dt;
                introFloorPieces[i].Rotate(spin[i] * dt, Space.World);
            }
            if (t > 0.45f) { fallV += 9.81f * dt * 1.3f; drop += fallV * dt; }
            drop = Mathf.Min(drop, fallDepth);
            Vector3 shake = Random.insideUnitSphere * (t < 0.45f ? 0.04f : 0.02f);
            cam.position = start + Vector3.down * drop + shake;
            float tumble = Mathf.Clamp01((t - 0.45f) / 1.5f);
            cam.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation(Vector3.up, startRot * Vector3.forward), tumble * 0.85f)
                           * Quaternion.Euler(0f, 0f, tumble * 70f);
            fade = Mathf.Clamp01((drop - fallDepth * 0.55f) / (fallDepth * 0.3f));
            yield return null;
        }
        fade = 1f;
        rec = false;
        subtitle = null;
        sfx.PlayOneShot(ProceduralAudio.Thump(0.6f, 40f, 9), 1f);
        p.AddShake(1f);
        sfx.PlayOneShot(ProceduralAudio.Tinnitus(5f), 0.7f);
        if (surfaceRoot != null) surfaceRoot.SetActive(false);
        yield return new WaitForSeconds(2.6f);

        // --- ujęcie 3: przebudzenie w labiryncie ---
        Transform pivot = p.cameraPivot;
        Vector3 eye = pivot.position;
        Vector3 ground = p.transform.position + Vector3.up * 0.22f + p.transform.right * 0.15f;
        Quaternion lying = Quaternion.Euler(-4f, p.transform.eulerAngles.y + 25f, 82f);
        Quaternion standing = Quaternion.Euler(0f, p.transform.eulerAngles.y, 0f);
        cam.SetPositionAndRotation(ground, lying);
        float[] blinkTimes = { 0f, 0.9f, 1.25f, 2.3f, 2.6f, 3.6f };
        float[] blinkVals = { 1f, 0.55f, 1f, 0.25f, 0.85f, 0f };
        t = 0f;
        while (t < 3.6f)
        {
            t += Time.deltaTime;
            int k = 0;
            while (k < blinkTimes.Length - 2 && t > blinkTimes[k + 1]) k++;
            fade = Mathf.Lerp(blinkVals[k], blinkVals[k + 1], Mathf.InverseLerp(blinkTimes[k], blinkTimes[k + 1], t));
            if (t > 1.3f && subtitle == null) Say("", me, "...ugh... gdzie... gdzie ja jestem?", "...ugh... where... where am I?", 3.5f);
            Vector3 hh = Handheld(t, 1.5f);
            cam.rotation = lying * Quaternion.Euler(hh.x, hh.y, 0f);
            yield return null;
        }
        t = 0f;
        while (t < 3.2f)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / 3.2f);
            float wobble = Mathf.Sin(t * 5f) * (1f - u) * 6f;
            cam.position = Vector3.Lerp(ground, eye, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.1f;
            cam.rotation = Quaternion.Slerp(lying, standing, u) * Quaternion.Euler(0f, 0f, wobble);
            yield return null;
        }
        Say("", me, "Telefon... brak zasięgu.", "Phone... no signal.", 2.5f);
        FinishIntro();
    }

    void SkipIntro()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        sfx.Stop();
        FinishIntro();
    }

    void FinishIntro()
    {
        if (surfaceRoot != null) surfaceRoot.SetActive(false);
        introSeen = true;
        var p = P;
        if (p != null) p.SetLook(p.transform.eulerAngles.y, 0f);
        EndCutscene();
        if (GameManager.Instance != null) GameManager.Instance.ShowIntro();
    }

    // =====================================================================
    //  ZAKOŃCZENIE
    // =====================================================================

    IEnumerator Ending()
    {
        CurrentPhase = Phase.Ending;
        checkpoint = false;
        var p = P;
        p.infiniteStamina = false;
        BeginCutscene(false, false);
        Objective = null;
        Transform cam = p.cameraTransform;
        var s = FindStalker();
        if (s != null) { s.allowDuringCutscene = true; s.Scare(); } // w pościgu = zatrzymanie na chwilę
        Color echo1 = new Color(0.45f, 0.9f, 1f), echo2 = new Color(1f, 0.85f, 0.3f), kuba = new Color(1f, 0.8f, 0.4f);

        // 1. agenci + reflektor odpędza potwora
        Quaternion from = cam.rotation;
        Vector3 agents = agentsLook != null ? agentsLook.position : cam.position + cam.forward;
        Say("ECHO-2", echo2, "Kontakt za nim! Reflektory – TERAZ!", "Contact behind him! Floodlights – NOW!", 2.2f);
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            cam.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(agents - cam.position), Mathf.SmoothStep(0f, 1f, t / 0.8f));
            yield return null;
        }
        float baseFlood = agentFloodlight != null ? agentFloodlight.intensity : 0f;
        if (GameManager.Instance != null) GameManager.Instance.Flash(new Color(1f, 1f, 1f, 0.7f), 0.5f);
        if (s != null) s.Repel(agentFloodlight != null ? agentFloodlight.transform.position : p.transform.position);
        sfx.PlayOneShot(ProceduralAudio.Whoosh(0.8f), 0.7f);
        t = 0f;
        Quaternion atAgents = cam.rotation;
        while (t < 2.2f)
        {
            t += Time.deltaTime;
            if (agentFloodlight != null) agentFloodlight.intensity = baseFlood * Mathf.Lerp(8f, 2f, t / 2.2f);
            // obejrzyj się za uciekającym potworem
            if (s != null && t > 0.3f)
            {
                Vector3 to = s.Head.position - cam.position;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(to), Time.deltaTime * 4f);
            }
            yield return null;
        }
        if (s != null) s.Vanish();
        if (sirenSrc != null) StartCoroutine(FadeOut(sirenSrc, 2f));

        // 2. do portalu
        Say("ECHO-1", echo1, "Odpędzony. Ocalały – do portalu, szybko! Przejście nie wytrzyma długo.", "It's driven off. Survivor – into the portal, quickly! The passage won't hold long.", 4f);
        p.ResetCameraLocal();
        Vector3 startPos = p.transform.position;
        Vector3 goal = portalWalkPoint != null ? portalWalkPoint.position : startPos + p.transform.forward * 6f;
        goal.y = startPos.y;
        humSrc.clip = ProceduralAudio.PortalHum();
        humSrc.volume = 0f;
        humSrc.Play();
        t = 0f;
        float walk = Mathf.Max(2f, Vector3.Distance(startPos, goal) / 2.4f);
        float startYaw = p.transform.eulerAngles.y;
        while (t < walk)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / walk);
            p.SetCutscenePosition(Vector3.Lerp(startPos, goal, u));
            Vector3 dir = goal - startPos; dir.y = 0f;
            float yaw = Mathf.LerpAngle(startYaw, Quaternion.LookRotation(dir).eulerAngles.y, Mathf.Clamp01(t / 0.8f));
            p.SetLook(yaw, Mathf.Lerp(0f, -4f, u));
            humSrc.volume = u * 0.8f;
            fade = Mathf.Clamp01((u - 0.75f) / 0.25f);
            fadeColor = Color.white;
            yield return null;
        }
        sfx.PlayOneShot(ProceduralAudio.Whoosh(1.6f), 1f);
        fade = 1f;
        yield return new WaitForSeconds(1f);

        // 3. prawdziwy świat
        if (surfaceRoot != null) surfaceRoot.SetActive(true);
        if (introSet != null) introSet.SetActive(false);
        if (endingSet != null) endingSet.SetActive(true);
        RenderSettings.ambientLight = new Color(0.09f, 0.1f, 0.14f);
        RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.07f);
        RenderSettings.fogDensity = 0.02f;
        var arms = Arms;
        if (arms != null) arms.gameObject.SetActive(false);
        ShowBodyThirdPerson(p);
        p.SetCutscenePosition(endPlayerStart.position);
        p.SetLook(endPlayerStart.eulerAngles.y, 0f);
        cutCam = MakeCutsceneCamera(p.GetComponentInChildren<Camera>());
        humSrc.volume = 0.6f;
        t = 0f;
        float shot = 11f;
        bool l1 = false, l2 = false, l3 = false;
        Vector3 ws = endPlayerStart.position, we = endPlayerWalkTo != null ? endPlayerWalkTo.position : ws;
        while (t < shot)
        {
            t += Time.deltaTime;
            fade = Mathf.Clamp01(1f - t / 1.5f);
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.8f) / 6f));
            p.SetCutscenePosition(Vector3.Lerp(ws, we, u));
            Vector3 camPos = Vector3.Lerp(endCamStart.position, endCamEnd.position, Mathf.SmoothStep(0f, 1f, t / shot));
            Vector3 lookAt = Vector3.Lerp(endCamLook.position, p.transform.position + Vector3.up * 1.4f, 0.5f);
            cutCam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookAt - camPos));
            if (!l1 && t > 1.5f) { l1 = true; Say("ECHO-1", echo1, "Agencja ECHO, raport 0412: ocalały odzyskany. Strefa „Izolacja” – zamykamy przejście.", "ECHO Agency, report 0412: survivor recovered. \"Isolation\" zone – closing the passage.", 4.2f); }
            if (!l2 && t > 6f) { l2 = true; Say("JACK", kuba, "Stary?! Nie było cię trzy dni!", "Dude?! You've been gone for three days!", 3f); }
            if (!l3 && t > 9f) { l3 = true; Say("ECHO-1", echo1, "Obiekt „Pracownik” – nie odnaleziony. Na razie.", "Subject \"Employee\" – not found. For now.", 3f); }
            if (endPortal != null && t > 8.5f)
                endPortal.localScale = Vector3.one * Mathf.Lerp(1f, 0.01f, Mathf.SmoothStep(0f, 1f, (t - 8.5f) / 1.8f));
            if (t > 8.5f) humSrc.volume = Mathf.Lerp(0.6f, 0f, (t - 8.5f) / 1.8f);
            yield return null;
        }
        humSrc.Stop();
        sfx.PlayOneShot(ProceduralAudio.Whoosh(0.9f), 0.6f);

        // 4. napisy
        fadeColor = Color.black;
        t = 0f;
        while (t < 1.5f) { t += Time.deltaTime; fade = t / 1.5f; yield return null; }
        fade = 1f;
        subtitle = null;
        credits = true;
        yield return new WaitForSeconds(6f);
        sfx.PlayOneShot(ProceduralAudio.Thump(0.25f, 60f, 4), 0.8f);
        yield return new WaitForSeconds(0.45f);
        sfx.PlayOneShot(ProceduralAudio.Thump(0.25f, 60f, 5), 0.8f);
        yield return new WaitForSeconds(3.5f);

        // 5. powrót do menu głównego (osobna scena – następna gra losuje nową mapę)
        introSeen = false;
        checkpoint = false;
        MenuSystem.skipMainMenuOnce = false;
        PlayerController.CutsceneLock = false;
        MenuSystem.LoadScene(MapPicker.MenuScene);
    }

    IEnumerator FadeOut(AudioSource a, float time)
    {
        float v = a.volume;
        for (float t = 0f; t < time; t += Time.deltaTime) { a.volume = Mathf.Lerp(v, 0f, t / time); yield return null; }
        a.Stop();
    }

    static void ShowBodyThirdPerson(PlayerController p)
    {
        if (p.bodyRoot == null) return;
        p.bodyRoot.gameObject.SetActive(true);
        foreach (var r in p.bodyRoot.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    static Camera MakeCutsceneCamera(Camera source)
    {
        var go = new GameObject("Cutscene Camera");
        var c = go.AddComponent<Camera>();
        if (source != null)
        {
            c.CopyFrom(source);
            source.enabled = false;
            var al = source.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
        c.fieldOfView = 50f;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        c.farClipPlane = 120f;
        go.AddComponent<AudioListener>();
        // post-processing URP (refleksja – działa też bez URP)
        System.Type t = null;
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData", false);
            if (t != null) break;
        }
        if (t != null)
        {
            var d = go.GetComponent(t);
            if (d == null) d = go.AddComponent(t);
            var pp = t.GetProperty("renderPostProcessing");
            if (pp != null && pp.CanWrite) pp.SetValue(d, true);
        }
        AeroFog.Apply(GameSettings.volumetrics, c);
        return c;
    }

    // =====================================================================
    //  GUI przerywników
    // =====================================================================

    void Styles()
    {
        float k = Screen.height / 1080f;
        if (subStyle != null && Mathf.Abs(k - styleK) < 0.001f) return;
        styleK = k;
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * k), alignment = TextAnchor.UpperCenter, wordWrap = true };
        subStyle.normal.textColor = Color.white;
        nameStyle = new GUIStyle(subStyle) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(24 * k) };
        bigStyle = new GUIStyle(subStyle) { fontSize = Mathf.RoundToInt(84 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        smallStyle = new GUIStyle(subStyle) { fontSize = Mathf.RoundToInt(24 * k), alignment = TextAnchor.MiddleCenter };
        smallStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = Color.white;
    }

    void Text(Rect r, string s, GUIStyle st, Color c)
    {
        GUI.color = new Color(0f, 0f, 0f, c.a * 0.85f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        GUI.color = c;
        GUI.Label(r, s, st);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        bool showSub = !string.IsNullOrEmpty(subtitle) && Time.time < subtitleUntil;
        if (!cutscene && !credits && !showSub) return;
        if (MenuSystem.IsOpen) return;
        Styles();
        GUI.depth = -5;
        float sw = Screen.width, sh = Screen.height, k = styleK;

        if (rec)
        {
            float m = 50f * k;
            bool blink = Mathf.Repeat(Time.time, 1f) < 0.6f;
            if (blink) Fill(new Rect(m, m, 22 * k, 22 * k), new Color(0.9f, 0.1f, 0.1f, 0.9f));
            Text(new Rect(m + 30 * k, m - 8 * k, 300 * k, 40 * k), "REC", nameStyle, Color.white);
            float e = Time.time - recStart;
            Text(new Rect(sw - 420 * k, m - 8 * k, 380 * k, 40 * k), $"20.11.2026  23:41:{(int)e % 60:00}", nameStyle, Color.white);
            Color line = new Color(1f, 1f, 1f, 0.5f);
            Fill(new Rect(m, sh - m - 40 * k, 60 * k, 3 * k), line);
            Fill(new Rect(sw - m - 60 * k, sh - m - 40 * k, 60 * k, 3 * k), line);
        }

        if (letterbox && !credits)
        {
            float bar = sh * 0.1f;
            Fill(new Rect(0, 0, sw, bar), Color.black);
            Fill(new Rect(0, sh - bar, sw, bar), Color.black);
        }

        if (fade > 0.001f)
        {
            var c = fadeColor; c.a = fade;
            Fill(new Rect(0, 0, sw, sh), c);
        }

        if (credits)
        {
            Text(new Rect(0, sh * 0.3f, sw, 120 * k), "THE BACKROOMS: ISOLATION", bigStyle, Color.white);
            Text(new Rect(0, sh * 0.45f, sw, 40 * k), Loc.T("Dziękujemy za grę!", "Thank you for playing!"), smallStyle, Color.white);
            Text(new Rect(0, sh * 0.52f, sw, 40 * k), Loc.T("Agencja ECHO – Służba Odzyskiwania Zaginionych", "ECHO Agency – Missing Persons Recovery Service"), smallStyle, new Color(0.45f, 0.9f, 1f));
            Text(new Rect(0, sh * 0.68f, sw, 40 * k), Loc.T("...coś wciąż stuka w rurach.", "...something is still knocking in the pipes."), smallStyle, new Color(0.7f, 0.15f, 0.12f));
            return;
        }

        if (showSub)
        {
            float w = sw * 0.7f, y = sh * 0.8f;
            if (!string.IsNullOrEmpty(speaker))
                Text(new Rect((sw - w) / 2f, y - 34 * k, w, 34 * k), speaker, nameStyle, speakerColor);
            Text(new Rect((sw - w) / 2f, y, w, 120 * k), subtitle, subStyle, Color.white);
        }

        if (cutscene && skippable)
            Text(new Rect(sw - 360 * k, sh - 50 * k, 330 * k, 40 * k), Loc.T("[Spacja] pomiń", "[Space] skip"), smallStyle, new Color(1f, 1f, 1f, 0.6f));
    }
}
