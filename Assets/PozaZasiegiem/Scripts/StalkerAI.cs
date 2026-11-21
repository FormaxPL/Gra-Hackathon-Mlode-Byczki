using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI Stalkera w stylu Alien Isolation.
///
///  NA ZIEMI
///  - Patrol: chodzi losowo; co jakiś czas "Reżyser" wysyła go W OKOLICE gracza (nie prosto na niego).
///  - Investigate: usłyszał hałas / coś mignęło / zobaczył snop latarki – idzie sprawdzić.
///  - Chase: widzi gracza – goni. Przez memoryTime s po zgubieniu wzroku dalej "wie", gdzie jesteś.
///  - Hunt: widział, jak chowasz się do kryjówki – wyciąga cię.
///  - MoveToLastKnown + Search: przeszukuje okolicę, czasem obwąchuje kryjówki (InspectHiding).
///    Jeśli siedzisz w środku i NIE wstrzymasz oddechu (Spacja) – koniec.
///
///  RUROCIĄG (rury pod sufitem)
///  - GoToHatch -> EnterPipe -> InPipe -> ExitPipe: wchodzi przez właz do rury, przemieszcza się po
///    rurociągu (stukanie, drżenie rur, kurz, wykrywacz go widzi) i zeskakuje z włazu niedaleko gracza.
///  - Ambush: ZASADZKA – czai się nieruchomo w rurze nad włazem na trasie gracza. Słychać mruczenie,
///    z otworu kapie ślina, w ciemności błyszczą oczy. Kto przejdzie pod spodem – ginie.
///  - "Menace" (napięcie): gdy długo jest blisko gracza, wycofuje się do rur, a potem wraca.
///    Gaśnica też przegania go do rur.
///
/// Wszystkie liczby da się stroić w Inspectorze. F1 w grze = podgląd stanu.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class StalkerAI : MonoBehaviour
{
    public enum State
    {
        Patrol, Investigate, Chase, Hunt, MoveToLastKnown, Search, InspectHiding, Flee,
        GoToHatch, EnterPipe, InPipe, ExitPipe, Ambush, Kill
    }
    public static readonly List<StalkerAI> All = new List<StalkerAI>();

    [Header("Referencje")]
    public Transform eyes;
    [Tooltip("Model potwora – chowany, gdy jest w rurach")]
    public GameObject model;
    public ProceduralRig rig;
    [Tooltip("Głowa nowego modelu (entity.fbx) – kamera przy jumpscare patrzy na nią")]
    public Transform headOverride;
    [Tooltip("Działa także podczas przerywnika (zakończenie)")]
    [System.NonSerialized] public bool allowDuringCutscene;
    [Tooltip("Drugie źródło dźwięku – zapętlony oddech (puste = utworzy się samo)")]
    public AudioSource breathSource;

    [Header("Prędkości")]
    public float patrolSpeed = 1.8f;
    public float investigateSpeed = 3f;
    public float chaseSpeed = 4.4f;
    public float fleeSpeed = 6f;

    [Header("Wzrok")]
    public float viewDistance = 13f;
    public float viewDistanceFlashlight = 20f;
    public float viewAngle = 60f;
    [Tooltip("Z tak bliska wyczuje cię niezależnie od kąta patrzenia")]
    public float senseDistance = 2.5f;
    [Tooltip("Mnożnik zasięgu wzroku, gdy gracz kuca")]
    public float crouchVisibility = 0.65f;
    [Tooltip("Ile mniej więcej sekund patrzenia potrzebuje, żeby zacząć pościg")]
    public float detectTime = 0.6f;
    [Tooltip("Zauważa snop latarki skierowany na niego")]
    public bool noticesFlashlight = true;

    [Header("Słuch")]
    public bool canHear = true;

    [Header("Pościg")]
    public float catchDistance = 1.3f;
    public float memoryTime = 3f;

    [Header("Szukanie i kryjówki")]
    public float searchDuration = 14f;
    public float searchRadius = 8f;
    [Range(0, 1)] public float inspectChance = 0.35f;
    [Tooltip("Szansa, że sprawdzi akurat TWOJĄ kryjówkę, jeśli jesteś w pobliżu")]
    [Range(0, 1)] public float inspectCheatChance = 0.6f;
    public float inspectRadius = 7f;
    public float inspectDuration = 4f;

    [Header("Reżyser (jak w Alien Isolation)")]
    public float gracePeriod = 20f;
    public float directorMinInterval = 12f;
    public float directorMaxInterval = 22f;
    public float directorRadius = 10f;
    [Tooltip("Jeśli gracz jest dalej niż tyle – Reżyser woli wysłać potwora rurami")]
    public float directorPipeDistance = 20f;
    [Tooltip("Ile sekund bycia blisko gracza, zanim wycofa się do rur")]
    public float menaceMax = 45f;

    [Header("Rurociąg")]
    public bool usePipes = true;
    public bool startInPipes = true;
    public float pipeSpeed = 3.2f;
    public float climbTime = 1.1f;
    public float dropTime = 0.55f;
    public Vector2 retreatTime = new Vector2(15f, 30f);
    public float exitMinDistance = 7f;
    public float exitMaxDistance = 18f;

    [Header("Zasadzka w rurach")]
    [Range(0, 1)] public float ambushChance = 0.35f;
    public Vector2 ambushDuration = new Vector2(18f, 30f);
    [Tooltip("Gracz bliżej włazu niż tyle (w poziomie) = potwór spada mu na głowę")]
    public float ambushDropRadius = 1.6f;
    public float ambushCooldown = 40f;
    public float ambushMinDistance = 5f;
    public float ambushMaxDistance = 14f;

    [Header("Ucieczka po gaśnicy")]
    public float fleeDuration = 6f;
    public float fleeCooldown = 20f;

    [Header("Dźwięki (puste = wygenerowane)")]
    public AudioClip stepClip;
    public AudioClip screechClip;
    public AudioClip growlClip;
    public AudioClip clankClip;
    public AudioClip breathClip;
    public AudioClip clickClip;
    public AudioClip hissClip;

    [Header("Debug")]
    public bool logStates = true;

    public State CurrentState => state;
    public bool CanSeePlayer { get; private set; }
    public float Suspicion => suspicion;
    public float Menace => menace;
    public float DistanceToPlayer { get; private set; }
    public bool IsMoving => velocity.sqrMagnitude > 0.09f;
    public Vector3 Velocity => velocity;
    public bool InPipes => state == State.EnterPipe || state == State.InPipe || state == State.ExitPipe || state == State.Ambush;
    public bool IsHuntingPlayer => state == State.Chase || state == State.Hunt;
    public ProceduralRig Rig => rig;
    public Transform Head => headOverride != null ? headOverride : rig != null && rig.head != null ? rig.head : (eyes != null ? eyes : transform);
    public bool ForcedChase => forcedChase;
    bool forcedChase, vanished;
    float stunUntil;
    float forcedRunSpeed = 5.2f;
    public Vector3 LastAmbushPoint { get; private set; }

    NavMeshAgent agent;
    PlayerController player;
    PipeNetwork net;
    State state;
    float stateTime, lastSeenTime = -999f, suspicion, menace, nextDirectorTime, nextRepath, stepAcc;
    float searchEndTime, inspectStart, pipeRoamUntil, pipeRoamDuration, ambushUntil, nextAmbushAllowed;
    float nextClank, nextGrowl, nextClick, nextDrip, nextLurkSound, nextDust;
    bool inspectArrived, pipeFlee, pipeExiting, pipeAmbushPlanned, chaseAfterDrop, litByPlayer;
    Vector3 lastKnownPos, lastPos, velocity, animFrom, animTo;
    HidingSpot huntSpot, inspectSpot, lastInspected;
    PipeHatch targetHatch, currentHatch;
    readonly List<int> path = new List<int>();
    int pathIndex, currentNode = -1;
    AudioSource audioSrc;
    Animator animator;
    bool animHasSpeed;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            Setup3D(audioSrc, 30f);
        }
        if (breathSource == null || breathSource == audioSrc)
        {
            breathSource = gameObject.AddComponent<AudioSource>();
            Setup3D(breathSource, 14f);
        }
        if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var p in animator.parameters)
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) animHasSpeed = true;

        if (stepClip == null) stepClip = ProceduralAudio.Thump(0.22f, 48f, 3);
        if (screechClip == null) screechClip = ProceduralAudio.Screech(1.1f);
        if (growlClip == null) growlClip = ProceduralAudio.Growl(1.6f);
        if (clankClip == null) clankClip = ProceduralAudio.Clank(17, 0.4f);
        if (breathClip == null) breathClip = ProceduralAudio.Breathing(3.6f);
        if (clickClip == null) clickClip = ProceduralAudio.Clicks(21);
        if (hissClip == null) hissClip = ProceduralAudio.Hiss(1.2f);
        breathSource.clip = breathClip;
        breathSource.loop = true;
        breathSource.volume = 0f;
        lastPos = transform.position;
    }

    static void Setup3D(AudioSource a, float maxDistance)
    {
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 2f;
        a.maxDistance = maxDistance;
    }

    void Start()
    {
        player = PlayerController.Instance;
        net = PipeNetwork.Instance;
        NoiseSystem.OnNoise += OnNoise;
        nextDirectorTime = Time.time + gracePeriod;
        nextAmbushAllowed = Time.time + gracePeriod + 25f;

        PipeHatch startHatch = null;
        if (usePipes && startInPipes && net != null && player != null)
        {
            float best = -1f;
            foreach (var h in PipeHatch.All)
            {
                if (h.node < 0 || !h.TryGetFloor(out _)) continue;
                float d = Vector3.Distance(h.transform.position, player.transform.position);
                if (d > best) { best = d; startHatch = h; }
            }
        }

        if (startHatch != null)
        {
            // Start w rurach: gracz najpierw tylko SŁYSZY coś nad sufitem.
            agent.enabled = false;
            transform.position = startHatch.InsidePoint;
            currentHatch = startHatch;
            currentNode = startHatch.node;
            SetModelVisible(false);
            pipeRoamUntil = Time.time + gracePeriod;
            SetState(State.InPipe);
            Log("Start w rurociągu");
        }
        else
        {
            EnsureOnNavMesh();
            SetState(State.Patrol);
        }
    }

    void OnDestroy() => NoiseSystem.OnNoise -= OnNoise;

    void EnsureOnNavMesh()
    {
        if (agent.enabled && agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            transform.position = hit.position;
        agent.enabled = true;
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out hit, 6f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    void Update()
    {
        if (player == null || state == State.Kill || vanished) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (PlayerController.CutsceneLock && !allowDuringCutscene)
        {
            // przerywnik: potwór czeka, a "reżyser" nie odlicza czasu
            nextDirectorTime += dt;
            nextAmbushAllowed += dt;
            pipeRoamUntil += dt;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        var gm = GameManager.Instance;
        if (gm != null && (gm.IsOver || gm.IsDying))
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (breathSource != null) breathSource.volume = Mathf.MoveTowards(breathSource.volume, 0f, Time.unscaledDeltaTime);
            return;
        }

        stateTime += dt;
        velocity = (transform.position - lastPos) / dt;
        lastPos = transform.position;

        if (InPipes)
        {
            CanSeePlayer = false;
            litByPlayer = false;
            DistanceToPlayer = FlatDist(player.transform.position);
        }
        else
        {
            if (!agent.enabled || !agent.isOnNavMesh) { EnsureOnNavMesh(); if (!agent.isOnNavMesh) return; }
            CanSeePlayer = CheckVision();
            litByPlayer = noticesFlashlight && !CanSeePlayer && CheckLit();
            if (CanSeePlayer) { lastSeenTime = Time.time; lastKnownPos = player.transform.position; }
        }

        UpdateMenace(dt);

        switch (state)
        {
            case State.Patrol: UpdatePatrol(dt); break;
            case State.Investigate: UpdateInvestigate(dt); break;
            case State.Chase: UpdateChase(); break;
            case State.Hunt: UpdateHunt(); break;
            case State.MoveToLastKnown: UpdateMoveToLastKnown(dt); break;
            case State.Search: UpdateSearch(dt); break;
            case State.InspectHiding: UpdateInspect(dt); break;
            case State.Flee: UpdateFlee(); break;
            case State.GoToHatch: UpdateGoToHatch(dt); break;
            case State.EnterPipe: UpdateEnterPipe(); break;
            case State.InPipe: UpdateInPipe(dt); break;
            case State.ExitPipe: UpdateExitPipe(); break;
            case State.Ambush: UpdateAmbush(); break;
        }
        UpdateFeedback(dt);
    }

    // =====================================================================
    //  STANY NA ZIEMI
    // =====================================================================

    void UpdatePatrol(float dt)
    {
        agent.speed = patrolSpeed;
        if (Detect(dt, 1f)) return;
        if (suspicion > 0.35f) { GoInvestigate(lastKnownPos, "coś mignęło"); return; }
        if (menace >= menaceMax && TryRetreat("za długo przy graczu – wycofuje się do rur")) return;

        if (Time.time >= nextDirectorTime)
        {
            nextDirectorTime = Time.time + Random.Range(directorMinInterval, directorMaxInterval);
            if (usePipes && DistanceToPlayer > directorPipeDistance && Random.value < 0.7f
                && TryGoToHatch(NearestHatch(transform.position), 0f, false))
            {
                Log("Reżyser: skrót rurami do gracza");
                return;
            }
            GoTo(RandomPointNear(player.transform.position, directorRadius));
            Log("Reżyser: idę w okolice gracza");
        }
        else if (Arrived())
        {
            GoTo(RandomPointNear(transform.position, 18f));
        }
    }

    void UpdateInvestigate(float dt)
    {
        agent.speed = investigateSpeed;
        if (Detect(dt, 1.5f)) return;
        if (Arrived()) StartSearch();
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        if (forcedChase) { UpdateForcedChase(); return; }

        if (player.IsHidden)
        {
            if (lastSeenTime >= player.HideTime - 0.75f && player.CurrentSpot != null)
            {
                huntSpot = player.CurrentSpot;
                SetState(State.Hunt);
                GoTo(huntSpot.ExitPosition);
                Log("Widział, jak się chowasz!");
            }
            else
            {
                SetState(State.MoveToLastKnown);
                GoTo(lastKnownPos);
                Log("Gracz zniknął");
            }
            return;
        }

        bool knows = CanSeePlayer || Time.time - lastSeenTime < memoryTime;
        if (knows)
        {
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.15f;
                agent.SetDestination(player.transform.position);
            }
            if (FlatDist(player.transform.position) <= catchDistance)
                Catch(Loc.T("Dopadło cię.", "It got you."), Random.value < 0.5f ? KillStyle.Lunge : KillStyle.Twist);
        }
        else
        {
            SetState(State.MoveToLastKnown);
            GoTo(lastKnownPos);
            Log("Zgubił gracza");
        }
    }

    /// <summary>Finałowy pościg: zawsze wie, gdzie jesteś, i trzyma się tuż za plecami (gumka).</summary>
    void UpdateForcedChase()
    {
        float d = FlatDist(player.transform.position);
        float k = d > 11f ? 1.2f : d > 6f ? 1f : d < 3.5f ? 0.82f : 0.92f;
        agent.speed = forcedRunSpeed * k;
        agent.isStopped = Time.time < stunUntil;
        lastSeenTime = Time.time;
        lastKnownPos = player.transform.position;
        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + 0.12f;
            agent.SetDestination(player.transform.position);
        }
        if (d <= catchDistance && Time.time >= stunUntil)
            Catch(Loc.T("Dopadło cię tuż przed wyjściem.", "It caught you right before the exit."), KillStyle.Lunge);
    }

    /// <summary>Start finałowego pościgu: potwór pojawia się w podanym miejscu i biegnie za graczem.</summary>
    public void ForceChase(Vector3 spawn, float runSpeed)
    {
        if (player == null) player = PlayerController.Instance;
        forcedChase = true;
        vanished = false;
        forcedRunSpeed = runSpeed;
        currentHatch = null;
        SetModelVisible(true);
        agent.enabled = true;
        if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 4f, NavMesh.AllAreas)) agent.Warp(hit.position);
        else agent.Warp(spawn);
        agent.isStopped = false;
        suspicion = 1f;
        menace = 0f;
        lastSeenTime = Time.time;
        lastKnownPos = player.transform.position;
        SetState(State.Chase);
        nextRepath = 0f;
        if (audioSrc != null) { audioSrc.pitch = 0.9f; audioSrc.PlayOneShot(screechClip, 1f); }
        if (FearFeedback.Instance != null) FearFeedback.Instance.PlayStinger();
        Log("FINAŁOWY POŚCIG!");
    }

    /// <summary>Agenci odpędzają potwora światłem: krzyk i ucieczka w głąb tunelu.</summary>
    public void Repel(Vector3 awayFrom)
    {
        forcedChase = false;
        allowDuringCutscene = true;
        if (audioSrc != null) { audioSrc.pitch = 1.3f; audioSrc.PlayOneShot(screechClip, 1f); }
        PlayVoice(hissClip, 0.9f, 1f);
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        SetState(State.Flee);
        Vector3 away = transform.position - awayFrom;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        GoTo(RandomPointNear(transform.position + away.normalized * 14f, 2f));
    }

    /// <summary>Potwór znika na dobre (koniec gry).</summary>
    public void Vanish()
    {
        vanished = true;
        forcedChase = false;
        SetModelVisible(false);
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        if (breathSource != null) breathSource.volume = 0f;
    }

    void UpdateHunt()
    {
        agent.speed = chaseSpeed;
        if (huntSpot == null || !player.IsHidden || player.CurrentSpot != huntSpot)
        {
            SetState(State.Chase);
            return;
        }
        if (FlatDist(huntSpot.ExitPosition) <= catchDistance + 0.3f) Catch(Loc.T("Wyciągnęło cię z kryjówki.", "It dragged you out of your hiding spot."), KillStyle.Locker);
    }

    void UpdateMoveToLastKnown(float dt)
    {
        agent.speed = investigateSpeed;
        if (Detect(dt, 2f)) return;
        if (Arrived()) StartSearch();
    }

    void UpdateSearch(float dt)
    {
        agent.speed = patrolSpeed * 1.2f;
        if (Detect(dt, 2f)) return;
        if (Time.time > searchEndTime)
        {
            if (usePipes && Random.value < 0.45f && TryRetreat("koniec szukania – wraca do rur")) return;
            SetState(State.Patrol);
            nextDirectorTime = Time.time + Random.Range(directorMinInterval, directorMaxInterval);
            Log("Koniec szukania");
            return;
        }
        if (Arrived())
        {
            if (TryStartInspect()) return;
            GoTo(RandomPointNear(lastKnownPos, searchRadius));
        }
    }

    bool TryStartInspect()
    {
        HidingSpot pick = null;
        if (player.IsHidden && player.CurrentSpot != null && player.CurrentSpot != lastInspected
            && Vector3.Distance(transform.position, player.CurrentSpot.transform.position) < inspectRadius
            && Random.value < inspectCheatChance)
        {
            pick = player.CurrentSpot;
        }
        else if (Random.value < inspectChance)
        {
            float best = inspectRadius;
            foreach (var h in HidingSpot.All)
            {
                if (h == lastInspected) continue;
                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < best) { best = d; pick = h; }
            }
        }
        if (pick == null) return false;

        inspectSpot = pick;
        lastInspected = pick;
        inspectArrived = false;
        SetState(State.InspectHiding);
        GoTo(pick.ExitPosition);
        Log("Obwąchuje kryjówkę");
        return true;
    }

    void UpdateInspect(float dt)
    {
        agent.speed = investigateSpeed * 0.8f;
        if (inspectSpot == null) { ResumeSearch(); return; }
        if (Detect(dt, 2f)) { agent.isStopped = false; return; }

        if (!inspectArrived)
        {
            if (Arrived() || FlatDist(inspectSpot.ExitPosition) < 0.9f)
            {
                inspectArrived = true;
                inspectStart = Time.time;
                agent.isStopped = true;
                PlayVoice(growlClip, 0.8f, 0.9f);
            }
            else if (stateTime > 12f) ResumeSearch();
            return;
        }

        Vector3 dir = inspectSpot.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 5f);

        bool playerInside = player.IsHidden && player.CurrentSpot == inspectSpot;
        float t = Time.time - inspectStart;
        if (playerInside && t > 1f && (!player.IsHoldingBreath || player.LastGaspTime > inspectStart))
        {
            Catch(Loc.T("Usłyszało twój oddech.", "It heard you breathing."), KillStyle.Locker);
            return;
        }
        if (t > inspectDuration)
        {
            agent.isStopped = false;
            ResumeSearch();
        }
    }

    void ResumeSearch()
    {
        inspectSpot = null;
        state = State.Search;
        stateTime = 0f;
        GoTo(RandomPointNear(lastKnownPos, searchRadius));
    }

    void UpdateFlee()
    {
        agent.speed = fleeSpeed;
        if (stateTime > fleeDuration)
        {
            SetState(State.Patrol);
            nextDirectorTime = Time.time + fleeCooldown;
        }
    }

    // =====================================================================
    //  RUROCIĄG
    // =====================================================================

    bool TryRetreat(string reason)
    {
        if (!usePipes) return false;
        if (!TryGoToHatch(NearestHatch(transform.position), Random.Range(retreatTime.x, retreatTime.y), false)) return false;
        menace = 0f;
        Log(reason);
        return true;
    }

    bool TryGoToHatch(PipeHatch h, float roamDuration, bool flee)
    {
        if (h == null || net == null || h.node < 0 || !h.TryGetFloor(out Vector3 floor)) return false;
        targetHatch = h;
        pipeRoamDuration = roamDuration;
        pipeFlee = flee;
        SetState(State.GoToHatch);
        GoTo(floor);
        return true;
    }

    void UpdateGoToHatch(float dt)
    {
        agent.speed = pipeFlee ? fleeSpeed : investigateSpeed;
        if (!pipeFlee && Detect(dt, 1f)) return;
        if (targetHatch == null || !targetHatch.TryGetFloor(out Vector3 floor)) { SetState(State.Patrol); return; }
        if (Arrived() || FlatDist(floor) < 0.8f)
        {
            agent.enabled = false;
            animFrom = transform.position;
            animTo = targetHatch.InsidePoint;
            Vector3 look = targetHatch.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            targetHatch.Disturb(1f);
            if (rig != null) rig.climb = 1f;
            SetState(State.EnterPipe);
            return;
        }
        if (stateTime > 25f) { SetState(State.Patrol); Log("Nie mógł dojść do włazu"); }
    }

    void UpdateEnterPipe()
    {
        float t = Mathf.Clamp01(stateTime / climbTime);
        transform.position = Vector3.Lerp(animFrom, animTo, Mathf.SmoothStep(0f, 1f, t));
        if (t < 1f) return;
        SetModelVisible(false);
        if (rig != null) rig.climb = 0f;
        currentHatch = targetHatch;
        currentNode = currentHatch != null ? currentHatch.node : (net != null ? net.NearestNode(transform.position) : -1);
        targetHatch = null;
        pipeExiting = false;
        pipeAmbushPlanned = false;
        pipeRoamUntil = Time.time + pipeRoamDuration;
        path.Clear();
        SetState(State.InPipe);
        Log("W rurociągu");
    }

    void UpdateInPipe(float dt)
    {
        if (net == null) { ForceExit(); return; }
        if (path.Count == 0 || pathIndex >= path.Count)
        {
            if (!PlanNextPipeTarget()) { ForceExit(); return; }
        }

        Vector3 target = net.nodes[path[pathIndex]];
        Vector3 to = target - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, target, pipeSpeed * dt);
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);

        // stukanie, drżenie rur, kurz z połączeń
        if (Time.time >= nextClank)
        {
            nextClank = Time.time + Random.Range(0.3f, 0.85f);
            if (audioSrc != null)
            {
                audioSrc.pitch = Random.Range(0.75f, 1.25f);
                audioSrc.PlayOneShot(clankClip, Random.Range(0.35f, 0.7f));
            }
        }
        net.ShakeNear(transform.position, 1f, 3.5f);
        if (Time.time >= nextDust)
        {
            nextDust = Time.time + Random.Range(0.6f, 1.5f);
            net.DustNear(transform.position);
        }
        foreach (var h in PipeHatch.All)
        {
            Vector3 d = h.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 4f) h.Disturb(0.7f);
        }

        if ((transform.position - target).sqrMagnitude > 0.0025f) return;
        currentNode = path[pathIndex];
        pathIndex++;
        if (pathIndex < path.Count) return;

        // dotarł do celu
        currentHatch = targetHatch;
        targetHatch = null;
        path.Clear();
        if (currentHatch == null) return;
        if (pipeAmbushPlanned) { pipeAmbushPlanned = false; StartAmbush(currentHatch); return; }
        if (pipeExiting) StartExitPipe(currentHatch);
    }

    bool PlanNextPipeTarget()
    {
        PipeHatch h;
        if (Time.time >= pipeRoamUntil)
        {
            if (!pipeFlee && Time.time >= nextAmbushAllowed && Random.value < ambushChance)
            {
                h = ChooseAmbushHatch();
                if (h != null && SetPipeTarget(h))
                {
                    pipeAmbushPlanned = true;
                    pipeExiting = false;
                    Log("Planuje zasadzkę");
                    return true;
                }
            }
            pipeExiting = true;
            pipeAmbushPlanned = false;
            h = ChooseExitHatch(player.transform.position);
        }
        else h = RandomRoamHatch();
        return h != null && SetPipeTarget(h);
    }

    bool SetPipeTarget(PipeHatch h)
    {
        if (net == null || h == null || h.node < 0) return false;
        if (currentNode < 0) currentNode = net.NearestNode(transform.position);
        var p = net.FindPath(currentNode, h.node);
        if (p == null || p.Count == 0) return false;
        targetHatch = h;
        path.Clear();
        path.AddRange(p);
        pathIndex = 0;
        return true;
    }

    PipeHatch RandomRoamHatch()
    {
        var options = new List<PipeHatch>();
        foreach (var h in PipeHatch.All)
            if (h != currentHatch && h.node >= 0 && Vector3.Distance(h.transform.position, transform.position) < 22f) options.Add(h);
        if (options.Count == 0)
            foreach (var h in PipeHatch.All) if (h != currentHatch && h.node >= 0) options.Add(h);
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    PipeHatch ChooseExitHatch(Vector3 near)
    {
        var good = new List<PipeHatch>();
        PipeHatch fallback = null;
        float fallbackScore = float.MaxValue;
        foreach (var h in PipeHatch.All)
        {
            if (h.node < 0 || !h.TryGetFloor(out _)) continue;
            float d = Vector3.Distance(Flat(h.transform.position), Flat(near));
            if (d >= exitMinDistance && d <= exitMaxDistance) good.Add(h);
            float score = Mathf.Abs(d - (exitMinDistance + exitMaxDistance) * 0.5f);
            if (score < fallbackScore) { fallbackScore = score; fallback = h; }
        }
        return good.Count > 0 ? good[Random.Range(0, good.Count)] : fallback;
    }

    /// <summary>Właz na trasie gracza (przed nim, w średniej odległości).</summary>
    PipeHatch ChooseAmbushHatch()
    {
        PipeHatch best = null;
        float bestScore = float.MinValue;
        Vector3 pp = Flat(player.transform.position);
        Vector3 fwd = player.Velocity.sqrMagnitude > 0.1f ? player.Velocity.normalized : player.transform.forward;
        foreach (var h in PipeHatch.All)
        {
            if (h.node < 0 || !h.TryGetFloor(out _)) continue;
            Vector3 to = Flat(h.transform.position) - pp;
            float d = to.magnitude;
            if (d < ambushMinDistance || d > ambushMaxDistance) continue;
            float score = Vector3.Dot(fwd, to / Mathf.Max(d, 0.01f)) + Random.value * 0.6f;
            if (score > bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    void StartAmbush(PipeHatch h)
    {
        currentHatch = h;
        LastAmbushPoint = h.InsidePoint;
        ambushUntil = Time.time + Random.Range(ambushDuration.x, ambushDuration.y);
        nextDrip = Time.time + 0.8f;
        nextLurkSound = Time.time + 2f;
        h.SetLurking(true);
        PlayVoice(hissClip, 0.8f, 0.5f);
        SetState(State.Ambush);
        Log("ZASADZKA – czai się nad włazem");
    }

    void UpdateAmbush()
    {
        var h = currentHatch;
        if (h == null) { SetState(State.InPipe); return; }
        if (Time.time >= nextDrip)
        {
            nextDrip = Time.time + Random.Range(1.2f, 3f);
            h.Drip();
        }
        if (Time.time >= nextLurkSound)
        {
            nextLurkSound = Time.time + Random.Range(4f, 8f);
            PlayVoice(Random.value < 0.5f ? clickClip : growlClip, Random.Range(0.75f, 0.95f), 0.45f);
        }

        // ofiara pod włazem?
        if (!player.IsHidden)
        {
            float d = Vector3.Distance(Flat(player.transform.position), Flat(h.transform.position));
            if (d < ambushDropRadius)
            {
                h.SetLurking(false);
                LastAmbushPoint = h.InsidePoint;
                SetModelVisible(true);
                Catch(Loc.T("Spadło na ciebie z rury.", "It dropped on you from the pipe."), KillStyle.Ceiling);
                return;
            }
        }

        if (Time.time >= ambushUntil)
        {
            h.SetLurking(false);
            nextAmbushAllowed = Time.time + ambushCooldown;
            pipeRoamUntil = Time.time + Random.Range(4f, 9f);
            path.Clear();
            SetState(State.InPipe);
            Log("Rezygnuje z zasadzki");
        }
    }

    void ForceExit()
    {
        if (currentHatch != null && currentHatch.TryGetFloor(out _)) { StartExitPipe(currentHatch); return; }
        SetModelVisible(true);
        EnsureOnNavMesh();
        SetState(State.Patrol);
    }

    void StartExitPipe(PipeHatch h)
    {
        if (h == null || !h.TryGetFloor(out Vector3 floor)) { ForceExit(); return; }
        h.SetLurking(false);
        currentHatch = h;
        animFrom = h.InsidePoint;
        animTo = floor;
        transform.position = animFrom;
        SetModelVisible(true);
        h.Disturb(1f);
        SetState(State.ExitPipe);
    }

    void UpdateExitPipe()
    {
        float t = Mathf.Clamp01(stateTime / dropTime);
        transform.position = Vector3.Lerp(animFrom, animTo, t * t);
        if (t < 1f) return;

        transform.position = animTo;
        agent.enabled = true;
        agent.Warp(animTo);
        pipeExiting = false;
        if (audioSrc != null) { audioSrc.pitch = 0.8f; audioSrc.PlayOneShot(stepClip, 1f); }
        PlayVoice(growlClip, 1f, 1f);
        float d = Vector3.Distance(transform.position, player.transform.position);
        if (d < 12f) player.AddShake(Mathf.Lerp(0.8f, 0.2f, d / 12f));
        Log("Zeskoczył z rury");

        if (chaseAfterDrop)
        {
            chaseAfterDrop = false;
            lastSeenTime = Time.time;
            lastKnownPos = player.transform.position;
            StartChase();
            return;
        }
        if (pipeFlee)
        {
            pipeFlee = false;
            SetState(State.Patrol);
            nextDirectorTime = Time.time + directorMinInterval;
            return;
        }
        // Reżyser "podpowiada" mniej więcej, gdzie jest gracz – potwór zaczyna szukać w okolicy
        lastKnownPos = RandomPointNear(player.transform.position, 6f);
        StartSearch();
    }

    // =====================================================================
    //  ZMYSŁY
    // =====================================================================

    bool Detect(float dt, float multiplier)
    {
        if (CanSeePlayer)
        {
            if (DistanceToPlayer <= senseDistance) { StartChase(); return true; }
            float closeness = 1f - Mathf.Clamp01(DistanceToPlayer / CurrentViewRange());
            suspicion += dt * multiplier * Mathf.Lerp(0.6f, 3f, closeness) / Mathf.Max(0.05f, detectTime);
            if (suspicion >= 1f) { StartChase(); return true; }
        }
        else if (litByPlayer)
        {
            suspicion += dt * multiplier * 0.8f;
            lastKnownPos = player.transform.position;
            if (suspicion >= 1f) { lastSeenTime = Time.time; StartChase(); return true; }
        }
        else
        {
            suspicion = Mathf.Max(0f, suspicion - dt * 0.3f);
        }
        return false;
    }

    bool CheckVision()
    {
        DistanceToPlayer = FlatDist(player.transform.position);
        if (player.IsHidden) return false;

        Vector3 eye = eyes != null ? eyes.position : transform.position + Vector3.up * 1.9f;
        Vector3 target = player.EyePosition - Vector3.up * 0.3f;
        Vector3 to = target - eye;
        float d = to.magnitude;

        if (d > CurrentViewRange()) return false;
        if (d > senseDistance && Vector3.Angle(transform.forward, to) > viewAngle) return false;
        if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerController>() == null) return false;
        return true;
    }

    bool CheckLit()
    {
        var fl = player.Flashlight;
        if (fl == null || !fl.IsOn || player.IsHidden) return false;
        Vector3 chest = transform.position + Vector3.up * 1.4f;
        Vector3 from = fl.transform.position;
        Vector3 to = chest - from;
        if (to.magnitude > fl.Range * 0.9f) return false;
        if (Vector3.Angle(fl.transform.forward, to) > fl.SpotAngle * 0.5f) return false;
        return !Physics.Linecast(from, chest, ~0, QueryTriggerInteraction.Ignore);
    }

    float CurrentViewRange()
    {
        float r = player.FlashlightOn ? viewDistanceFlashlight : viewDistance;
        // awaria prądu: w ciemności widzi słabiej, ale latarka świeci jak latarnia
        if (Blackout.Active) r *= player.FlashlightOn ? 1.6f : 0.6f;
        if (player.IsCrouching) r *= crouchVisibility;
        return r;
    }

    void OnNoise(Vector3 pos, float radius)
    {
        if (!canHear || player == null || state == State.Kill) return;
        float d = Vector3.Distance(transform.position, pos);

        if (state == State.Ambush)
        {
            // hałas tuż pod włazem – zeskakuje i rusza w pościg
            if (currentHatch == null || Vector3.Distance(Flat(currentHatch.transform.position), Flat(pos)) > Mathf.Min(radius, 6f)) return;
            chaseAfterDrop = true;
            Log("Usłyszał ofiarę pod włazem!");
            StartExitPipe(currentHatch);
            return;
        }
        if (state == State.InPipe)
        {
            if (d > radius * 1.3f || pipeFlee) return;
            lastKnownPos = pos;
            var h = NearestHatch(pos);
            if (h != null && SetPipeTarget(h))
            {
                pipeExiting = true;
                pipeAmbushPlanned = false;
                Log("Usłyszał coś z rur!");
            }
            return;
        }
        if (state == State.Chase || state == State.Hunt || state == State.Flee
            || state == State.EnterPipe || state == State.ExitPipe || (state == State.GoToHatch && pipeFlee)) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (d > radius) return;

        // westchnięcie z kryjówki obok = koniec zabawy
        if (player.IsHidden && player.CurrentSpot != null && Time.time - player.LastGaspTime < 0.2f
            && Vector3.Distance(transform.position, player.CurrentSpot.transform.position) < 5f)
        {
            huntSpot = player.CurrentSpot;
            SetState(State.Hunt);
            GoTo(huntSpot.ExitPosition);
            Log("Usłyszał oddech w kryjówce!");
            return;
        }
        agent.isStopped = false;
        GoInvestigate(pos + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), "hałas");
    }

    // =====================================================================
    //  AKCJE
    // =====================================================================

    public void Scare()
    {
        if (forcedChase)
        {
            // w finałowym pościgu gaśnica tylko go na chwilę zatrzymuje
            stunUntil = Time.time + 1.4f;
            if (audioSrc != null) { audioSrc.pitch = 1.4f; audioSrc.PlayOneShot(screechClip, 0.8f); }
            return;
        }
        if (player == null || InPipes || state == State.Kill || state == State.Flee || (state == State.GoToHatch && pipeFlee)) return;
        suspicion = 0f;
        menace = 0f;
        if (audioSrc != null) { audioSrc.pitch = 1.4f; audioSrc.PlayOneShot(screechClip, 0.8f); }
        PlayVoice(hissClip, 1.1f, 0.8f);
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        if (usePipes && TryGoToHatch(NearestHatch(transform.position), fleeCooldown, true))
        {
            Log("Odstraszony! Ucieka do rur");
            return;
        }
        SetState(State.Flee);
        Vector3 away = transform.position - player.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        GoTo(RandomPointNear(transform.position + away.normalized * 15f, 4f));
        Log("Odstraszony!");
    }

    /// <summary>Czy potwór zagraża graczowi w tej kryjówce (do podpowiedzi o oddechu).</summary>
    public bool IsThreateningSpot(HidingSpot s)
    {
        if (s == null || InPipes || state == State.Kill) return false;
        if (state == State.InspectHiding && inspectSpot == s) return true;
        if (state == State.Hunt && huntSpot == s) return true;
        return Vector3.Distance(transform.position, s.transform.position) < 4.5f;
    }

    /// <summary>Wywoływane przez DeathSequence – potwór przestaje "myśleć" i jest sterowany animacją.</summary>
    public void BeginKill()
    {
        state = State.Kill;
        stateTime = 0f;
        if (agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
        if (currentHatch != null) currentHatch.SetLurking(false);
        SetModelVisible(true);
        if (breathSource != null) breathSource.Stop();
    }

    /// <summary>0 = wrzask, 1 = pomruk, 2 = syk.</summary>
    public void PlayKillVoice(int kind)
    {
        switch (kind)
        {
            case 1: PlayVoice(growlClip, 0.85f, 1f); break;
            case 2: PlayVoice(hissClip, 0.9f, 1f); break;
            default: PlayVoice(screechClip, 0.95f, 1f); break;
        }
    }

    void StartChase()
    {
        if (state == State.Chase) return;
        suspicion = 1f;
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        SetState(State.Chase);
        nextRepath = 0f;
        if (audioSrc != null) { audioSrc.pitch = 1f; audioSrc.PlayOneShot(screechClip, 1f); }
        if (FearFeedback.Instance != null) FearFeedback.Instance.PlayStinger();
        if (DistanceToPlayer < 10f) player.AddShake(0.35f);
        Log("POŚCIG!");
    }

    void StartSearch()
    {
        SetState(State.Search);
        searchEndTime = Time.time + searchDuration;
        GoTo(RandomPointNear(lastKnownPos, searchRadius));
        Log("Szuka...");
    }

    void GoInvestigate(Vector3 p, string why)
    {
        lastKnownPos = p;
        SetState(State.Investigate);
        GoTo(p);
        Log("Sprawdza: " + why);
    }

    void Catch(string msg, KillStyle style)
    {
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        var gm = GameManager.Instance;
        if (gm != null) gm.Kill(this, style, msg);
    }

    // =====================================================================
    //  POMOCNICZE
    // =====================================================================

    void UpdateMenace(float dt)
    {
        if (InPipes) menace -= dt * 2f;
        else if (IsHuntingPlayer) menace += dt * 2f;
        else if (DistanceToPlayer < 15f) menace += dt;
        else menace -= dt * 0.5f;
        menace = Mathf.Clamp(menace, 0f, menaceMax * 1.2f);
    }

    void UpdateFeedback(float dt)
    {
        if (rig != null)
        {
            rig.reach = IsHuntingPlayer ? 1f : 0f;
            if (IsHuntingPlayer) rig.lookTarget = player.cameraPivot;
            else if (state == State.InspectHiding && inspectSpot != null) rig.lookTarget = inspectSpot.transform;
            else if (CanSeePlayer || litByPlayer) rig.lookTarget = player.cameraPivot;
            else rig.lookTarget = null;
        }

        bool onGround = !InPipes && agent.enabled;
        // zapętlony oddech – słychać go z bliska
        if (breathSource != null)
        {
            float target = onGround ? (IsHuntingPlayer ? 0.95f : 0.6f) : 0f;
            breathSource.volume = Mathf.MoveTowards(breathSource.volume, target, dt * 0.8f);
            if (breathSource.volume > 0.01f && !breathSource.isPlaying) breathSource.Play();
            else if (breathSource.volume <= 0.01f && breathSource.isPlaying) breathSource.Stop();
            breathSource.pitch = IsHuntingPlayer ? 1.25f : 1f;
        }
        if (!onGround) return;

        float spd = agent.velocity.magnitude;
        if (animHasSpeed) animator.SetFloat("Speed", spd);

        stepAcc += spd * dt;
        float stride = state == State.Chase ? 1.6f : 1.2f;
        if (stepAcc >= stride)
        {
            stepAcc = 0f;
            if (audioSrc != null && stepClip != null)
            {
                audioSrc.pitch = Random.Range(0.85f, 1.1f);
                audioSrc.PlayOneShot(stepClip, state == State.Chase ? 1f : 0.7f);
            }
        }

        bool searching = state == State.Search || state == State.Investigate || state == State.MoveToLastKnown || state == State.InspectHiding;
        if (searching && Time.time >= nextClick)
        {
            nextClick = Time.time + Random.Range(3f, 7f);
            PlayVoice(clickClip, Random.Range(0.85f, 1.1f), 0.7f);
        }
        if (!IsHuntingPlayer && !searching && Time.time >= nextGrowl)
        {
            nextGrowl = Time.time + Random.Range(7f, 16f);
            PlayVoice(growlClip, Random.Range(0.85f, 1.05f), 0.6f);
        }
    }

    void PlayVoice(AudioClip clip, float pitch, float volume)
    {
        if (audioSrc == null || clip == null) return;
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip, volume);
    }

    void SetModelVisible(bool visible)
    {
        if (model != null) model.SetActive(visible);
    }

    PipeHatch NearestHatch(Vector3 p)
    {
        PipeHatch best = null;
        float bestD = float.MaxValue;
        foreach (var h in PipeHatch.All)
        {
            if (h.node < 0 || !h.TryGetFloor(out _)) continue;
            float d = Vector3.Distance(h.transform.position, p);
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }

    bool Arrived()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return false;
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;
    }

    void GoTo(Vector3 p)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.SetDestination(p);
    }

    float FlatDist(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static Vector3 RandomPointNear(Vector3 center, float radius)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 rc = Random.insideUnitCircle * radius;
            Vector3 probe = new Vector3(center.x + rc.x, 0f, center.z + rc.y);
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) && hit.position.y < 0.3f)
                return hit.position;
        }
        return new Vector3(center.x, 0f, center.z);
    }

    void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    void Log(string m)
    {
        if (logStates) Debug.Log($"[Stalker] {m}  (stan: {state})", this);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 e = transform.position + Vector3.up * 1.9f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(e, Quaternion.Euler(0f, viewAngle, 0f) * transform.forward * viewDistance);
        Gizmos.DrawRay(e, Quaternion.Euler(0f, -viewAngle, 0f) * transform.forward * viewDistance);
    }
}
