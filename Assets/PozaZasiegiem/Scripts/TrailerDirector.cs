using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// TRAILER gry (~29 s), odpalany komendą: konsola (`) → „trailer”, argument „-trailer” przy starcie gry
/// albo w edytorze „Poza Zasiegiem → Odtworz trailer”. Gra się na mapie A, przez cały czas leci straszny ambient.
///  1. czerń, „Młode Byczki przedstawiają”, stukanie w rurach
///  2. nagranie z kamery (REC): przejazd korytarzem labiryntu, migające światło, szept
///  3. plansza „Do tego miejsca nie dociera sygnał.”
///  4. właz w rurze od dołu: grzechot, ślina, oczy w ciemności
///  5. plansza „Coś poluje w rurach.”
///  6. oczami bohatera: idzie, staje, rozgląda się... cisza, kapnięcie z góry, patrzy w górę –
///     potwór wyskakuje z wentylacji i atakuje (JUMPSCARE)
///  7. cięcie do czerni → tytuł gry → powrót do menu głównego
/// Miejsce akcji wybierane automatycznie: właz z wolnym korytarzem, z którego widać go z dołu.
/// </summary>
public class TrailerDirector : MonoBehaviour
{
    public const string Map = "TestLevel_A";

    /// <summary>Długości kolejnych części (s): czerń, korytarz, plansza, właz, plansza, bohater, czerń, tytuł.</summary>
    public static readonly float[] Shots = { 2.5f, 5f, 1.5f, 4f, 1.5f, 9f, 1f, 4.5f };
    public const int HeroShot = 5;

    public static float TotalLength
    {
        get { float s = 0f; foreach (var x in Shots) s += x; return s; }
    }

    /// <summary>Kiedy (od początku trailera) zaczyna się ujęcie o danym indeksie.</summary>
    public static float ShotStart(int index)
    {
        float s = 0f;
        for (int i = 0; i < index && i < Shots.Length; i++) s += Shots[i];
        return s;
    }

    public static bool Pending { get; private set; }
    public static bool Playing { get; private set; }

    /// <summary>Komenda „trailer”: wczytuje mapę i odpala trailer zamiast intro.</summary>
    public static void Request()
    {
        if (!Application.isPlaying || Playing) return;
        Pending = true;
        StoryDirector.checkpoint = false;
        MenuSystem.LoadScene(Map);
    }

    /// <summary>Wołane przez StoryDirector.OnGameStart – zamiast intro rusza trailer.</summary>
    public static bool TryBegin()
    {
        if (!Pending) return false;
        Pending = false;
        new GameObject("Trailer").AddComponent<TrailerDirector>();
        return true;
    }

    /// <summary>Wartość z klatek kluczowych (płynnie między nimi, poza zakresem – skrajne).</summary>
    public static float Keys(float t, float[] times, float[] values)
    {
        if (times.Length == 0) return 0f;
        if (t <= times[0]) return values[0];
        for (int i = 0; i < times.Length - 1; i++)
            if (t < times[i + 1])
                return Mathf.Lerp(values[i], values[i + 1], Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(times[i], times[i + 1], t)));
        return values[values.Length - 1];
    }

    // =====================================================================
    //  MIEJSCE AKCJI
    // =====================================================================

    const float EyeH = 1.65f, StandDist = 1.05f, WalkDist = 3.2f;

    struct Stage
    {
        public PipeHatch hatch;
        public Vector3 hatchPos, under, stand, from, dir; // dir: od włazu w stronę, z której przychodzi bohater
        public List<Vector3> dolly;
        public Vector3 lowA, lowB;
    }

    Stage st;

    static bool OnNav(Vector3 p, float floorY, out Vector3 q)
    {
        q = p;
        if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) return false;
        Vector3 off = hit.position - p; off.y = 0f;
        if (off.magnitude > 0.3f || Mathf.Abs(hit.position.y - floorY) > 0.3f) return false;
        q = hit.position;
        return true;
    }

    static bool Blocked(Vector3 a, Vector3 b)
    {
        float len = Vector3.Distance(a, b);
        return Physics.Linecast(a, b, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.distance < len - 0.35f;
    }

    static bool TryStage(PipeHatch h, out Stage s)
    {
        s = default;
        if (h == null || !h.TryGetFloor(out Vector3 floor)) return false;
        Vector3 hp = h.transform.position;
        if (hp.y - floor.y < 2.2f) return false;
        Vector3 under = new Vector3(hp.x, floor.y, hp.z);
        for (int i = 0; i < 16; i++)
        {
            float ang = i * Mathf.PI * 2f / 16f;
            Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (!OnNav(under + d * StandDist, floor.y, out Vector3 stand)) continue;
            if (!OnNav(under + d * (StandDist + WalkDist), floor.y, out Vector3 from)) continue;
            if (NavMesh.Raycast(from, stand, out _, NavMesh.AllAreas)) continue;
            Vector3 eyeStand = stand + Vector3.up * EyeH;
            if (Blocked(from + Vector3.up * EyeH, eyeStand)) continue;
            if (Blocked(eyeStand, hp)) continue;
            Vector3 lowA = under + d * 1.9f + Vector3.up * 0.4f, lowB = under + d * 1.35f + Vector3.up * 0.55f;
            if (Blocked(lowA, hp) || Blocked(lowA, lowB))
            {
                lowA = from + Vector3.up * 1.2f;
                lowB = eyeStand;
            }
            s = new Stage { hatch = h, hatchPos = hp, under = under, stand = stand, from = from, dir = d, lowA = lowA, lowB = lowB };
            s.dolly = DollyPath(from, d, floor.y);
            return true;
        }
        return false;
    }

    /// <summary>Droga kamery w ujęciu 2: korytarzem po NavMeshu, kończy się tam, skąd potem wyjdzie bohater.</summary>
    static List<Vector3> DollyPath(Vector3 end, Vector3 dir, float floorY)
    {
        var r = new System.Random(7);
        var path = new NavMeshPath();
        List<Vector3> best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < 40; i++)
        {
            float ang = (float)r.NextDouble() * Mathf.PI * 2f;
            float rad = 6f + (float)r.NextDouble() * 7f;
            Vector3 probe = end + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floorY) > 0.3f) continue;
            if (!NavMesh.CalculatePath(hit.position, end, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            var c = path.corners;
            if (c.Length < 2) continue;
            float len = 0f;
            for (int k = 1; k < c.Length; k++) len += Vector3.Distance(c[k - 1], c[k]);
            if (len < 6f || len > 20f) continue;
            // najlepiej, gdy kamera dojeżdża w tę samą stronę, w którą potem pójdzie bohater (do włazu)
            Vector3 lastDir = c[c.Length - 1] - c[c.Length - 2]; lastDir.y = 0f;
            float score = Vector3.Dot(lastDir.normalized, -dir) * 3f - Mathf.Abs(len - 9f) * 0.2f;
            if (score > bestScore) { bestScore = score; best = new List<Vector3>(c); }
        }
        if (best != null) return best;
        // zapas: prosto do tyłu od miejsca startu bohatera (o ile nie ma tam ściany)
        Vector3 back = end + dir * 5f;
        if (!NavMesh.Raycast(end, back, out NavMeshHit h2, NavMesh.AllAreas)) return new List<Vector3> { back, end };
        return new List<Vector3> { h2.position, end };
    }

    static Vector3 Along(List<Vector3> pts, float dist)
    {
        if (pts.Count == 1) return pts[0];
        for (int i = 1; i < pts.Count; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            if (dist <= seg || i == pts.Count - 1) return Vector3.Lerp(pts[i - 1], pts[i], seg < 0.0001f ? 1f : Mathf.Clamp01(dist / seg));
            dist -= seg;
        }
        return pts[pts.Count - 1];
    }

    static float Length(List<Vector3> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
        return len;
    }

    Stage PickStage(PlayerController p)
    {
        var hatches = new List<PipeHatch>(PipeHatch.All);
        Vector3 origin = p.transform.position;
        hatches.Sort((a, b) => Vector3.Distance(a.transform.position, origin).CompareTo(Vector3.Distance(b.transform.position, origin)));
        foreach (var h in hatches)
            if (TryStage(h, out Stage s)) return s;

        // zapas: bez włazu – potwór spada z sufitu przed graczem
        Vector3 f = p.transform.forward; f.y = 0f; f.Normalize();
        Vector3 stand = origin;
        Vector3 hatchPos = origin + f * StandDist + Vector3.up * 2.6f;
        Vector3 from = NavMesh.Raycast(stand, stand - f * WalkDist, out NavMeshHit hit, NavMesh.AllAreas) ? hit.position : stand - f * WalkDist;
        return new Stage
        {
            hatchPos = hatchPos, under = new Vector3(hatchPos.x, origin.y, hatchPos.z), stand = stand, from = from, dir = -f,
            dolly = DollyPath(from, -f, origin.y), lowA = from + Vector3.up * 1.2f, lowB = stand + Vector3.up * EyeH,
        };
    }

    // =====================================================================
    //  STAN I DŹWIĘK
    // =====================================================================

    PlayerController P => PlayerController.Instance;
    Transform cam;
    StalkerAI stalker;
    FirstPersonArms arms;
    AudioSource sfx, breath;
    AudioClip hitClip, heartbeat, boom, thump, stinger, clicks;
    AudioClip[] steps;
    AmbientScape amb;

    float black = 1f, flash, flicker, cardAlpha, titleT = -1f, shake, recStart;
    string card, cardSmall;
    bool rec, letterbox = true;
    Texture2D white;
    GUIStyle cardStyle, smallStyle, titleStyle, subStyle, recStyle;
    float styleK;

    void Awake()
    {
        Playing = true;
        white = Texture2D.whiteTexture;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        breath = gameObject.AddComponent<AudioSource>();
        breath.playOnAwake = false;
        breath.spatialBlend = 0f;
        breath.loop = true;
        breath.volume = 0f;
        breath.clip = ProceduralAudio.Breathing(4f);
        hitClip = ProceduralAudio.JumpscareHit();
        heartbeat = ProceduralAudio.Heartbeat();
        boom = ProceduralAudio.TitleBoom();
        thump = ProceduralAudio.Thump(0.8f, 38f, 21);
        stinger = ProceduralAudio.Stinger();
        clicks = ProceduralAudio.Clicks(55);
        steps = new[] { ProceduralAudio.Footstep(0, 301), ProceduralAudio.Footstep(0, 302), ProceduralAudio.Footstep(0, 303) };
    }

    void OnDestroy()
    {
        Playing = false;
        if (amb != null) { amb.scripted = false; amb.Duck(1f, 2f); }
    }

    void Start() => StartCoroutine(Run());

    // =====================================================================
    //  PRZEBIEG
    // =====================================================================

    IEnumerator Run()
    {
        // jedna klatka: wszystkie Start() na mapie (potwór, latarka...) muszą się już wykonać
        PlayerController.CutsceneLock = true;
        yield return null;

        var p = P;
        if (p == null) { Finish(); yield break; }
        PlayerController.LockCursor(true);
        if (Camcorder.Instance != null) Camcorder.Instance.ForceLower();
        cam = p.cameraTransform;
        arms = p.GetComponentInChildren<FirstPersonArms>(true);
        stalker = StalkerAI.All.Count > 0 ? StalkerAI.All[0] : null;
        if (stalker != null) stalker.HideForCutscene();

        p.SetControllerEnabled(false);
        st = PickStage(p);
        p.SetCutscenePosition(st.from);
        p.SetLook(Quaternion.LookRotation(-st.dir).eulerAngles.y, 0f);
        SetFirstPerson(p, false);

        amb = AmbientScape.Ensure();
        amb.scripted = true;
        amb.SetDuck(0f);

        yield return ColdOpen();
        yield return Corridor(Shots[1]);
        yield return Card(Shots[2], Loc.T("Do tego miejsca nie dociera sygnał.", "No signal reaches this place."), null, thump);
        yield return Hatch(Shots[3]);
        yield return Card(Shots[4], Loc.T("Coś poluje w rurach.", "Something hunts in the pipes."), null, stinger);
        yield return Hero(Shots[5]);
        yield return Silence(Shots[6]);
        yield return Title(Shots[7]);
        Finish();
    }

    void SetFirstPerson(PlayerController p, bool on)
    {
        if (arms != null) arms.gameObject.SetActive(on);
        if (p.bodyRoot != null) p.bodyRoot.gameObject.SetActive(on);
        if (p.Flashlight != null) p.Flashlight.SetOn(on);
        if (on) p.ResetCameraLocal();
    }

    static Vector3 Handheld(float t, float amount) =>
        new Vector3(Mathf.PerlinNoise(t * 0.9f, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t * 0.8f) - 0.5f, 0f) * amount;

    IEnumerator ColdOpen()
    {
        black = 1f;
        card = Loc.T("MŁODE BYCZKI PRZEDSTAWIAJĄ", "MŁODE BYCZKI PRESENT");
        cardSmall = null;
        amb.Duck(0.8f, Shots[0]);
        float T = Shots[0], t = 0f;
        bool knock = false;
        while (t < T)
        {
            t += Time.deltaTime;
            cardAlpha = Mathf.Clamp01((t - 0.3f) / 0.6f) * Mathf.Clamp01((T - 0.2f - t) / 0.5f);
            if (!knock && t > 1.1f)
            {
                knock = true;
                amb.Play(AmbientScape.Sound.Knocks, cam.position + Vector3.up * 2.5f + cam.right * 6f, 1f);
            }
            yield return null;
        }
        card = null;
        cardAlpha = 0f;
    }

    IEnumerator Corridor(float T)
    {
        rec = true;
        recStart = Time.time;
        amb.Duck(1f, 1f);
        var path = st.dolly;
        float len = Length(path);
        float travel = Mathf.Min(5.2f, Mathf.Max(0f, len - 0.4f));
        float d0 = Mathf.Max(0f, len - 0.4f - travel);
        Quaternion rot = Quaternion.LookRotation(Flat(Along(path, d0 + 2.5f) - Along(path, d0), -st.dir));
        float t = 0f;
        bool groan = false, whisper = false, buzz = false;
        while (t < T)
        {
            t += Time.deltaTime;
            float u = t / T;
            black = Mathf.Clamp01(1f - t / 0.5f);
            float dist = d0 + travel * u;
            Vector3 pos = Along(path, dist) + Vector3.up * 1.45f + Vector3.up * Mathf.Sin(t * 5.5f) * 0.012f;
            Vector3 look = Along(path, dist + 2.5f) + Vector3.up * 1.35f;
            Vector3 fwd = look - pos;
            if (fwd.sqrMagnitude < 0.01f) fwd = -st.dir;
            rot = Quaternion.Slerp(rot, Quaternion.LookRotation(fwd), Time.deltaTime * 2.5f);
            Vector3 hh = Handheld(t, 2.4f);
            cam.SetPositionAndRotation(pos, rot * Quaternion.Euler(hh.x, hh.y, hh.x * 0.5f));

            // migające światło (świetlówka „łapie” przerwy)
            float n = Mathf.PerlinNoise(t * 9f, 4.2f);
            flicker = n > 0.74f ? 0.55f : 0f;
            if (!buzz && t > 3.1f) { buzz = true; sfx.PlayOneShot(clicks, 0.5f); }
            if (t > 3.1f && t < 3.35f) flicker = 0.92f;

            if (!groan && t > 1.2f) { groan = true; amb.Play(AmbientScape.Sound.Groan, pos + fwd.normalized * 9f + Vector3.up * 1.5f, 1f); }
            if (!whisper && t > 3.6f) { whisper = true; amb.Play(AmbientScape.Sound.Whisper, pos - cam.right * 2.5f, 1f); }
            yield return null;
        }
        rec = false;
        flicker = 0f;
    }

    IEnumerator Card(float T, string big, string small, AudioClip hit)
    {
        black = 1f;
        card = big;
        cardSmall = small;
        if (hit != null) sfx.PlayOneShot(hit, 0.9f);
        float t = 0f;
        while (t < T)
        {
            t += Time.deltaTime;
            cardAlpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((T - t) / 0.3f);
            yield return null;
        }
        card = null;
        cardAlpha = 0f;
    }

    IEnumerator Hatch(float T)
    {
        var h = st.hatch;
        float t = 0f;
        bool r1 = false, r2 = false, eyes = false, drip = false, eyesOff = false;
        while (t < T)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / T);
            black = Mathf.Clamp01(1f - t / 0.4f);
            Vector3 pos = Vector3.Lerp(st.lowA, st.lowB, u);
            Vector3 hh = Handheld(t + 10f, 1.2f);
            cam.SetPositionAndRotation(pos, Quaternion.LookRotation(st.hatchPos - pos) * Quaternion.Euler(hh.x, hh.y, 0f));
            if (!r1 && t > 0.5f) { r1 = true; if (h != null) h.Disturb(0.8f); amb.Play(AmbientScape.Sound.Knocks, st.hatchPos + st.dir * 4f, 0.8f); }
            if (!r2 && t > 1.7f) { r2 = true; if (h != null) h.Disturb(1f); }
            if (!eyes && t > 2.4f) { eyes = true; if (h != null) h.SetLurking(true); }
            if (!drip && t > 3.0f) { drip = true; if (h != null) h.Drip(); else amb.Play(AmbientScape.Sound.Drip, st.hatchPos, 1f); }
            if (!eyesOff && t > T - 0.15f) { eyesOff = true; if (h != null) h.SetLurking(false); }
            yield return null;
        }
        if (h != null) h.SetLurking(false);
    }

    IEnumerator Hero(float T)
    {
        var p = P;
        var h = st.hatch;
        SetFirstPerson(p, true);
        float walkYaw = Quaternion.LookRotation(-st.dir).eulerAngles.y;
        Vector3 eyeAtStand = st.stand + Vector3.up * EyeH;
        Vector3 toHatch = st.hatchPos - eyeAtStand;
        float upPitch = -Mathf.Atan2(toHatch.y, new Vector2(toHatch.x, toHatch.z).magnitude) * Mathf.Rad2Deg;
        upPitch = Mathf.Max(upPitch, -70f);

        float[] yawT = { 0f, 3.2f, 3.9f, 4.2f, 5.0f, 5.3f, 5.9f };
        float[] yawV = { 0f, 0f, -60f, -60f, 55f, 55f, 0f };
        float[] pitchT = { 0f, 3.2f, 5.9f, 6.5f, 7.4f };
        float[] pitchV = { 4f, 6f, 2f, 0f, upPitch };
        const float walkEnd = 3.2f, dropAt = 7.6f;

        breath.volume = 0.3f;
        breath.Play();
        float t = 0f, nextStep = 0.25f, nextBeat = 5.6f, beatGap = 0.8f;
        int stepI = 0;
        bool ducked = false, drip = false, eyes = false;
        while (t < dropAt)
        {
            t += Time.deltaTime;
            black = Mathf.Clamp01(1f - t / 0.6f);
            float w = Mathf.Clamp01(t / walkEnd);
            float wu = Mathf.SmoothStep(0f, 1f, w);
            p.SetCutscenePosition(Vector3.Lerp(st.from, st.stand, wu));
            p.SetLook(walkYaw + Keys(t, yawT, yawV), Keys(t, pitchT, pitchV));
            bool walking = t < walkEnd - 0.2f;
            float bobK = walking ? 1f : Mathf.Clamp01((walkEnd + 0.2f - t) / 0.4f);
            float ph = t * 7.2f;
            cam.localPosition = new Vector3(Mathf.Sin(ph * 0.5f) * 0.02f, Mathf.Abs(Mathf.Sin(ph * 0.5f)) * 0.03f - 0.015f, 0f) * bobK;
            Vector3 hh = Handheld(t * 1.3f, 1.2f);
            cam.localRotation = Quaternion.Euler(hh.x, hh.y, Mathf.Sin(ph * 0.5f) * 0.8f * bobK);

            if (walking && t >= nextStep) { nextStep += 0.6f; sfx.PlayOneShot(steps[stepI++ % steps.Length], 0.55f); }
            if (!ducked && t > 5.6f) { ducked = true; amb.Duck(0.08f, 1.2f); breath.volume = 0.18f; }
            if (t >= nextBeat) { sfx.PlayOneShot(heartbeat, 0.8f); beatGap = Mathf.Max(0.45f, beatGap - 0.07f); nextBeat += beatGap; }
            if (!drip && t > 6.3f) { drip = true; if (h != null) h.Drip(); else amb.Play(AmbientScape.Sound.Drip, st.hatchPos, 1f); }
            if (!eyes && t > 6.9f) { eyes = true; if (h != null) h.SetLurking(true); }
            yield return null;
        }
        breath.Stop();
        yield return Jumpscare(p, T - dropAt);
    }

    void Hit(PlayerController p)
    {
        sfx.PlayOneShot(hitClip, 1f);
        if (stalker != null) stalker.PlayKillVoice(0);
        shake = 1f;
        flash = 0.42f;
    }

    static void Look(Transform pivot, Vector3 target, float speed)
    {
        Vector3 d = target - pivot.position;
        if (d.sqrMagnitude < 0.0001f) return;
        pivot.rotation = Quaternion.Slerp(pivot.rotation, Quaternion.LookRotation(d), Time.deltaTime * speed);
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) { v = fallback; v.y = 0f; }
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    void Shake()
    {
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.9f);
        float s = shake * shake;
        cam.localPosition = Random.insideUnitSphere * s * 0.07f;
        cam.localRotation = Quaternion.Euler(Random.Range(-1f, 1f) * s * 3f, Random.Range(-1f, 1f) * s * 3f, Random.Range(-1f, 1f) * s * 2f);
    }

    /// <summary>Jak „Ceiling” w DeathSequence: potwór zwisa z włazu głową w dół i rzuca się na bohatera.</summary>
    IEnumerator Jumpscare(PlayerController p, float total)
    {
        Transform pivot = p.cameraPivot;
        Vector3 eye = pivot.position;
        Vector3 hatch = st.hatchPos;
        Vector3 dir = Flat(hatch - p.transform.position, p.transform.forward);
        if (st.hatch != null) { st.hatch.SetLurking(false); st.hatch.Disturb(1f); }
        float t0 = Time.time;

        var s = stalker;
        if (s == null)
        {
            Hit(p);
            while (Time.time - t0 < total) { Shake(); yield return null; }
            yield break;
        }

        s.BeginKill();
        Transform root = s.transform;
        ProceduralRig rig = s.Rig;
        // ręce tylko lekko do przodu – w kadrze ma być pysk, nie łapy
        if (rig != null) { rig.lookTarget = pivot; rig.reach = 0.3f; rig.scream = 0f; rig.headRoll = 0f; rig.climb = 0f; }
        Quaternion upside = Quaternion.LookRotation(-dir, Vector3.down);
        root.rotation = upside;
        root.position = hatch + Vector3.up * 3f; // schowany w rurze, aż ułoży się poza
        s.PlayKillVoice(2);
        yield return null;
        Transform head = s.Head;
        Vector3 headLocal = root.InverseTransformPoint(head.position);
        if (headLocal.y < 1f) headLocal = new Vector3(0f, 2.1f, 0.15f);
        Vector3 headStart = new Vector3(hatch.x, hatch.y - 0.15f, hatch.z);
        Vector3 headEnd = eye + dir * 0.55f;
        Vector3 rootStart = headStart - upside * headLocal;
        Vector3 rootEnd = headEnd - upside * headLocal;
        root.position = rootStart;

        float t = 0f;
        while (t < 0.28f) { t += Time.deltaTime; Look(pivot, headStart, 18f); yield return null; }
        Hit(p);
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.2f);
            root.position = Vector3.Lerp(rootStart, rootEnd, k * k);
            if (rig != null) rig.scream = k;
            Look(pivot, head.position, 35f);
            Shake();
            yield return null;
        }
        while (Time.time - t0 < total)
        {
            if (rig != null) rig.scream = 1f;
            shake = Mathf.Max(shake, 0.85f);
            Look(pivot, head.position, 30f);
            Shake();
            yield return null;
        }
    }

    IEnumerator Silence(float T)
    {
        black = 1f;
        flash = 0f;
        sfx.Stop();
        amb.SetDuck(0f);
        if (stalker != null) stalker.HideForCutscene();
        yield return new WaitForSeconds(T);
    }

    IEnumerator Title(float T)
    {
        black = 1f;
        letterbox = false;
        titleT = 0f;
        sfx.PlayOneShot(boom, 1f);
        amb.Duck(0.6f, 2.5f);
        float t = 0f;
        while (t < T)
        {
            t += Time.deltaTime;
            titleT = t;
            yield return null;
        }
    }

    void Finish()
    {
        Playing = false;
        PlayerController.CutsceneLock = false;
        StoryDirector.checkpoint = false;
        StoryDirector.introSeen = false;
        if (amb != null) { amb.scripted = false; amb.Duck(1f, 2f); }
        MenuSystem.LoadScene(MapPicker.MenuScene);
    }

    // ---- glitch napisów: co chwilę krótki „skok” (rozciągnięcie na cały ekran, rozjechane kolory), pomiędzy lekkie mruganie ----
    float glitchUntil, nextGlitch = 0.5f;
    AudioClip glitchClip;

    bool TextVisible => (!string.IsNullOrEmpty(card) && cardAlpha > 0.3f) || titleT >= 0f;

    void Update()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.4f);
        if (!TextVisible) { nextGlitch = Mathf.Max(nextGlitch, Time.time + 0.25f); return; }
        if (Time.time < nextGlitch) return;
        glitchUntil = Time.time + Random.Range(0.06f, 0.2f);
        nextGlitch = Time.time + Random.Range(0.35f, 1.3f);
        if (glitchClip == null) glitchClip = ProceduralAudio.Noise(0.12f, 0.9f, "Glitch");
        sfx.PlayOneShot(glitchClip, 0.22f);
    }

    /// <summary>Napis z glitchem: w czasie „skoku” rozciąga się w poziomie (czasem na cały ekran) i rozjeżdża na kanały RGB.</summary>
    void GlitchText(Rect r, string s, GUIStyle style, Color c, float strength = 1f)
    {
        if (c.a <= 0.001f) return;
        // lekkie mruganie przez cały czas
        if (Random.value < 0.07f * strength) c.a *= Random.Range(0.25f, 0.7f);
        bool burst = Time.time < glitchUntil && strength > 0f;
        if (!burst)
        {
            Text(r, s, style, c);
            return;
        }
        Matrix4x4 saved = GUI.matrix;
        float textW = Mathf.Max(1f, style.CalcSize(new GUIContent(s)).x);
        float full = Screen.width / textW;                               // tyle trzeba, żeby wypełnić ekran
        float sx = Random.value < 0.45f ? Mathf.Max(1.6f, full * Random.Range(0.9f, 1.3f)) : Random.Range(1.1f, 2.2f);
        sx = Mathf.Lerp(1f, sx, strength);
        float sy = Mathf.Lerp(1f, Random.Range(0.55f, 1.15f), strength);
        GUIUtility.ScaleAroundPivot(new Vector2(sx, sy), r.center);
        float dx = Random.Range(4f, 14f) * styleK * strength;
        float jx = Random.Range(-20f, 20f) * styleK * strength;
        Rect j = new Rect(r.x + jx / sx, r.y + Random.Range(-6f, 6f) * styleK, r.width, r.height);
        GUI.color = new Color(1f, 0.1f, 0.1f, c.a * 0.7f);
        GUI.Label(new Rect(j.x + dx / sx, j.y, j.width, j.height), s, style);
        GUI.color = new Color(0.1f, 0.9f, 1f, c.a * 0.7f);
        GUI.Label(new Rect(j.x - dx / sx, j.y, j.width, j.height), s, style);
        Text(j, s, style, c);
        GUI.matrix = saved;
    }

    // =====================================================================
    //  OBRAZ (OnGUI – jak reszta przerywników)
    // =====================================================================

    void Styles()
    {
        float k = Screen.height / 1080f;
        if (cardStyle != null && Mathf.Abs(k - styleK) < 0.001f) return;
        styleK = k;
        cardStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(46 * k), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        cardStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(cardStyle) { fontSize = Mathf.RoundToInt(26 * k) };
        titleStyle = new GUIStyle(cardStyle) { fontSize = Mathf.RoundToInt(118 * k), fontStyle = FontStyle.Bold };
        subStyle = new GUIStyle(cardStyle) { fontSize = Mathf.RoundToInt(44 * k), fontStyle = FontStyle.Bold };
        recStyle = new GUIStyle(cardStyle) { fontSize = Mathf.RoundToInt(26 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
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

    static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    void OnGUI()
    {
        Styles();
        GUI.depth = -20;
        float sw = Screen.width, sh = Screen.height, k = styleK;

        if (flicker > 0.001f) Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, flicker));
        if (flash > 0.001f) Fill(new Rect(0, 0, sw, sh), new Color(0.55f, 0f, 0f, flash));

        if (rec && black < 0.99f)
        {
            float m = 70f * k + (letterbox ? sh * 0.1f : 0f);
            if (Mathf.Repeat(Time.time, 1f) < 0.6f) Fill(new Rect(60 * k, m, 22 * k, 22 * k), new Color(0.9f, 0.1f, 0.1f, 0.9f));
            Text(new Rect(92 * k, m - 6 * k, 300 * k, 40 * k), "REC", recStyle, Color.white);
            float e = Time.time - recStart;
            Text(new Rect(sw - 420 * k, m - 6 * k, 380 * k, 40 * k), $"20.11.2026  23:58:{(int)e % 60:00}", recStyle, Color.white);
        }

        if (letterbox)
        {
            float bar = sh * 0.1f;
            Fill(new Rect(0, 0, sw, bar), Color.black);
            Fill(new Rect(0, sh - bar, sw, bar), Color.black);
        }

        if (black > 0.001f) Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, black));

        if (!string.IsNullOrEmpty(card) && cardAlpha > 0.001f)
        {
            GlitchText(new Rect(sw * 0.1f, sh * 0.42f, sw * 0.8f, 90 * k), card, cardStyle, new Color(0.92f, 0.92f, 0.88f, cardAlpha));
            if (!string.IsNullOrEmpty(cardSmall))
                GlitchText(new Rect(sw * 0.1f, sh * 0.52f, sw * 0.8f, 50 * k), cardSmall, smallStyle, new Color(0.7f, 0.7f, 0.7f, cardAlpha), 0.5f);
        }

        if (titleT >= 0f)
        {
            float T = Shots[Shots.Length - 1];
            // tytuł „zapala się” jak świetlówka, potem gaśnie
            float on = titleT < 0.6f ? (Random.value < 0.55f ? 1f : 0.15f) : 1f;
            float a = on * Mathf.Clamp01((T - titleT) / 0.8f);
            float jitter = titleT < 1.2f ? Random.Range(-3f, 3f) * k : 0f;
            Rect tr = new Rect(jitter, sh * 0.36f, sw, 140 * k);
            if (Time.time >= glitchUntil)
            {
                GUI.color = new Color(0.8f, 0.05f, 0.05f, a * 0.55f);
                GUI.Label(new Rect(tr.x + 5 * k, tr.y, tr.width, tr.height), "THE BACKROOMS", titleStyle);
                GUI.color = Color.white;
            }
            GlitchText(tr, "THE BACKROOMS", titleStyle, new Color(0.95f, 0.93f, 0.85f, a));
            float subA = a * Mathf.Clamp01((titleT - 0.9f) / 0.6f);
            GlitchText(new Rect(0, sh * 0.5f, sw, 60 * k), Spaced("ISOLATION"), subStyle, new Color(0.85f, 0.8f, 0.55f, subA), 0.7f);
            float tagA = a * Mathf.Clamp01((titleT - 1.8f) / 0.6f);
            GlitchText(new Rect(0, sh * 0.62f, sw, 40 * k), Loc.T("BRAK ZASIĘGU. BRAK WYJŚCIA.", "NO SIGNAL. NO WAY OUT."), smallStyle, new Color(0.75f, 0.12f, 0.1f, tagA), 0.5f);
            Text(new Rect(0, sh * 0.86f, sw, 40 * k), "Młode Byczki", smallStyle, new Color(0.6f, 0.6f, 0.6f, tagA));
        }
    }
}
