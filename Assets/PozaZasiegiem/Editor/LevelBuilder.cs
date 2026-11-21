using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Menu: "Poza Zasiegiem > Zbuduj scene testowa".
/// Generuje "nieskończone" zaplecze fast foodu podzielone na STREFY z wyraźnymi wejściami:
/// MAGAZYN, KUCHNIA (drzwi wahadłowe), CHŁODNIA (plastikowe paski z fizyką), SALA RESTAURACJI,
/// BIURO KIEROWNIKA i żółte korytarze BACKROOMS.
/// Anomalia "pamięta" meble ze świata, ale odtwarza je źle: gigantyczne, na suficie, krzywe,
/// rozciągnięte, miniaturowe albo z pomylonym materiałem.
/// Pod sufitem biegnie RUROCIĄG z włazami – drogi Stalkera.
/// Meble stoją przy ścianach (nie w przejściach), a na końcu generator sprawdza NavMeshem,
/// czy da się dojść do każdego pokoju, przedmiotu i kryjówki (blokujące meble usuwa).
/// </summary>
public static class LevelBuilder
{
    const string Root = "Assets/PozaZasiegiem";
    const string GenDir = Root + "/Generated";
    const string ScenePath = Root + "/Scenes/TestLevel.unity";

    const int W = 9, H = 9;
    const float Cell = 6f, WallH = 3.2f, WallT = 0.25f, DoorW = 2.2f, DoorH = 2.45f;
    const float PipeY = 2.88f, PipeR = 0.27f;

    enum WS { Solid, Door, Open }
    enum Zone { Backrooms, Storage, Kitchen, Freezer, Dining, Office }

    class Style
    {
        public Material wall, floor, ceil, lamp, pedestal;
        public Color light;
        public float intensity, lightChance, flicker, anomaly, shadowChance;
        public int fixture;   // 0 panel, 1 świetlówka, 2 lampa wisząca, 3 lampa kulista
        public float lampShaftTop => fixture == 0 ? 0.45f : fixture == 1 ? 0.35f : fixture == 2 ? 0.25f : 0.2f;
    }

    class PropDef
    {
        public string name;
        public System.Action<Transform> build;
        public float depth;
        public bool tall;
        public PropDef(string n, System.Action<Transform> b, float d, bool isTall = false) { name = n; build = b; depth = d; tall = isTall; }
    }

    class WallRef { public bool horiz; public int x, y; }
    class Entrance { public bool horiz; public int x, y; public Zone a, b; }
    class ReachPoint { public Vector3 p; public Vector2Int cell; }

    static System.Random rng;
    static bool urp;
    static WS[,] hwG, vwG;
    static List<Entrance> entrances;
    static bool[,,] usedCorner, usedMid;
    static bool[,] usedCenter;
    static bool[,] corridor;
    static Transform fxT;
    static Dictionary<string, Material> shaftMats;
    static Material mMist, mFridgeGlass, mFridgeInner, mBottleGreen;
    static Renderer lastLampRenderer;
    static Zone[,] zone;
    static Dictionary<Zone, Style> styles;
    static Dictionary<string, Mesh> meshCache;
    static Dictionary<Vector2Int, List<GameObject>> cellProps;
    static Dictionary<Vector2Int, GameObject> centerProps;
    static List<ReachPoint> reach;
    static List<float> pipeRowsZ, pipeColsX;
    static Transform levelT, ceilT, detailT, itemsT, dynT;

    static Material mWallpaper, mKitchenTile, mFreezerPanel, mConcreteBlock, mDiningWall, mOfficeWall;
    static Material mCarpet, mQuarry, mDiamond, mConcrete, mChecker, mOfficeCarpet, mDropCeil, mDarkCeil;
    static Material mSteel, mSteelDark, mBlack, mDark, mBox, mBoxFrost, mIce, mMeat, mFrost, mRackOrange, mWood, mWoodDark;
    static Material mBarrelBlue, mBarrelRust, mSafetyYellow, mTableTop, mChairRed, mLeatherRed, mRedPaint, mYellowPaint, mOrange;
    static Material mSodaPanel, mMenuPanel, mBeige, mCRT, mGreyMetal, mPaper, mLocker, mLockerWhite, mLockerGreen, mPipe;
    static Material mGrease, mWetStain, mOilStain, mStrip, mVentMetal, mExit, mEmergency, mLampOff, mEmber, mRadioPanel;
    static Material mJacket, mPants, mShoe, mSkin, mFlashBody, mLens, mTrackerBody, mTrackerScreen;
    static Material mCreatureSkin, mUniform, mUniformDark, mApron, mClaw, mCreatureEye, mParticle;
    static Material mNotePaper, mStripClear, mPipeBig, mGlassDark, mCamBody, mCamScreen, mTrackerRT, mDrool;
    static Texture2D flashCookie;

    static float R() => (float)rng.NextDouble();
    static int RI(int min, int maxExcl) => rng.Next(min, maxExcl);
    static float RR(float a, float b) => a + (b - a) * R();

    /// <summary>Bez okna z raportem na końcu (budowanie sterowane z zewnątrz, np. z CLI). Raport trafia do LastReport i konsoli.</summary>
    public static bool Quiet;
    public static string LastReport = "";

    [MenuItem("Poza Zasiegiem/Zbuduj scene testowa (staly uklad)", false, 1)]
    public static void BuildFixed() => Build(12345);

    [MenuItem("Poza Zasiegiem/Zbuduj scene testowa (losowy uklad)", false, 2)]
    public static void BuildRandom() => Build(System.Environment.TickCount);

    static void Build(int seed)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report;
        GameObject player;
        try
        {
            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Tekstury i materiały...", 0.05f);
            rng = new System.Random(seed);
            urp = GraphicsSettings.currentRenderPipeline != null;
            meshCache = new Dictionary<string, Mesh>();
            cellProps = new Dictionary<Vector2Int, List<GameObject>>();
            centerProps = new Dictionary<Vector2Int, GameObject>();
            reach = new List<ReachPoint>();
            kitchenProps = null;
            story = new StoryRefs();
            EnsureFolder(GenDir);
            EnsureFolder(Root + "/Scenes");
            CreateMaterials();
            CreateStyles();
            string aeroRep = SetupAero();
            string gfx = ConfigureGraphics();

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Strefy, ściany i wejścia...", 0.2f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupRendering();
            levelT = new GameObject("Level (NavMesh)").transform;
            ceilT = new GameObject("Ceiling + Lights").transform;
            detailT = new GameObject("Details (bez NavMesh)").transform;
            dynT = new GameObject("Ruchome elementy (paski, drzwi)").transform;
            itemsT = new GameObject("Items").transform;
            fxT = new GameObject("Efekty wolumetryczne (mgła, snopy)").transform;
            shaftMats = new Dictionary<string, Material>();
            usedCorner = new bool[W, H, 4];
            usedMid = new bool[W, H, 4];
            usedCenter = new bool[W, H];

            GenerateLayout();
            PlanGate();
            PlanCorridors();
            BuildFloorAndCeiling();
            BuildWalls();
            BuildCorridors();
            BuildEntrances();
            BuildDoorDetails();
            BuildLights();
            BuildDecals();

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Rurociąg...", 0.35f);
            BuildPipeline();

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Brama, tunel pościgu, portal, przerywniki...", 0.4f);
            BuildGate();
            BuildTunnel();
            BuildSurfaceSets();
            // światła tunelu (włączane fabularnie) i przerywników nie trafiają do wypalania GI
            foreach (var root in new[] { story.tunnelLights, story.surface })
                if (root != null)
                    foreach (var lc in root.GetComponentsInChildren<LightCategory>(true)) lc.excludeFromBake = true;

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Przedmioty i kryjówki...", 0.45f);
            var start = new Vector2Int(0, 0);
            var reserved = new HashSet<Vector2Int> { start };
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (corridor[x, y]) reserved.Add(new Vector2Int(x, y)); // w wąskich korytarzach nie ma miejsca na przedmioty
            MakeNote(start, "Pognieciona kartka", StartNote, "Crumpled note", StartNoteEn);

            MakePickup(PickCell(reserved, 2, 3), PickupType.Tracker, "Wykrywacz ruchu", "Motion tracker", new Color(0.3f, 1f, 0.4f));
            MakePickup(PickZoneCell(Zone.Kitchen, reserved, 3), PickupType.Weapon, "Gaśnica", "Fire extinguisher", new Color(1f, 0.2f, 0.1f));
            MakePickup(PickZoneCell(Zone.Kitchen, reserved, 4), PickupType.Part, "Lampa nadawcza", "Transmitter tube", new Color(1f, 0.65f, 0.1f));
            MakePickup(PickZoneCell(Zone.Freezer, reserved, 5), PickupType.Part, "Zasilacz", "Power supply", new Color(1f, 0.65f, 0.1f));
            MakePickup(PickZoneCell(Zone.Dining, reserved, 5), PickupType.Part, "Antena", "Antenna", new Color(1f, 0.65f, 0.1f));
            MakeRadio(PickZoneCell(Zone.Office, reserved, 6));
            for (int i = 0; i < 6; i++)
                MakePickup(PickCell(reserved, 1, 99), PickupType.Battery, "Baterie", "Batteries", new Color(0.3f, 0.6f, 1f));

            MakeNote(PickZoneCell(Zone.Kitchen, reserved, 2), "Grafik zmian", KitchenNote, "Shift schedule", KitchenNoteEn);
            MakeNote(PickZoneCell(Zone.Freezer, reserved, 2), "Notatka kierownika zmiany", FreezerNote, "Shift manager's memo", FreezerNoteEn);
            MakeNote(PickZoneCell(Zone.Dining, reserved, 2), "Ankieta klienta", DiningNote, "Customer survey", DiningNoteEn);
            MakeNote(PickZoneCell(Zone.Office, reserved, 2), "Transkrypcja – 108.7 MHz", OfficeNote, "Transcript – 108.7 MHz", OfficeNoteEn);

            PlaceHidingSpots(start);

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Meble i anomalie...", 0.55f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    FillCell(new Vector2Int(x, y));
            if (!aero) // z AERO mgłę liczy renderer (prawdziwa wolumetryczna), cząsteczki byłyby podwójne
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        MakeMist(new Vector2Int(x, y));

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Sprawdzanie przejść (NavMesh)...", 0.7f);
            string validation = ValidateLayout();

            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Gracz, Stalker, post-processing...", 0.85f);
            player = MakePlayer(CellCenter(start), out GameObject camGo);
            MakeStalker(CellCenter(new Vector2Int(W - 1, H / 2)));

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            gm.AddComponent<DeathSequence>();
            gm.AddComponent<RuntimeNavMeshBaker>().levelRoot = levelT;
            gm.AddComponent<MenuSystem>();
            gm.AddComponent<Blackout>();
            gm.AddComponent<LightCuller>();
            if (aero)
            {
                var af = gm.AddComponent<AeroFog>();
                af.fogMaterial = aeroMat;
                af.densityProperties = aeroDensityProps;
                af.densityBase = aeroDensityBase;
            }
            var sd = gm.AddComponent<StoryDirector>();
            sd.surfaceRoot = story.surface;
            sd.introSet = story.introSet;
            sd.endingSet = story.endingSet;
            sd.introCamStart = story.introCamStart;
            sd.introCamEnd = story.introCamEnd;
            sd.introLook = story.introLook;
            sd.introHole = story.introHole;
            sd.introFloorPieces = story.floorPieces.ToArray();
            sd.friends = story.friends.ToArray();
            sd.gate = story.gate;
            sd.gateCheckpoint = story.checkpoint;
            sd.chaseStalkerSpawn = story.stalkerSpawn;
            sd.chaseZone = story.chaseZone;
            sd.endZone = story.endZone;
            sd.tunnelLightsRoot = story.tunnelLights;
            sd.sirenPoint = story.sirenPoint;
            sd.portalWalkPoint = story.portalWalk;
            sd.agentsLook = story.agentsLook;
            sd.agentFloodlight = story.flood;
            sd.endCamStart = story.endCamStart;
            sd.endCamEnd = story.endCamEnd;
            sd.endCamLook = story.endCamLook;
            sd.endPlayerStart = story.endPlayerStart;
            sd.endPlayerWalkTo = story.endPlayerWalkTo;
            sd.endPortal = story.endPortal;

            string pp = SetupPostProcessing(camGo);

            MarkStatic(levelT, true);
            MarkStatic(ceilT, true);
            MarkStatic(detailT, false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            // GI: po zapisie (zestaw wypalania potrzebuje identyfikatora sceny), po MarkStatic (dokłada flagę ContributeGI)
            string giRep = urp ? GiBaker.PrepareScene(scene) : "GI: pominięte (projekt bez URP).";
            EditorUtility.DisplayProgressBar("Poza Zasięgiem", "Occlusion Culling (rysuj tylko to, co widać)...", 0.95f);
            string occ = BakeOcclusion();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            report = gfx + "\n" + aeroRep + "\n" + pp + "\n" + validation + "\n" + entityReport + "\n" + occ + "\n" + giRep;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Selection.activeGameObject = player;
        Debug.Log("[Poza Zasięgiem] " + report);
        LastReport = report;
        if (!Quiet) EditorUtility.DisplayDialog("Poza Zasięgiem",
            "Scena zbudowana i zapisana:\n" + ScenePath + "\n\n" + report +
            "\n\nWciśnij Play – menu główne, potem intro (Spacja = pomiń).\nWASD, Shift, Ctrl/C, E, F, Tab/Q, LPM, PPM (kamera), N (noktowizja), Spacja (oddech w kryjówce), Esc = pauza/ustawienia, F1 = debug.", "OK");
    }

    // =====================================================================
    //  STREFY, REGIONY I WEJŚCIA
    // =====================================================================

    static void AssignZones()
    {
        zone = new Zone[W, H];
        var sz = new List<Zone>();
        var sp = new List<Vector2>();
        var sw = new List<float>();
        float sc = (W - 1) / 7f; // pozycje ziaren zaprojektowane dla mapy 8x8 – skalowane do aktualnego rozmiaru
        void Seed(Zone z, float x, float y, float w) { sz.Add(z); sp.Add(new Vector2(x * sc, y * sc)); sw.Add(w); }
        float J() => (R() - 0.5f) * 1.6f;

        Seed(Zone.Storage, 0f, 0f, 1.0f);
        Seed(Zone.Kitchen, 4.5f + J(), 1.2f + J() * 0.5f, 1.0f);
        Seed(Zone.Freezer, 7f, 0.5f + R(), 0.7f);
        Seed(Zone.Dining, 2f + J(), 5.5f + J(), 1.15f);
        Seed(Zone.Office, 7f, 7f, 0.5f);
        Seed(Zone.Backrooms, 4f + J(), 4f + J(), 0.9f);
        Seed(Zone.Backrooms, 0f, 3.2f + J() * 0.5f, 0.6f);
        Seed(Zone.Backrooms, 6.3f + J() * 0.5f, 4.5f + J(), 0.6f);
        Seed(Zone.Backrooms, 2.6f + J() * 0.4f, 2.2f + J() * 0.4f, 0.5f);
        Seed(Zone.Backrooms, 4.8f + J() * 0.4f, 6.8f, 0.45f);

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float best = float.MaxValue;
                for (int i = 0; i < sz.Count; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), sp[i]) / sw[i] + R() * 0.35f;
                    if (d < best) { best = d; zone[x, y] = sz[i]; }
                }
            }
        zone[0, 0] = Zone.Storage;
    }

    /// <summary>
    /// Każda spójna strefa (region) dostaje własny labirynt pomieszczeń, a sąsiednie regiony
    /// łączą się tylko przez WEJŚCIA (drzwi) – nigdy przez otwartą ścianę.
    /// </summary>
    static void GenerateLayout()
    {
        AssignZones();
        hwG = new WS[W, H + 1];
        vwG = new WS[W + 1, H];
        entrances = new List<Entrance>();

        // regiony = spójne grupy komórek tej samej strefy
        var region = new int[W, H];
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) region[x, y] = -1;
        int rc = 0;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (region[x, y] >= 0) continue;
                var q = new Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(x, y));
                region[x, y] = rc;
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var d in dirs)
                    {
                        var n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || region[n.x, n.y] >= 0) continue;
                        if (zone[n.x, n.y] != zone[c.x, c.y]) continue;
                        region[n.x, n.y] = rc;
                        q.Enqueue(n);
                    }
                }
                rc++;
            }

        // labirynt wewnątrz każdego regionu (DFS)
        var visited = new bool[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (visited[x, y]) continue;
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                visited[x, y] = true;
                while (stack.Count > 0)
                {
                    var c = stack.Peek();
                    var options = new List<Vector2Int>();
                    foreach (var d in dirs)
                    {
                        var n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || visited[n.x, n.y]) continue;
                        if (region[n.x, n.y] != region[c.x, c.y]) continue;
                        options.Add(n);
                    }
                    if (options.Count == 0) { stack.Pop(); continue; }
                    var next = options[RI(0, options.Count)];
                    SetWallBetween(c, next, WS.Door);
                    visited[next.x, next.y] = true;
                    stack.Push(next);
                }
            }

        // rozluźnienie ścian wewnątrz regionów (duże sale, pętle do ucieczki)
        for (int x = 0; x < W; x++)
            for (int y = 1; y < H; y++)
                if (region[x, y - 1] == region[x, y]) hwG[x, y] = LoosenIn(hwG[x, y], zone[x, y]);
        for (int x = 1; x < W; x++)
            for (int y = 0; y < H; y++)
                if (region[x - 1, y] == region[x, y]) vwG[x, y] = LoosenIn(vwG[x, y], zone[x, y]);

        // wspólne granice regionów
        var shared = new Dictionary<long, List<WallRef>>();
        void AddShared(int ra, int rb, WallRef w)
        {
            long key = (long)Mathf.Min(ra, rb) * 1000 + Mathf.Max(ra, rb);
            if (!shared.TryGetValue(key, out var list)) { list = new List<WallRef>(); shared[key] = list; }
            list.Add(w);
        }
        for (int x = 0; x < W; x++)
            for (int y = 1; y < H; y++)
                if (region[x, y - 1] != region[x, y]) AddShared(region[x, y - 1], region[x, y], new WallRef { horiz = true, x = x, y = y });
        for (int x = 1; x < W; x++)
            for (int y = 0; y < H; y++)
                if (region[x - 1, y] != region[x, y]) AddShared(region[x - 1, y], region[x, y], new WallRef { horiz = false, x = x, y = y });

        // drzewo połączeń regionów + kilka dodatkowych wejść (pętle)
        var keys = new List<long>(shared.Keys);
        for (int i = keys.Count - 1; i > 0; i--) { int j = RI(0, i + 1); long tmp = keys[i]; keys[i] = keys[j]; keys[j] = tmp; }
        var parent = new int[rc];
        for (int i = 0; i < rc; i++) parent[i] = i;
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }

        foreach (long key in keys)
        {
            int ra = (int)(key / 1000), rb = (int)(key % 1000);
            var walls = shared[key];
            if (Find(ra) != Find(rb))
            {
                parent[Find(ra)] = Find(rb);
                int first = RI(0, walls.Count);
                MakeEntrance(walls[first]);
                if (walls.Count >= 4 && R() < 0.35f)
                {
                    int second = (first + walls.Count / 2) % walls.Count;
                    MakeEntrance(walls[second]);
                }
            }
            else if (R() < 0.45f) MakeEntrance(walls[RI(0, walls.Count)]);
        }
    }

    static void SetWallBetween(Vector2Int a, Vector2Int b, WS s)
    {
        if (b.x == a.x + 1) vwG[a.x + 1, a.y] = s;
        else if (b.x == a.x - 1) vwG[a.x, a.y] = s;
        else if (b.y == a.y + 1) hwG[a.x, a.y + 1] = s;
        else if (b.y == a.y - 1) hwG[a.x, a.y] = s;
    }

    static void MakeEntrance(WallRef w)
    {
        var e = new Entrance { horiz = w.horiz, x = w.x, y = w.y };
        if (w.horiz)
        {
            if (hwG[w.x, w.y] == WS.Door) return;
            hwG[w.x, w.y] = WS.Door;
            e.a = zone[w.x, w.y - 1];
            e.b = zone[w.x, w.y];
        }
        else
        {
            if (vwG[w.x, w.y] == WS.Door) return;
            vwG[w.x, w.y] = WS.Door;
            e.a = zone[w.x - 1, w.y];
            e.b = zone[w.x, w.y];
        }
        entrances.Add(e);
    }

    static WS LoosenIn(WS s, Zone z)
    {
        float open = 0.25f, door = 0.25f;
        switch (z)
        {
            case Zone.Freezer: open = 0.6f; door = 0.15f; break;
            case Zone.Storage: open = 0.45f; door = 0.2f; break;
            case Zone.Dining: open = 0.65f; door = 0.15f; break;
            case Zone.Kitchen: open = 0.35f; door = 0.2f; break;
            case Zone.Office: open = 0.1f; door = 0.25f; break;
            case Zone.Backrooms: open = 0.2f; door = 0.3f; break;
        }
        float r = R();
        if (s == WS.Door) return r < open ? WS.Open : WS.Door;
        if (s == WS.Solid) return r < open * 0.4f ? WS.Open : (r < open * 0.4f + door ? WS.Door : WS.Solid);
        return s;
    }

    static Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell);
    static Style St(Vector2Int c) => styles[zone[c.x, c.y]];

    // =====================================================================
    //  GEOMETRIA
    // =====================================================================

    static void BuildFloorAndCeiling()
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var st = styles[zone[x, y]];
                Vector3 c = CellCenter(new Vector2Int(x, y));
                Box("Floor", levelT, c + Vector3.down * 0.1f, new Vector3(Cell, 0.2f, Cell), st.floor, true, false, null, 0f, ShadowCastingMode.Off);
                Box("Ceiling", ceilT, c + Vector3.up * (WallH + 0.1f), new Vector3(Cell, 0.2f, Cell), st.ceil, true, false, null, 0f, ShadowCastingMode.Off);
            }
    }

    static void BuildWalls()
    {
        var walls = new GameObject("Walls").transform;
        walls.SetParent(levelT);
        for (int y = 0; y <= H; y++)
            for (int x = 0; x < W; x++)
            {
                Material neg = y - 1 >= 0 ? styles[zone[x, y - 1]].wall : null;
                Material pos = y < H ? styles[zone[x, y]].wall : null;
                BuildWall(new Vector3(x * Cell, 0f, y * Cell), true, hwG[x, y], neg, pos, walls);
            }
        for (int x = 0; x <= W; x++)
            for (int y = 0; y < H; y++)
            {
                Material neg = x - 1 >= 0 ? styles[zone[x - 1, y]].wall : null;
                Material pos = x < W ? styles[zone[x, y]].wall : null;
                BuildWall(new Vector3(x * Cell, 0f, y * Cell), false, vwG[x, y], neg, pos, walls);
            }
        for (int x = 0; x <= W; x++)
            for (int y = 0; y <= H; y++)
            {
                var m = styles[zone[Mathf.Clamp(x, 0, W - 1), Mathf.Clamp(y, 0, H - 1)]].wall;
                Box("Pillar", walls, new Vector3(x * Cell, WallH / 2f, y * Cell), new Vector3(0.4f, WallH, 0.4f), m, true, false, null, 0f);
            }
    }

    /// <summary>Ściana z dwóch połówek – każda strona ma materiał swojej strefy.</summary>
    static void BuildWall(Vector3 start, bool alongX, WS state, Material neg, Material pos, Transform parent)
    {
        Vector3 dir = alongX ? Vector3.right : Vector3.forward;
        Vector3 n = alongX ? Vector3.forward : Vector3.right;

        void Piece(float from, float len, float y0, float h)
        {
            Vector3 center = start + dir * (from + len / 2f) + Vector3.up * (y0 + h / 2f);
            Vector3 size = alongX ? new Vector3(len, h, WallT / 2f) : new Vector3(WallT / 2f, h, len);
            if (neg != null) Box("Wall", parent, center - n * (WallT / 4f), size, neg, true, false, null, y0);
            if (pos != null) Box("Wall", parent, center + n * (WallT / 4f), size, pos, true, false, null, y0);
        }

        switch (state)
        {
            case WS.Solid:
                Piece(0f, Cell, 0f, WallH);
                break;
            case WS.Door:
                float side = (Cell - DoorW) / 2f;
                Piece(0f, side, 0f, WallH);
                Piece(Cell - side, side, 0f, WallH);
                Piece(side, DoorW, DoorH, WallH - DoorH);
                break;
            case WS.Open:
                Piece(0f, Cell, WallH - 0.4f, 0.4f);
                break;
        }
    }

    static void WallFrame(WallRef w, out Vector3 mid, out Vector3 dir, out Vector3 n)
    {
        Vector3 start = new Vector3(w.x * Cell, 0f, w.y * Cell);
        dir = w.horiz ? Vector3.right : Vector3.forward;
        n = w.horiz ? Vector3.forward : Vector3.right;
        mid = start + dir * (Cell / 2f);
    }

    static bool Has(Entrance e, Zone z) => e.a == z || e.b == z;

    /// <summary>Wejścia między strefami: ościeżnice, paski w chłodni, drzwi wahadłowe w kuchni.</summary>
    static void BuildEntrances()
    {
        foreach (var e in entrances)
        {
            WallFrame(new WallRef { horiz = e.horiz, x = e.x, y = e.y }, out Vector3 mid, out Vector3 dir, out Vector3 n);
            Material frame = Has(e, Zone.Freezer) ? mSteel : Has(e, Zone.Kitchen) ? mSteel
                : (Has(e, Zone.Office) || Has(e, Zone.Dining)) ? mWoodDark : Has(e, Zone.Storage) ? mSafetyYellow : mSteelDark;
            float depth = WallT + 0.1f;
            Vector3 jamb = e.horiz ? new Vector3(0.12f, DoorH, depth) : new Vector3(depth, DoorH, 0.12f);
            Box("Ościeżnica", levelT, mid + dir * (DoorW / 2f - 0.06f) + Vector3.up * (DoorH / 2f), jamb, frame, true, false, null, 0f);
            Box("Ościeżnica", levelT, mid - dir * (DoorW / 2f - 0.06f) + Vector3.up * (DoorH / 2f), jamb, frame, true, false, null, 0f);
            Box("Nadproże", levelT, mid + Vector3.up * (DoorH + 0.06f), e.horiz ? new Vector3(DoorW, 0.12f, depth) : new Vector3(depth, 0.12f, DoorW), frame, true, false, null, 0f);

            if (Has(e, Zone.Freezer)) MakeStripCurtain(mid, n);
            else if (Has(e, Zone.Kitchen) && R() < 0.75f) MakeSwingDoors(mid, n);

            if (R() < 0.22f) ExitSign(mid, n, R() < 0.5f ? 1f : -1f, e.horiz);
        }
    }

    static void ExitSign(Vector3 mid, Vector3 n, float side, bool alongX)
    {
        Vector3 p = mid + Vector3.up * (DoorH + 0.26f) + n * side * (WallT / 2f + 0.04f);
        Vector3 size = alongX ? new Vector3(0.5f, 0.18f, 0.04f) : new Vector3(0.04f, 0.18f, 0.5f);
        Box("ExitSign", detailT, p, size, mExit, false, false, null, 0f, ShadowCastingMode.Off);
        AddSpot(detailT, p + n * side * 0.12f, Quaternion.LookRotation(Vector3.down + n * side * 0.7f), new Color(0.2f, 1f, 0.35f), 3.6f, 120f, urp ? 1.6f : 0.6f, 2, false);
    }

    static void MakeStripCurtain(Vector3 mid, Vector3 n)
    {
        var root = new GameObject("Paski chłodni (fizyka)");
        root.transform.SetParent(dynT);
        root.transform.position = mid + Vector3.up * (DoorH - 0.02f);
        root.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
        B(root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(DoorW, 0.06f, 0.06f), mSteel);
        int count = 12;
        float len = DoorH - 0.08f;
        var pivots = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            float x = -DoorW / 2f + 0.09f + i * (DoorW - 0.18f) / (count - 1);
            var pv = Pivot("Pasek", root.transform, new Vector3(x, 0f, 0f));
            B(pv, new Vector3(0f, -len / 2f, 0f), new Vector3(0.16f, len, 0.006f), mStripClear, 0f, false, ShadowCastingMode.Off);
            pivots[i] = pv;
        }
        var sc = root.AddComponent<StripCurtain>();
        sc.strips = pivots;
        sc.length = len;
        sc.stripHalfWidth = 0.08f;
    }

    static void MakeSwingDoors(Vector3 mid, Vector3 n)
    {
        var root = new GameObject("Drzwi wahadłowe (kuchnia)");
        root.transform.SetParent(dynT);
        root.transform.position = mid;
        root.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
        float half = DoorW / 2f - 0.13f;
        float pw = half - 0.02f;
        var l = Pivot("Skrzydło L", root.transform, new Vector3(-half, 0f, 0f));
        B(l, new Vector3(pw / 2f, 1.0f, 0f), new Vector3(pw, 1.9f, 0.04f), mSteel);
        B(l, new Vector3(pw * 0.55f, 1.45f, 0f), new Vector3(0.28f, 0.28f, 0.05f), mGlassDark);
        B(l, new Vector3(pw / 2f, 0.18f, 0f), new Vector3(pw - 0.06f, 0.25f, 0.05f), mSteelDark);
        var r = Pivot("Skrzydło P", root.transform, new Vector3(half, 0f, 0f));
        B(r, new Vector3(-pw / 2f, 1.0f, 0f), new Vector3(pw, 1.9f, 0.04f), mSteel);
        B(r, new Vector3(-pw * 0.55f, 1.45f, 0f), new Vector3(0.28f, 0.28f, 0.05f), mGlassDark);
        B(r, new Vector3(-pw / 2f, 0.18f, 0f), new Vector3(pw - 0.06f, 0.25f, 0.05f), mSteelDark);
        var sd = root.AddComponent<SwingDoor>();
        sd.leftHinge = l;
        sd.rightHinge = r;
        sd.halfWidth = half;
    }

    /// <summary>Zwykłe drzwi wewnątrz stref – czasem zielony znak WYJŚCIE (który nigdzie nie prowadzi).</summary>
    static void BuildDoorDetails()
    {
        var isEntrance = new HashSet<long>();
        foreach (var e in entrances) isEntrance.Add((e.horiz ? 1L : 0L) * 100000 + e.x * 100 + e.y);
        for (int y = 1; y < H; y++)
            for (int x = 0; x < W; x++)
                if (hwG[x, y] == WS.Door && !isEntrance.Contains(100000L + x * 100 + y) && R() < 0.08f)
                {
                    WallFrame(new WallRef { horiz = true, x = x, y = y }, out Vector3 mid, out _, out Vector3 n);
                    ExitSign(mid, n, R() < 0.5f ? 1f : -1f, true);
                }
        for (int x = 1; x < W; x++)
            for (int y = 0; y < H; y++)
                if (vwG[x, y] == WS.Door && !isEntrance.Contains(x * 100 + y) && R() < 0.08f)
                {
                    WallFrame(new WallRef { horiz = false, x = x, y = y }, out Vector3 mid, out _, out Vector3 n);
                    ExitSign(mid, n, R() < 0.5f ? 1f : -1f, false);
                }
    }

    static void BuildLights()
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var cell = new Vector2Int(x, y);
                var st = St(cell);
                bool start = x == 0 && y == 0;
                bool on = start || R() < st.lightChance;
                Vector3 c = CellCenter(cell);
                float lightY = LampFixture(c, st, on);
                if (on)
                {
                    var g = new GameObject("Lamp Light");
                    g.transform.SetParent(ceilT);
                    g.transform.position = new Vector3(c.x, lightY, c.z);
                    g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    var l = g.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 140f;
                    l.innerSpotAngle = 55f;
                    l.range = lightY + 1.6f;
                    l.intensity = (urp ? 12f : 1.6f) * st.intensity;
                    l.color = st.light;
                    l.shadows = LightShadows.Soft;          // v4: każda lampa rzuca cień
                    l.shadowStrength = 0.95f;
                    l.shadowNearPlane = 0.1f;
                    SetupShadowedLight(l, 1, 1);
                    var shaft = MakeShaft(g.transform, st.lampShaftTop, corridor[x, y] ? 1.05f : 1.7f, lightY - 0.05f, st.light, 0.14f, "Lamp_" + zone[x, y]);
                    if (!start && R() < st.flicker)
                    {
                        var fl = g.AddComponent<FlickerLight>();
                        fl.shaft = shaft;
                        fl.fixture = lastLampRenderer;
                    }
                    MakeDustMotes(cell, lightY);
                }
                else if (R() < 0.4f)
                {
                    // ciemne pomieszczenie – tylko czerwone światło awaryjne
                    Vector3 p = (corridor[x, y] ? c + new Vector3(0.6f, 0f, 0f) : CornerPos(cell, RI(0, 4), 0.45f)) + Vector3.up * (WallH - 0.1f);
                    Box("EmergencyLight", ceilT, p, new Vector3(0.3f, 0.12f, 0.12f), mEmergency, false, false, null, 0f, ShadowCastingMode.Off);
                    var lg = AddSpot(ceilT, p + Vector3.down * 0.15f, Quaternion.Euler(90f, 0f, 0f), new Color(1f, 0.08f, 0.03f), 5.5f, 130f, urp ? 3.2f : 1f, 2, false);
                    MakeShaft(lg.transform, 0.08f, 1.4f, WallH - 0.3f, new Color(1f, 0.1f, 0.05f), 0.1f, "Emergency");
                    if (R() < 0.5f) lg.AddComponent<FlickerLight>();
                }
            }
    }

    /// <summary>Oprawa lampy. Zwraca wysokość, na której ma być światło.</summary>
    static float LampFixture(Vector3 c, Style st, bool on)
    {
        Material em = on ? st.lamp : mLampOff;
        var off = ShadowCastingMode.Off;
        lastLampRenderer = null;
        switch (st.fixture)
        {
            case 1:
                Box("LampHousing", ceilT, c + Vector3.up * (WallH - 0.04f), new Vector3(1.5f, 0.08f, 0.28f), mSteelDark, false, false, null, 0f, off);
                lastLampRenderer = Box("LampTube", ceilT, c + Vector3.up * (WallH - 0.1f), new Vector3(1.35f, 0.05f, 0.07f), em, false, false, null, 0f, off).GetComponent<Renderer>();
                return WallH - 0.16f;
            case 2:
                Box("LampCable", ceilT, c + Vector3.up * (WallH - 0.35f), new Vector3(0.015f, 0.7f, 0.015f), mBlack, false, false, null, 0f, off);
                Prim(PrimitiveType.Cylinder, "LampShade", ceilT, c + Vector3.up * (WallH - 0.72f), new Vector3(0.55f, 0.05f, 0.55f), mSteelDark, default, off);
                lastLampRenderer = Prim(PrimitiveType.Sphere, "LampBulb", ceilT, c + Vector3.up * (WallH - 0.8f), Vector3.one * 0.16f, em, default, off).GetComponent<Renderer>();
                return WallH - 0.9f;
            case 3:
                Box("LampCable", ceilT, c + Vector3.up * (WallH - 0.4f), new Vector3(0.015f, 0.8f, 0.015f), mBlack, false, false, null, 0f, off);
                lastLampRenderer = Prim(PrimitiveType.Sphere, "LampGlobe", ceilT, c + Vector3.up * (WallH - 0.88f), new Vector3(0.45f, 0.28f, 0.45f), em, default, off).GetComponent<Renderer>();
                return WallH - 1.0f;
            default:
                lastLampRenderer = Box("LampPanel", ceilT, c + Vector3.up * (WallH - 0.03f), new Vector3(1.2f, 0.06f, 0.6f), em, false, false, null, 0f, off).GetComponent<Renderer>();
                return WallH - 0.1f;
        }
    }

    static void BuildDecals()
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var cell = new Vector2Int(x, y);
                var z = zone[x, y];
                Vector3 c = CellCenter(cell);

                if (z == Zone.Storage && R() < 0.35f)
                {
                    bool ax = R() < 0.5f;
                    float o = (R() < 0.5f ? -1f : 1f) * 1.9f;
                    Vector3 p = c + new Vector3(ax ? 0f : o, 0.003f, ax ? o : 0f);
                    Box("SafetyLine", detailT, p, ax ? new Vector3(Cell, 0.004f, 0.1f) : new Vector3(0.1f, 0.004f, Cell), mSafetyYellow, false, false, null, 0f, ShadowCastingMode.Off);
                }

                if (R() < 0.4f)
                {
                    Material m = z == Zone.Freezer ? mFrost : z == Zone.Kitchen ? mGrease : z == Zone.Storage ? mOilStain : z == Zone.Backrooms ? mWetStain : null;
                    if (m != null)
                    {
                        float d = RR(0.6f, 1.6f);
                        Vector3 p = c + new Vector3(RR(-1.8f, 1.8f), 0.002f, RR(-1.8f, 1.8f));
                        Prim(PrimitiveType.Cylinder, "Stain", detailT, p, new Vector3(d, 0.001f, d * RR(0.5f, 1f)), m, new Vector3(0f, RR(0f, 360f), 0f), ShadowCastingMode.Off);
                    }
                }
            }
    }

    // =====================================================================
    //  RUROCIĄG POD SUFITEM (drogi Stalkera)
    // =====================================================================

    static List<float> PickLines(int count, int cells)
    {
        var candidates = new List<int>();
        for (int i = 1; i < cells - 1; i++) candidates.Add(i);
        for (int i = candidates.Count - 1; i > 0; i--) { int j = RI(0, i + 1); int tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp; }
        var picked = new List<int>();
        foreach (int c in candidates)
        {
            bool ok = true;
            foreach (int p in picked) if (Mathf.Abs(p - c) < 2) ok = false;
            if (ok) picked.Add(c);
            if (picked.Count >= count) break;
        }
        picked.Sort();
        var coords = new List<float>();
        foreach (int p in picked) coords.Add((p + 0.5f) * Cell + (R() < 0.5f ? -1.6f : 1.6f));
        return coords;
    }

    static bool NearPipe(Vector3 p, float radius)
    {
        if (pipeRowsZ != null) foreach (float z in pipeRowsZ) if (Mathf.Abs(p.z - z) < radius + PipeR) return true;
        if (pipeColsX != null) foreach (float x in pipeColsX) if (Mathf.Abs(p.x - x) < radius + PipeR) return true;
        return false;
    }

    static void BuildPipeline()
    {
        pipeRowsZ = PickLines(3, H);
        pipeColsX = PickLines(3, W);
        float minC = 0.35f, maxX = W * Cell - 0.35f, maxZ = H * Cell - 0.35f;

        var root = new GameObject("Rurociąg (rury pod sufitem)").transform;
        var net = root.gameObject.AddComponent<PipeNetwork>();
        var nodes = new List<Vector3>();
        var ea = new List<int>();
        var eb = new List<int>();
        var segs = new List<PipeSegment>();
        var flanges = new List<Vector3>();

        int NodeAt(Vector3 p)
        {
            for (int i = 0; i < nodes.Count; i++) if ((nodes[i] - p).sqrMagnitude < 0.09f) return i;
            nodes.Add(p);
            return nodes.Count - 1;
        }

        // kandydaci na włazy: rzuty środków komórek na linie rur (z dala od skrzyżowań)
        var cand = new List<Vector3>();
        foreach (float z0 in pipeRowsZ)
            for (int i = 0; i < W; i++)
            {
                float x = (i + 0.5f) * Cell;
                bool nearCross = false;
                foreach (float x0 in pipeColsX) if (Mathf.Abs(x - x0) < 1.2f) nearCross = true;
                if (!nearCross) cand.Add(new Vector3(x, PipeY, z0));
            }
        foreach (float x0 in pipeColsX)
            for (int j = 0; j < H; j++)
            {
                float z = (j + 0.5f) * Cell;
                bool nearCross = false;
                foreach (float z0 in pipeRowsZ) if (Mathf.Abs(z - z0) < 1.2f) nearCross = true;
                if (!nearCross) cand.Add(new Vector3(x0, PipeY, z));
            }
        for (int i = cand.Count - 1; i > 0; i--) { int j = RI(0, i + 1); var tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp; }
        var hatches = new List<Vector3>();
        foreach (var p in cand)
        {
            if (p.x < Cell && p.z < Cell) continue; // nie w komórce startowej
            bool ok = true;
            foreach (var h in hatches) if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(h.x, h.z)) < 8.5f) ok = false;
            if (ok) hatches.Add(p);
            if (hatches.Count >= 15) break;
        }

        // węzły i krawędzie wzdłuż każdej linii
        void Line(bool alongX, float fixedCoord)
        {
            var pts = new List<float> { minC, alongX ? maxX : maxZ };
            foreach (float c in alongX ? pipeColsX : pipeRowsZ) pts.Add(c);
            foreach (var h in hatches)
            {
                if (alongX && Mathf.Abs(h.z - fixedCoord) < 0.01f) pts.Add(h.x);
                if (!alongX && Mathf.Abs(h.x - fixedCoord) < 0.01f) pts.Add(h.z);
            }
            pts.Sort();
            int prev = -1;
            float prevV = float.MinValue;
            foreach (float v in pts)
            {
                if (v - prevV < 0.3f) continue;
                Vector3 p = alongX ? new Vector3(v, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, v);
                int id = NodeAt(p);
                if (prev >= 0 && prev != id) { ea.Add(prev); eb.Add(id); }
                prev = id;
                prevV = v;
            }

            // wygląd: odcinek rury na każdą komórkę + kołnierze na ścianach + obejmy
            int cells = alongX ? W : H;
            float end = alongX ? maxX : maxZ;
            for (int i = 0; i < cells; i++)
            {
                float a = Mathf.Max(minC, i * Cell), b = Mathf.Min(end, (i + 1) * Cell);
                Vector3 pa = alongX ? new Vector3(a, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, a);
                Vector3 pb = alongX ? new Vector3(b, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, b);
                var segGo = new GameObject("Rura");
                segGo.transform.SetParent(root, false);
                segGo.transform.position = (pa + pb) * 0.5f;
                Cyl(segGo.transform, Vector3.zero, PipeR, b - a, mPipeBig, alongX ? 'x' : 'z');
                if (i > 0)
                {
                    Vector3 fl = alongX ? new Vector3(i * Cell, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, i * Cell);
                    Cyl(segGo.transform, segGo.transform.InverseTransformPoint(fl), PipeR + 0.06f, 0.14f, mVentMetal, alongX ? 'x' : 'z');
                    flanges.Add(fl);
                }
                for (int s = 0; s < 2; s++)
                {
                    float sv = a + (b - a) * (0.3f + 0.4f * s);
                    Vector3 sp = alongX ? new Vector3(sv, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, sv);
                    Cyl(segGo.transform, segGo.transform.InverseTransformPoint(sp), PipeR + 0.025f, 0.06f, mSteelDark, alongX ? 'x' : 'z');
                    B(segGo.transform, segGo.transform.InverseTransformPoint(sp + Vector3.up * (PipeR + 0.03f)), new Vector3(0.05f, 0.08f, 0.05f), mSteelDark);
                }
                var seg = segGo.AddComponent<PipeSegment>();
                seg.a = pa;
                seg.b = pb;
                segs.Add(seg);
            }
            // zaślepki
            Vector3 c0 = alongX ? new Vector3(minC, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, minC);
            Vector3 c1 = alongX ? new Vector3(end, PipeY, fixedCoord) : new Vector3(fixedCoord, PipeY, end);
            Cyl(detailT, c0, PipeR + 0.04f, 0.08f, mVentMetal, alongX ? 'x' : 'z');
            Cyl(detailT, c1, PipeR + 0.04f, 0.08f, mVentMetal, alongX ? 'x' : 'z');
        }

        foreach (float z0 in pipeRowsZ) Line(true, z0);
        foreach (float x0 in pipeColsX) Line(false, x0);

        // skrzynki na skrzyżowaniach
        foreach (float z0 in pipeRowsZ)
            foreach (float x0 in pipeColsX)
                Box("Złączka rur", detailT, new Vector3(x0, PipeY, z0), new Vector3(0.8f, 0.7f, 0.8f), mSteelDark, false, false, null, 0f);

        // włazy
        foreach (var h in hatches)
        {
            bool alongX = false;
            foreach (float z0 in pipeRowsZ) if (Mathf.Abs(h.z - z0) < 0.01f) alongX = true;
            MakeHatch(root, h, alongX, NodeAt(h));
        }

        // pula cząsteczek kurzu (przesuwane w miejsce, gdzie coś przechodzi)
        var pool = new List<ParticleSystem>();
        for (int i = 0; i < 3; i++)
            pool.Add(MakeParticles(root, Vector3.zero, new Vector3(90f, 0f, 0f), new Color(0.55f, 0.5f, 0.42f, 0.7f), 0.16f, 0.5f, 2f, 20f, 0.6f, 10, 30f, 0.15f));

        net.nodes = nodes.ToArray();
        net.edgeA = ea.ToArray();
        net.edgeB = eb.ToArray();
        net.segments = segs.ToArray();
        net.flangePoints = flanges.ToArray();
        net.dustPool = pool.ToArray();
    }

    static void MakeHatch(Transform parent, Vector3 p, bool alongX, int node)
    {
        var root = new GameObject("Właz w rurze");
        root.transform.SetParent(parent, false);
        root.transform.position = p;
        var t = root.transform;
        char ax = alongX ? 'x' : 'z';
        Cyl(t, Vector3.zero, PipeR + 0.07f, 0.7f, mVentMetal, ax);
        Cyl(t, new Vector3(0f, -PipeR - 0.075f, 0f), 0.2f, 0.01f, mBlack, 'y', ShadowCastingMode.Off);
        var flap = Pivot("Klapa", t, new Vector3(0.2f, -PipeR - 0.07f, 0f));
        flap.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, -70f);
        B(flap, new Vector3(0.17f, 0f, 0f), new Vector3(0.34f, 0.015f, 0.3f), mVentMetal);

        var eyes = new GameObject("Oczy w ciemności");
        eyes.transform.SetParent(t, false);
        eyes.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        Prim(PrimitiveType.Sphere, "Oko", eyes.transform, new Vector3(-0.05f, 0f, 0.02f), new Vector3(0.03f, 0.02f, 0.03f), mCreatureEye, default, ShadowCastingMode.Off);
        Prim(PrimitiveType.Sphere, "Oko", eyes.transform, new Vector3(0.05f, 0f, 0.02f), new Vector3(0.03f, 0.02f, 0.03f), mCreatureEye, default, ShadowCastingMode.Off);
        AddPointLight(eyes.transform, new Vector3(0f, -0.15f, 0f), new Color(1f, 0.1f, 0.05f), 1.6f, urp ? 0.8f : 0.4f, true);
        eyes.SetActive(false);

        var dust = MakeParticles(t, new Vector3(0f, -PipeR - 0.1f, 0f), new Vector3(90f, 0f, 0f), new Color(0.6f, 0.56f, 0.48f, 0.75f),
            0.18f, 0.6f, 2.2f, 25f, 0.8f, 15, 25f, 0.15f);
        var drool = MakeParticles(t, new Vector3(0f, -PipeR - 0.09f, 0f), new Vector3(90f, 0f, 0f), new Color(0.75f, 0.8f, 0.6f, 0.9f),
            0.04f, 0.15f, 1.3f, 0f, 0.3f, 2, 3f, 1.0f);
        drool.GetComponent<ParticleSystemRenderer>().sharedMaterial = mDrool;

        var hatch = root.AddComponent<PipeHatch>();
        hatch.node = node;
        hatch.dust = dust;
        hatch.drool = drool;
        hatch.lurkEyes = eyes;
        reach.Add(new ReachPoint { p = new Vector3(p.x, 0f, p.z), cell = CellOf(p) });
    }

    static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x / Cell), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(p.z / Cell), 0, H - 1));

    // =====================================================================
    //  SLOTY (meble przy ścianach, nie w przejściach) I ANOMALIE
    // =====================================================================

    /// <summary>Stan ściany komórki. side: 0 = -X, 1 = +X, 2 = -Z, 3 = +Z.</summary>
    static WS SideState(Vector2Int c, int side)
    {
        switch (side)
        {
            case 0: return vwG[c.x, c.y];
            case 1: return vwG[c.x + 1, c.y];
            case 2: return hwG[c.x, c.y];
            default: return hwG[c.x, c.y + 1];
        }
    }

    static Vector3 SideNormal(int side) =>
        side == 0 ? Vector3.left : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.forward;

    static Vector3 CornerSign(int k) => new Vector3((k & 1) == 0 ? -1f : 1f, 0f, (k & 2) == 0 ? -1f : 1f);

    static Vector3 CornerPos(Vector2Int c, int k, float inset)
    {
        Vector3 s = CornerSign(k);
        return CellCenter(c) + new Vector3(s.x, 0f, s.z) * (Cell / 2f - inset);
    }

    /// <summary>Miejsce przy ścianie w rogu komórki; obiekt plecami do ściany, przodem (+Z) do pokoju.</summary>
    static void WallSlot(Vector2Int c, int k, float depth, bool againstXWall, out Vector3 pos, out Quaternion rot)
    {
        Vector3 s = CornerSign(k);
        Vector3 cc = CellCenter(c);
        float back = Cell / 2f - WallT / 2f - depth / 2f - 0.05f;
        float side = Cell / 2f - 1.25f;
        if (againstXWall)
        {
            pos = cc + new Vector3(s.x * back, 0f, s.z * side);
            rot = Quaternion.LookRotation(new Vector3(-s.x, 0f, 0f));
        }
        else
        {
            pos = cc + new Vector3(s.x * side, 0f, s.z * back);
            rot = Quaternion.LookRotation(new Vector3(0f, 0f, -s.z));
        }
    }

    /// <summary>Wolny róg, w którym jest prawdziwa ściana (nie otwarte przejście).</summary>
    static bool TakeCorner(Vector2Int c, float depth, out Vector3 pos, out Quaternion rot)
    {
        var opts = new List<Vector2Int>(); // (róg, 1 = przy ścianie X)
        for (int k = 0; k < 4; k++)
        {
            if (usedCorner[c.x, c.y, k]) continue;
            Vector3 s = CornerSign(k);
            if (SideState(c, s.x < 0 ? 0 : 1) != WS.Open) opts.Add(new Vector2Int(k, 1));
            if (SideState(c, s.z < 0 ? 2 : 3) != WS.Open) opts.Add(new Vector2Int(k, 0));
        }
        if (opts.Count == 0) { pos = Vector3.zero; rot = Quaternion.identity; return false; }
        var o = opts[RI(0, opts.Count)];
        usedCorner[c.x, c.y, o.x] = true;
        WallSlot(c, o.x, depth, o.y == 1, out pos, out rot);
        return true;
    }

    /// <summary>Środek pełnej ściany (bez drzwi) – miejsce na blat, regał, lożę...</summary>
    static bool TakeMid(Vector2Int c, float depth, out Vector3 pos, out Quaternion rot)
    {
        var opts = new List<int>();
        for (int s = 0; s < 4; s++) if (!usedMid[c.x, c.y, s] && SideState(c, s) == WS.Solid) opts.Add(s);
        if (opts.Count == 0) { pos = Vector3.zero; rot = Quaternion.identity; return false; }
        int side = opts[RI(0, opts.Count)];
        usedMid[c.x, c.y, side] = true;
        Vector3 n = SideNormal(side);
        pos = CellCenter(c) + n * (Cell / 2f - WallT / 2f - depth / 2f - 0.05f);
        rot = Quaternion.LookRotation(-n);
        return true;
    }

    static int FreeCornerAny(Vector2Int c)
    {
        var free = new List<int>();
        for (int k = 0; k < 4; k++)
        {
            if (usedCorner[c.x, c.y, k]) continue;
            Vector3 s = CornerSign(k);
            bool wall = SideState(c, s.x < 0 ? 0 : 1) != WS.Open || SideState(c, s.z < 0 ? 2 : 3) != WS.Open;
            if (wall) free.Insert(0, k); else free.Add(k);
        }
        if (free.Count == 0) return -1;
        int pick = free[0];
        if (free.Count > 1 && R() < 0.5f) pick = free[RI(0, free.Count)];
        usedCorner[c.x, c.y, pick] = true;
        return pick;
    }

    static void RegisterProp(Vector2Int c, GameObject g)
    {
        if (!cellProps.TryGetValue(c, out var list)) { list = new List<GameObject>(); cellProps[c] = list; }
        list.Add(g);
    }

    static GameObject PlaceWall(Vector2Int c, PropDef def, float anomaly, bool preferMid, bool collider = true)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        bool ok = preferMid && TakeMid(c, def.depth, out pos, out rot);
        if (!ok) ok = TakeCorner(c, def.depth, out pos, out rot);
        if (!ok) return null;
        if (def.tall && NearPipe(pos, 0.9f)) return null; // wysoki mebel nie zmieści się pod rurą
        var root = new GameObject(def.name);
        root.transform.SetParent(levelT);
        root.transform.SetPositionAndRotation(pos, rot);
        def.build(root.transform);
        if (collider) AddRootCollider(root.transform);
        if (R() < anomaly) Anomalize(root, c);
        RegisterProp(c, root);
        return root;
    }

    static GameObject PlaceCenter(Vector2Int c, string name, System.Action<Transform> build, float anomaly, bool collider = true, Transform parent = null, bool tall = false)
    {
        if (usedCenter[c.x, c.y]) return null;
        if (tall && NearPipe(CellCenter(c), 2.0f)) return null;
        usedCenter[c.x, c.y] = true;
        var root = new GameObject(name);
        root.transform.SetParent(parent != null ? parent : levelT);
        root.transform.position = CellCenter(c) + new Vector3(RR(-0.3f, 0.3f), 0f, RR(-0.3f, 0.3f));
        root.transform.rotation = Quaternion.Euler(0f, RI(0, 4) * 90f, 0f);
        build(root.transform);
        if (collider) AddRootCollider(root.transform);
        if (R() < anomaly) Anomalize(root, c);
        if (parent == null || parent == levelT) centerProps[c] = root;
        return root;
    }

    /// <summary>"Pamięć" anomalii odtwarza mebel nieprawidłowo.</summary>
    static void Anomalize(GameObject root, Vector2Int cell)
    {
        Bounds b = LocalBounds(root.transform);
        float h = Mathf.Max(0.05f, b.size.y);
        float foot = Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z));
        Transform t = root.transform;
        root.name += " [ANOMALIA]";

        switch (RI(0, 6))
        {
            case 0: // gigant na środku pokoju
                if (!usedCenter[cell.x, cell.y])
                {
                    Vector3 c = CellCenter(cell);
                    float maxH = NearPipe(c, 1.6f) ? 2.5f : WallH - 0.08f;
                    float s = Mathf.Min(RR(1.8f, 3.4f), maxH / h, 3.0f / foot);
                    if (s > 1.2f)
                    {
                        usedCenter[cell.x, cell.y] = true;
                        t.position = new Vector3(c.x + RR(-0.4f, 0.4f), 0f, c.z + RR(-0.4f, 0.4f));
                        t.rotation = Quaternion.Euler(0f, RR(0f, 360f), 0f);
                        t.localScale = Vector3.one * s;
                        root.name = root.name.Replace("[ANOMALIA]", "[ANOMALIA: gigant]");
                        centerProps[cell] = root;
                        break;
                    }
                }
                goto case 2;
            case 1: // do góry nogami na suficie
                if (NearPipe(t.position, foot * 0.5f + 0.2f)) goto case 2;
                t.position = new Vector3(t.position.x, WallH, t.position.z);
                t.rotation = t.rotation * Quaternion.Euler(180f, 0f, 0f);
                break;
            case 2: // przekrzywiony, zapadnięty w podłogę
                t.rotation = t.rotation * Quaternion.Euler(RR(-20f, 20f), 0f, RR(-25f, 25f));
                t.position += Vector3.down * h * RR(0.1f, 0.4f);
                break;
            case 3: // rozciągnięty w górę
            {
                float maxH = NearPipe(t.position, 0.8f) ? 2.5f : WallH - 0.08f;
                t.localScale = new Vector3(0.75f, Mathf.Max(1.1f, Mathf.Min(RR(1.6f, 2.6f), maxH / h)), 0.75f);
                break;
            }
            case 4: // miniaturka
                t.localScale = Vector3.one * RR(0.3f, 0.45f);
                break;
            case 5: // pomylony materiał – "zapamiętał" złą teksturę
                var mat = styles[(Zone)RI(0, 6)].wall;
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                t.localScale = new Vector3(1f, Mathf.Min(1.25f, (WallH - 0.08f) / h), 0.85f);
                break;
        }
    }

    static Bounds LocalBounds(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    static void AddRootCollider(Transform root)
    {
        Bounds b = LocalBounds(root);
        if (b.size.sqrMagnitude < 0.0001f) return;
        var bc = root.gameObject.AddComponent<BoxCollider>();
        bc.center = b.center;
        bc.size = b.size;
    }

    // =====================================================================
    //  WYPEŁNIANIE STREF
    // =====================================================================

    static PropDef[] kitchenProps, kitchenWall, freezerProps, storageProps, storageWall, diningProps, diningWall, officeProps, oddProps;

    static void InitPropLists()
    {
        kitchenWall = new[]
        {
            new PropDef("Blat", K_Counter, 0.78f), new PropDef("Frytownica", K_Fryer, 0.76f),
            new PropDef("Grill z okapem", K_Grill, 0.88f, true), new PropDef("Zlew", K_Sink, 0.7f), new PropDef("Blat", K_Counter, 0.78f),
            new PropDef("Lodówka z przeszklonymi drzwiami", D_Fridge, 0.75f),
        };
        kitchenProps = new[]
        {
            new PropDef("Regał z garnkami", K_PotShelf, 0.58f), new PropDef("Blat", K_Counter, 0.78f), new PropDef("Frytownica", K_Fryer, 0.76f),
        };
        freezerProps = new[]
        {
            new PropDef("Regał chłodniczy", F_ColdShelf, 0.62f), new PropDef("Regał chłodniczy", F_ColdShelf, 0.62f),
            new PropDef("Bloki lodu", F_IceBlocks, 0.72f), new PropDef("Zamrożone kartony", F_FrozenBoxes, 0.75f),
            new PropDef("Lodówka", D_Fridge, 0.75f),
        };
        storageWall = new[]
        {
            new PropDef("Regał magazynowy", S_Rack, 0.64f), new PropDef("Regał magazynowy", S_Rack, 0.64f), new PropDef("Paleta", S_Pallet, 1.0f),
        };
        storageProps = new[]
        {
            new PropDef("Regał magazynowy", S_Rack, 0.64f), new PropDef("Paleta", S_Pallet, 1.0f),
            new PropDef("Beczki", S_Barrels, 0.7f), new PropDef("Kartony", S_BoxPile, 0.9f),
        };
        diningWall = new[]
        {
            new PropDef("Loża", D_Booth, 0.98f), new PropDef("Automat z napojami", D_Soda, 0.68f), new PropDef("Loża", D_Booth, 0.98f),
            new PropDef("Lodówka z napojami", D_Fridge, 0.75f),
        };
        diningProps = new[]
        {
            new PropDef("Kosz na śmieci", D_TrashBin, 0.52f), new PropDef("Stolik przy ścianie", D_TableWall, 1.3f),
            new PropDef("Stolik przy ścianie", D_TableWall, 1.3f), new PropDef("Automat z napojami", D_Soda, 0.68f),
        };
        officeProps = new[]
        {
            new PropDef("Biurko z monitorem", O_Desk, 0.72f), new PropDef("Szafka na akta", O_Filing, 0.62f),
            new PropDef("Krzesło biurowe", O_Chair, 0.6f), new PropDef("Biurko z monitorem", O_Desk, 0.72f),
        };
        oddProps = new[]
        {
            new PropDef("Frytownica", K_Fryer, 0.76f), new PropDef("Automat z napojami", D_Soda, 0.68f),
            new PropDef("Beczki", S_Barrels, 0.7f), new PropDef("Szafka na akta", O_Filing, 0.62f),
            new PropDef("Maskotka", D_Mascot, 0.62f), new PropDef("Krzesło", r => Chair(r, Vector3.zero, 0f, mChairRed), 0.5f),
            new PropDef("Bloki lodu", F_IceBlocks, 0.72f), new PropDef("Stolik przy ścianie", D_TableWall, 1.3f),
            new PropDef("Lodówka z napojami", D_Fridge, 0.75f),
        };
    }

    static PropDef Pick(PropDef[] list) => list[RI(0, list.Length)];

    static void FillCell(Vector2Int c)
    {
        if (kitchenProps == null) InitPropLists();
        var z = zone[c.x, c.y];
        float an = St(c).anomaly;
        bool start = c == Vector2Int.zero;

        switch (z)
        {
            case Zone.Kitchen:
                for (int s = 0; s < 4; s++) if (R() < 0.75f) PlaceWall(c, Pick(kitchenWall), an, true);
                if (R() < 0.3f) PlaceCenter(c, "Wyspa kuchenna", K_Island, an, true, null, true);
                for (int i = RI(1, 3); i > 0; i--) PlaceWall(c, Pick(kitchenProps), an, false);
                break;
            case Zone.Freezer:
                if (R() < 0.55f) PlaceCenter(c, "Tusze na hakach", F_MeatRail, 0f, false, detailT, true);
                for (int s = 0; s < 4; s++) if (R() < 0.55f) PlaceWall(c, freezerProps[RI(0, 2)], an, true);
                for (int i = RI(1, 3); i > 0; i--) PlaceWall(c, Pick(freezerProps), an, false);
                break;
            case Zone.Storage:
                for (int s = 0; s < 4; s++) if (R() < (start ? 0.3f : 0.6f)) PlaceWall(c, Pick(storageWall), an, true);
                if (!start && R() < 0.25f) PlaceCenter(c, "Paleta z kartonami", S_PalletTall, an);
                for (int i = RI(1, 3); i > 0; i--) PlaceWall(c, Pick(storageProps), an, false);
                break;
            case Zone.Dining:
                if (R() < 0.13f) PlaceCenter(c, "Rzędy krzeseł [ANOMALIA]", D_ChairRows, 0f);
                else if (R() < 0.6f) PlaceCenter(c, "Stolik z krzesłami", D_TableSet, an);
                for (int s = 0; s < 4; s++) if (R() < 0.45f) PlaceWall(c, Pick(diningWall), an, true);
                if (R() < 0.12f) PlaceWall(c, new PropDef("Maskotka", D_Mascot, 0.62f), an, false);
                for (int i = RI(1, 3); i > 0; i--) PlaceWall(c, Pick(diningProps), an, false);
                if (R() < 0.35f) MakeMenuBoard(c);
                break;
            case Zone.Office:
                if (R() < 0.25f) PlaceCenter(c, "Biurka", O_DeskPair, an);
                for (int s = 0; s < 4; s++) if (R() < 0.5f) PlaceWall(c, Pick(officeProps), an, true);
                for (int i = RI(1, 3); i > 0; i--) PlaceWall(c, Pick(officeProps), an, false);
                if (R() < 0.5f) PlaceWall(c, new PropDef("Papiery", O_Papers, 0.8f), 0f, false, false);
                break;
            case Zone.Backrooms:
                if (R() < 0.35f)
                {
                    var root = PlaceWall(c, Pick(oddProps), 0f, R() < 0.5f);
                    if (root != null && !root.name.Contains("ANOMALIA")) Anomalize(root, c); // przedmiot "z innego miejsca"
                }
                if (R() < 0.12f) PlaceCenter(c, "Samotne krzesło", r => Chair(r, Vector3.zero, 0f, mChairRed), 0f);
                break;
        }
        if (R() < 0.15f) PlaceWall(c, new PropDef("Karton", S_SingleBox, 0.6f), an * 0.5f, false);
    }

    // ---------------- KUCHNIA ----------------

    static void K_Counter(Transform r)
    {
        B(r, new Vector3(0f, 0.43f, 0f), new Vector3(1.3f, 0.86f, 0.72f), mSteel);
        B(r, new Vector3(0f, 0.88f, 0f), new Vector3(1.34f, 0.04f, 0.76f), mSteelDark);
        B(r, new Vector3(0f, 0.45f, 0.362f), new Vector3(0.015f, 0.7f, 0.01f), mBlack);
        B(r, new Vector3(-0.1f, 0.62f, 0.37f), new Vector3(0.02f, 0.12f, 0.02f), mSteelDark);
        B(r, new Vector3(0.1f, 0.62f, 0.37f), new Vector3(0.02f, 0.12f, 0.02f), mSteelDark);
        TopClutter(r, 0.9f, 1.1f, 0.55f);
    }

    static void TopClutter(Transform r, float y, float w, float d)
    {
        int n = RI(0, 4);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = new Vector3(RR(-w / 2f, w / 2f), y, RR(-d / 2f, d / 2f));
            switch (RI(0, 3))
            {
                case 0: Cyl(r, p + Vector3.up * 0.1f, 0.04f, 0.2f, mRedPaint); break;
                case 1: Cyl(r, p + Vector3.up * 0.06f, 0.045f, 0.12f, mPaper); break;
                default: B(r, p + Vector3.up * 0.01f, new Vector3(0.4f, 0.02f, 0.3f), mWoodDark, RR(0f, 360f)); break;
            }
        }
    }

    static void K_Fryer(Transform r)
    {
        B(r, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.72f), mSteel);
        B(r, new Vector3(-0.2f, 0.906f, 0.02f), new Vector3(0.33f, 0.015f, 0.45f), mGrease);
        B(r, new Vector3(0.2f, 0.906f, 0.02f), new Vector3(0.33f, 0.015f, 0.45f), mGrease);
        B(r, new Vector3(-0.2f, 0.98f, 0.26f), new Vector3(0.025f, 0.025f, 0.22f), mBlack);
        B(r, new Vector3(0.2f, 0.98f, 0.26f), new Vector3(0.025f, 0.025f, 0.22f), mBlack);
        B(r, new Vector3(0f, 1.12f, -0.33f), new Vector3(0.9f, 0.45f, 0.05f), mSteelDark);
        B(r, new Vector3(0f, 0.75f, 0.362f), new Vector3(0.7f, 0.12f, 0.01f), mBlack);
        B(r, new Vector3(0.3f, 0.75f, 0.37f), new Vector3(0.03f, 0.03f, 0.01f), mEmergency, 0f, false, ShadowCastingMode.Off);
    }

    static void K_Grill(Transform r)
    {
        B(r, new Vector3(0f, 0.42f, 0f), new Vector3(1.2f, 0.84f, 0.72f), mSteelDark);
        B(r, new Vector3(0f, 0.855f, 0.02f), new Vector3(1.1f, 0.03f, 0.6f), mBlack);
        B(r, new Vector3(0f, 0.872f, 0.02f), new Vector3(1.0f, 0.004f, 0.5f), mEmber, 0f, false, ShadowCastingMode.Off);
        B(r, new Vector3(0f, 2.55f, 0.05f), new Vector3(1.3f, 0.45f, 0.85f), mSteel);
        B(r, new Vector3(0f, 2.95f, -0.15f), new Vector3(0.4f, 0.4f, 0.4f), mSteel);
    }

    static void K_Sink(Transform r)
    {
        B(r, new Vector3(0f, 0.43f, 0f), new Vector3(1.2f, 0.86f, 0.68f), mSteel);
        B(r, new Vector3(-0.25f, 0.865f, 0.03f), new Vector3(0.5f, 0.01f, 0.42f), mBlack);
        B(r, new Vector3(0.3f, 0.865f, 0.03f), new Vector3(0.45f, 0.01f, 0.42f), mSteelDark);
        Cyl(r, new Vector3(-0.25f, 1.02f, -0.26f), 0.02f, 0.3f, mSteel);
        B(r, new Vector3(-0.25f, 1.16f, -0.17f), new Vector3(0.03f, 0.03f, 0.2f), mSteel);
    }

    static List<float> Shelf(Transform r, float w, float h, float d, Material m, int levels)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = ((i & 1) == 0 ? -1f : 1f) * (w / 2f - 0.02f);
            float z = ((i & 2) == 0 ? -1f : 1f) * (d / 2f - 0.02f);
            B(r, new Vector3(x, h / 2f, z), new Vector3(0.04f, h, 0.04f), m);
        }
        var ys = new List<float>();
        for (int i = 0; i < levels; i++)
        {
            float y = 0.1f + i * (h - 0.15f) / Mathf.Max(1, levels - 1);
            B(r, new Vector3(0f, y, 0f), new Vector3(w, 0.03f, d), m);
            ys.Add(y);
        }
        return ys;
    }

    static void K_PotShelf(Transform r)
    {
        var ys = Shelf(r, 1.3f, 1.9f, 0.55f, mSteel, 3);
        foreach (float y in ys)
            for (int i = RI(1, 4); i > 0; i--)
            {
                float rad = RR(0.1f, 0.17f), h = RR(0.12f, 0.3f);
                Cyl(r, new Vector3(RR(-0.45f, 0.45f), y + 0.015f + h / 2f, RR(-0.1f, 0.1f)), rad, h, mSteelDark);
            }
    }

    static void K_Island(Transform r)
    {
        B(r, new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.95f), mSteel);
        B(r, new Vector3(0f, 0.92f, 0f), new Vector3(2.04f, 0.04f, 0.99f), mSteelDark);
        TopClutter(r, 0.94f, 1.8f, 0.8f);
        B(r, new Vector3(0f, 2.3f, 0f), new Vector3(1.6f, 0.04f, 0.5f), mSteelDark);
        B(r, new Vector3(-0.7f, 2.75f, 0f), new Vector3(0.02f, 0.9f, 0.02f), mBlack);
        B(r, new Vector3(0.7f, 2.75f, 0f), new Vector3(0.02f, 0.9f, 0.02f), mBlack);
        for (int i = 0; i < 4; i++) Cyl(r, new Vector3(-0.6f + i * 0.4f, 2.05f, 0f), 0.13f, 0.03f, mSteelDark, 'z');
    }

    // ---------------- CHŁODNIA ----------------

    static void F_ColdShelf(Transform r)
    {
        var ys = Shelf(r, 1.3f, 2.2f, 0.6f, mSteel, 4);
        foreach (float y in ys)
            for (int i = RI(1, 4); i > 0; i--)
            {
                float bw = RR(0.25f, 0.45f), bh = RR(0.2f, 0.4f);
                B(r, new Vector3(RR(-0.42f, 0.42f), y + 0.015f + bh / 2f, RR(-0.1f, 0.1f)), new Vector3(bw, bh, RR(0.3f, 0.45f)), R() < 0.6f ? mBoxFrost : mBox, RR(-10f, 10f));
            }
    }

    static void F_IceBlocks(Transform r)
    {
        float y = 0f;
        for (int i = RI(2, 5); i > 0 && y < 1.6f; i--)
        {
            float s = RR(0.4f, 0.65f);
            B(r, new Vector3(RR(-0.2f, 0.2f), y + s * 0.4f, RR(-0.1f, 0.1f)), new Vector3(s, s * 0.8f, s), mIce, RR(-20f, 20f));
            y += s * 0.8f;
        }
    }

    static void F_FrozenBoxes(Transform r)
    {
        for (int i = RI(3, 7); i > 0; i--)
        {
            float s = RR(0.35f, 0.55f);
            int layer = RI(0, 3);
            B(r, new Vector3(RR(-0.4f, 0.4f), s / 2f + layer * 0.45f, RR(-0.2f, 0.2f)), new Vector3(s, s * 0.85f, s), mBoxFrost, RR(-25f, 25f));
        }
    }

    static void F_MeatRail(Transform r)
    {
        Cyl(r, new Vector3(0f, 2.95f, 0f), 0.03f, 3.6f, mSteel, 'x');
        B(r, new Vector3(-1.75f, 3.08f, 0f), new Vector3(0.04f, 0.25f, 0.04f), mSteel);
        B(r, new Vector3(1.75f, 3.08f, 0f), new Vector3(0.04f, 0.25f, 0.04f), mSteel);
        int n = RI(4, 7);
        for (int i = 0; i < n; i++)
        {
            float x = -1.5f + i * (3f / (n - 1)) + RR(-0.08f, 0.08f);
            var hook = new GameObject("Hak").transform;
            hook.SetParent(r, false);
            hook.localPosition = new Vector3(x, 2.93f, 0f);
            hook.gameObject.AddComponent<HangingSway>();
            B(hook, new Vector3(0f, -0.13f, 0f), new Vector3(0.015f, 0.26f, 0.015f), mSteel);
            float s = RR(0.85f, 1.15f);
            Prim(PrimitiveType.Capsule, "Tusza", hook, new Vector3(0f, -0.6f, 0f), new Vector3(0.3f, 0.32f, 0.26f) * s, mMeat, new Vector3(RR(-6f, 6f), RR(0f, 360f), 0f));
        }
    }

    // ---------------- MAGAZYN ----------------

    static void S_Rack(Transform r)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = ((i & 1) == 0 ? -1f : 1f) * 0.61f;
            float z = ((i & 2) == 0 ? -1f : 1f) * 0.27f;
            B(r, new Vector3(x, 1.3f, z), new Vector3(0.08f, 2.6f, 0.08f), mRackOrange);
        }
        float[] levels = { 0.12f, 0.95f, 1.8f, 2.55f };
        for (int l = 0; l < levels.Length; l++)
        {
            float y = levels[l];
            B(r, new Vector3(0f, y, 0.27f), new Vector3(1.3f, 0.07f, 0.05f), mRackOrange);
            B(r, new Vector3(0f, y, -0.27f), new Vector3(1.3f, 0.07f, 0.05f), mRackOrange);
            B(r, new Vector3(0f, y + 0.045f, 0f), new Vector3(1.22f, 0.025f, 0.56f), mWood);
            if (l == levels.Length - 1) continue;
            for (int i = RI(1, 4); i > 0; i--)
            {
                float s = RR(0.3f, 0.5f);
                B(r, new Vector3(RR(-0.38f, 0.38f), y + 0.06f + s * 0.45f, RR(-0.05f, 0.05f)), new Vector3(s, s * 0.9f, s * RR(0.8f, 1.1f)), mBox, RR(-12f, 12f));
            }
        }
    }

    static void S_Pallet(Transform r) => PalletStack(r, RI(1, 3));
    static void S_PalletTall(Transform r) => PalletStack(r, 3);

    static void PalletStack(Transform r, int layers)
    {
        B(r, new Vector3(0f, 0.07f, 0f), new Vector3(1.2f, 0.14f, 1.0f), mWood);
        for (int l = 0; l < layers; l++)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    if (R() < 0.85f)
                        B(r, new Vector3(-0.3f + i * 0.6f, 0.14f + 0.24f + l * 0.49f, -0.25f + j * 0.5f), new Vector3(0.56f, 0.48f, 0.46f), mBox, RR(-6f, 6f));
    }

    static void S_Barrels(Transform r)
    {
        int n = RI(2, 4);
        for (int i = 0; i < n; i++)
        {
            Material m = R() < 0.5f ? mBarrelBlue : mBarrelRust;
            if (i == n - 1 && R() < 0.3f)
                Cyl(r, new Vector3(RR(-0.2f, 0.2f), 0.28f, 0.05f), 0.28f, 0.9f, m, 'x'); // przewrócona
            else
                Cyl(r, new Vector3(-0.32f + i * 0.33f, 0.45f, RR(-0.08f, 0.08f)), 0.28f, 0.9f, m);
        }
    }

    static void S_BoxPile(Transform r)
    {
        for (int i = RI(3, 7); i > 0; i--)
        {
            float s = RR(0.35f, 0.6f);
            int layer = RI(0, 3);
            B(r, new Vector3(RR(-0.4f, 0.4f), s * 0.45f + layer * 0.5f, RR(-0.25f, 0.25f)), new Vector3(s, s * 0.9f, s), mBox, RR(-30f, 30f));
        }
    }

    static void S_SingleBox(Transform r)
    {
        float s = RR(0.4f, 0.6f);
        B(r, new Vector3(0f, s * 0.45f, 0f), new Vector3(s, s * 0.9f, s), mBox, RR(-30f, 30f));
    }

    // ---------------- SALA RESTAURACJI ----------------

    static void Chair(Transform parent, Vector3 lpos, float yaw, Material seat)
    {
        var c = new GameObject("Krzesło").transform;
        c.SetParent(parent, false);
        c.localPosition = lpos;
        c.localRotation = Quaternion.Euler(0f, yaw, 0f);
        B(c, new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.05f, 0.44f), seat);
        B(c, new Vector3(0f, 0.76f, -0.2f), new Vector3(0.44f, 0.55f, 0.05f), seat);
        for (int i = 0; i < 4; i++)
            B(c, new Vector3(((i & 1) == 0 ? -1f : 1f) * 0.19f, 0.22f, ((i & 2) == 0 ? -1f : 1f) * 0.19f), new Vector3(0.035f, 0.44f, 0.035f), mSteelDark);
    }

    static void Table(Transform r, Vector3 p)
    {
        B(r, p + new Vector3(0f, 0.74f, 0f), new Vector3(1.0f, 0.05f, 0.75f), mTableTop);
        Cyl(r, p + new Vector3(0f, 0.37f, 0f), 0.05f, 0.72f, mSteelDark);
        B(r, p + new Vector3(0f, 0.015f, 0f), new Vector3(0.5f, 0.03f, 0.5f), mSteelDark);
    }

    static void D_TableSet(Transform r)
    {
        Table(r, Vector3.zero);
        TopClutter(r, 0.77f, 0.8f, 0.5f);
        var seats = new[] { new Vector4(0f, -0.62f, 0f, 0f), new Vector4(0f, 0.62f, 180f, 0f), new Vector4(-0.72f, 0f, 90f, 0f), new Vector4(0.72f, 0f, -90f, 0f) };
        int n = RI(2, 5);
        for (int i = 0; i < n; i++)
            Chair(r, new Vector3(seats[i].x + RR(-0.08f, 0.08f), 0f, seats[i].y + RR(-0.08f, 0.08f)), seats[i].z + RR(-18f, 18f), mChairRed);
    }

    static void D_TableWall(Transform r)
    {
        Table(r, new Vector3(0f, 0f, -0.27f));
        TopClutter(r, 0.77f, 0.8f, 0.4f);
        if (R() < 0.8f) Chair(r, new Vector3(-0.25f, 0f, 0.42f), 180f + RR(-15f, 15f), mChairRed);
        if (R() < 0.6f) Chair(r, new Vector3(0.25f, 0f, 0.42f), 180f + RR(-15f, 15f), mChairRed);
    }

    static void D_ChairRows(Transform r)
    {
        // anomalia: rzędy identycznych krzeseł zwróconych w jedną stronę, jak widownia
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 4; i++)
                Chair(r, new Vector3(-0.82f + i * 0.55f, 0f, -0.35f + row * 0.7f), 0f, mChairRed);
    }

    static void D_Booth(Transform r)
    {
        B(r, new Vector3(0f, 0.23f, -0.12f), new Vector3(1.3f, 0.46f, 0.5f), mLeatherRed);
        B(r, new Vector3(0f, 0.78f, -0.38f), new Vector3(1.3f, 0.75f, 0.14f), mLeatherRed);
        B(r, new Vector3(0f, 0.74f, 0.3f), new Vector3(1.0f, 0.05f, 0.55f), mTableTop);
        Cyl(r, new Vector3(0f, 0.37f, 0.3f), 0.05f, 0.72f, mSteelDark);
    }

    static void D_TrashBin(Transform r)
    {
        B(r, new Vector3(0f, 0.55f, 0f), new Vector3(0.65f, 1.1f, 0.5f), mWoodDark);
        B(r, new Vector3(0f, 0.85f, 0.255f), new Vector3(0.42f, 0.22f, 0.01f), mBlack);
        B(r, new Vector3(0f, 1.115f, 0f), new Vector3(0.65f, 0.03f, 0.5f), mWood);
        B(r, new Vector3(0f, 1.15f, 0f), new Vector3(0.45f, 0.04f, 0.33f), mRedPaint);
    }

    static void D_Soda(Transform r)
    {
        B(r, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.8f, 0.65f), mRedPaint);
        B(r, new Vector3(0f, 1.25f, 0.33f), new Vector3(0.7f, 0.75f, 0.01f), mSodaPanel, 0f, false, ShadowCastingMode.Off);
        B(r, new Vector3(0f, 0.55f, 0.31f), new Vector3(0.45f, 0.3f, 0.05f), mBlack);
        B(r, new Vector3(0f, 1.85f, 0f), new Vector3(0.92f, 0.1f, 0.67f), mSteelDark);
        AddSpot(r, new Vector3(0f, 1.62f, 0.42f), Quaternion.Euler(28f, 0f, 0f), new Color(1f, 0.35f, 0.25f), 4.5f, 115f, urp ? 2.6f : 0.8f, 2, true);
    }

    static void D_Mascot(Transform r)
    {
        // oryginalna maskotka sieci: kurczak "Kogucik" – anomalia pamięta go trochę źle
        B(r, new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.3f, 0.6f), mRedPaint);
        Prim(PrimitiveType.Sphere, "Brzuch", r, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.9f, 0.6f), mYellowPaint);
        Prim(PrimitiveType.Sphere, "Skrzydło", r, new Vector3(-0.38f, 0.9f, 0f), new Vector3(0.14f, 0.45f, 0.35f), mYellowPaint, new Vector3(0f, 0f, -15f));
        Prim(PrimitiveType.Sphere, "Skrzydło", r, new Vector3(0.38f, 0.9f, 0f), new Vector3(0.14f, 0.45f, 0.35f), mYellowPaint, new Vector3(0f, 0f, 15f));
        var head = new GameObject("Głowa").transform;
        head.SetParent(r, false);
        head.localPosition = new Vector3(0f, 1.55f, 0f);
        head.localRotation = Quaternion.Euler(R() < 0.5f ? 0f : RR(-35f, 35f), R() < 0.35f ? 180f : 0f, R() < 0.5f ? RR(-70f, 70f) : 0f);
        Prim(PrimitiveType.Sphere, "Głowa", head, Vector3.zero, new Vector3(0.55f, 0.55f, 0.5f), mYellowPaint);
        Cyl(head, new Vector3(0f, -0.03f, 0.28f), 0.07f, 0.18f, mOrange, 'z');
        Prim(PrimitiveType.Sphere, "Oko", head, new Vector3(-0.12f, 0.08f, 0.22f), new Vector3(0.09f, 0.11f, 0.05f), mBlack);
        Prim(PrimitiveType.Sphere, "Oko", head, new Vector3(0.12f, 0.08f, 0.22f), new Vector3(0.09f, 0.11f, 0.05f), mBlack);
        B(head, new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.16f, 0.26f), mRedPaint);
        B(head, new Vector3(0f, -0.14f, 0.21f), new Vector3(0.24f, 0.02f, 0.02f), mBlack);
    }

    static void MakeMenuBoard(Vector2Int c)
    {
        WallSlot(c, RI(0, 4), 0.06f, R() < 0.5f, out Vector3 pos, out Quaternion rot);
        var root = new GameObject("Tablica menu");
        root.transform.SetParent(detailT);
        root.transform.SetPositionAndRotation(pos, rot);
        B(root.transform, new Vector3(0f, 2.2f, -0.01f), new Vector3(1.28f, 0.63f, 0.04f), mBlack);
        B(root.transform, new Vector3(0f, 2.2f, 0.012f), new Vector3(1.2f, 0.55f, 0.01f), mMenuPanel, 0f, false, ShadowCastingMode.Off);
    }

    // ---------------- BIURO ----------------

    static void CRT(Transform r, Vector3 p)
    {
        B(r, p + new Vector3(0f, 0.19f, 0f), new Vector3(0.42f, 0.36f, 0.4f), mBeige);
        B(r, p + new Vector3(0f, 0.17f, -0.27f), new Vector3(0.3f, 0.26f, 0.2f), mBeige);
        bool on = R() < 0.7f;
        B(r, p + new Vector3(0f, 0.2f, 0.201f), new Vector3(0.33f, 0.25f, 0.01f), on ? mCRT : mBlack, 0f, false, ShadowCastingMode.Off);
        if (on)
        {
            var g = AddSpot(r, p + new Vector3(0f, 0.22f, 0.26f), Quaternion.Euler(12f, 0f, 0f), new Color(0.55f, 0.75f, 1f), 4f, 105f, urp ? 2.2f : 0.7f, 2, true);
            g.AddComponent<ScreenFlicker>();
        }
    }

    static void O_Desk(Transform r)
    {
        B(r, new Vector3(0f, 0.74f, 0f), new Vector3(1.3f, 0.04f, 0.7f), mWoodDark);
        B(r, new Vector3(-0.63f, 0.36f, 0f), new Vector3(0.04f, 0.72f, 0.66f), mWoodDark);
        B(r, new Vector3(0.42f, 0.36f, 0f), new Vector3(0.42f, 0.72f, 0.66f), mWoodDark);
        B(r, new Vector3(0.42f, 0.5f, 0.335f), new Vector3(0.38f, 0.01f, 0.01f), mBlack);
        B(r, new Vector3(0.42f, 0.25f, 0.335f), new Vector3(0.38f, 0.01f, 0.01f), mBlack);
        CRT(r, new Vector3(-0.1f, 0.76f, -0.08f));
        B(r, new Vector3(-0.1f, 0.775f, 0.2f), new Vector3(0.42f, 0.025f, 0.14f), mBeige);
        for (int i = RI(0, 4); i > 0; i--)
            B(r, new Vector3(RR(-0.5f, 0.5f), 0.762f + i * 0.002f, RR(-0.2f, 0.25f)), new Vector3(0.21f, 0.002f, 0.3f), mPaper, RR(0f, 360f), false, ShadowCastingMode.Off);
    }

    static void O_DeskPair(Transform r)
    {
        var a = new GameObject("Biurko A").transform;
        a.SetParent(r, false);
        a.localPosition = new Vector3(0f, 0f, -0.38f);
        O_Desk(a);
        var b = new GameObject("Biurko B").transform;
        b.SetParent(r, false);
        b.localPosition = new Vector3(0f, 0f, 0.38f);
        b.localRotation = Quaternion.Euler(0f, 180f, 0f);
        O_Desk(b);
    }

    static void O_Filing(Transform r)
    {
        B(r, new Vector3(0f, 0.66f, 0f), new Vector3(0.5f, 1.32f, 0.6f), mGreyMetal);
        for (int i = 0; i < 4; i++)
        {
            B(r, new Vector3(0f, 0.17f + i * 0.32f, 0.302f), new Vector3(0.46f, 0.01f, 0.01f), mBlack);
            B(r, new Vector3(0f, 0.3f + i * 0.32f, 0.31f), new Vector3(0.12f, 0.02f, 0.02f), mSteel);
        }
        if (R() < 0.35f) B(r, new Vector3(0f, 0.5f, 0.45f), new Vector3(0.44f, 0.28f, 0.3f), mGreyMetal);
    }

    static void O_Chair(Transform r) => OfficeChair(r, Vector3.zero, RR(0f, 360f));

    static void OfficeChair(Transform parent, Vector3 lpos, float yaw)
    {
        var c = new GameObject("Krzesło biurowe").transform;
        c.SetParent(parent, false);
        c.localPosition = lpos;
        c.localRotation = Quaternion.Euler(0f, yaw, 0f);
        B(c, new Vector3(0f, 0.48f, 0f), new Vector3(0.48f, 0.08f, 0.48f), mBlack);
        Cyl(c, new Vector3(0f, 0.25f, 0f), 0.025f, 0.42f, mSteelDark);
        B(c, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.06f), mSteelDark);
        B(c, new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.04f, 0.6f), mSteelDark);
        B(c, new Vector3(0f, 0.85f, -0.22f), new Vector3(0.45f, 0.55f, 0.06f), mBlack);
    }

    static void O_Papers(Transform r)
    {
        for (int i = RI(6, 12); i > 0; i--)
            B(r, new Vector3(RR(-0.6f, 0.6f), 0.003f + i * 0.001f, RR(-0.4f, 0.4f)), new Vector3(0.21f, 0.002f, 0.3f), mPaper, RR(0f, 360f), false, ShadowCastingMode.Off);
        B(r, new Vector3(0.3f, 0.15f, -0.2f), new Vector3(0.4f, 0.3f, 0.3f), mBox, RR(-20f, 20f), true);
    }


    // =====================================================================
    //  OBIEKTY ROZGRYWKI
    // =====================================================================

    static Vector2Int PickCell(HashSet<Vector2Int> reserved, int minDist, int maxDist)
    {
        var options = new List<Vector2Int>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var c = new Vector2Int(x, y);
                int d = x + y;
                if (!reserved.Contains(c) && d >= minDist && d <= maxDist) options.Add(c);
            }
        if (options.Count == 0)
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (!reserved.Contains(new Vector2Int(x, y))) options.Add(new Vector2Int(x, y));
        var pick = options[RI(0, options.Count)];
        reserved.Add(pick);
        return pick;
    }

    static Vector2Int PickZoneCell(Zone z, HashSet<Vector2Int> reserved, int minDist)
    {
        var options = new List<Vector2Int>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var c = new Vector2Int(x, y);
                if (zone[x, y] == z && !reserved.Contains(c) && x + y >= minDist) options.Add(c);
            }
        if (options.Count == 0) return PickCell(reserved, minDist, 99);
        var pick = options[RI(0, options.Count)];
        reserved.Add(pick);
        return pick;
    }

    /// <summary>Kryjówki rozstawione równomiernie: żadne dwie w sąsiednich komórkach.</summary>
    static void PlaceHidingSpots(Vector2Int start)
    {
        var have = new HashSet<Vector2Int>();
        if (MakeHidingSpot(start)) have.Add(start);
        var cells = new List<Vector2Int>();
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) cells.Add(new Vector2Int(x, y));
        for (int i = cells.Count - 1; i > 0; i--) { int j = RI(0, i + 1); var tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp; }
        var dirs = new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var c in cells)
        {
            bool near = false;
            foreach (var d in dirs) if (have.Contains(c + d)) near = true;
            if (near || R() > 0.85f) continue;
            if (MakeHidingSpot(c)) have.Add(c);
        }
    }

    static bool MakeHidingSpot(Vector2Int c)
    {
        Zone z = zone[c.x, c.y];
        bool cabinet = z == Zone.Kitchen;
        float depth = cabinet ? 0.78f : 0.72f;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        bool ok = cabinet && TakeMid(c, depth, out pos, out rot);
        if (!ok) ok = TakeCorner(c, depth, out pos, out rot);
        if (!ok) return false;

        var root = new GameObject(cabinet ? "Szafka pod blatem (kryjówka)" : "Szafka (kryjówka)");
        root.transform.SetParent(levelT);
        root.transform.SetPositionAndRotation(pos, rot);
        var hs = root.AddComponent<HidingSpot>();

        if (cabinet)
        {
            B(root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.75f), mSteel, 0f, true);
            B(root.transform, new Vector3(0f, 0.92f, 0f), new Vector3(1.24f, 0.04f, 0.79f), mSteelDark);
            B(root.transform, new Vector3(0f, 0.45f, 0.377f), new Vector3(0.015f, 0.75f, 0.01f), mBlack);
            for (int i = 0; i < 3; i++)
                B(root.transform, new Vector3(-0.3f, 0.52f + i * 0.06f, 0.377f), new Vector3(0.35f, 0.02f, 0.01f), mBlack);
            hs.displayName = "Schowaj się w szafce pod blatem";
            hs.displayNameEn = "Hide in the cabinet under the counter";
            hs.cameraHeight = 0.55f;
        }
        else
        {
            Material m = z == Zone.Freezer ? mLockerWhite : z == Zone.Office ? mLockerGreen : mLocker;
            B(root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.9f, 2.1f, 0.7f), m, 0f, true);
            for (int i = 0; i < 4; i++)
                B(root.transform, new Vector3(0f, 1.5f + i * 0.08f, 0.355f), new Vector3(0.5f, 0.03f, 0.01f), mBlack);
            B(root.transform, new Vector3(0.32f, 1.0f, 0.365f), new Vector3(0.03f, 0.16f, 0.03f), mSteel);
            hs.displayName = z == Zone.Freezer ? "Schowaj się w szafie chłodniczej" : "Schowaj się w szafce";
            hs.displayNameEn = z == Zone.Freezer ? "Hide in the cold storage locker" : "Hide in the locker";
            hs.cameraHeight = 1.6f;
        }
        reach.Add(new ReachPoint { p = hs.ExitPosition, cell = c });
        return true;
    }

    static void MakeRadio(Vector2Int c)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (!TakeMid(c, 0.8f, out pos, out rot) && !TakeCorner(c, 0.8f, out pos, out rot))
        {
            int k = FreeCornerAny(c);
            WallSlot(c, k < 0 ? 0 : k, 0.8f, R() < 0.5f, out pos, out rot);
        }
        var root = new GameObject("RadioStation");
        root.transform.SetParent(levelT);
        root.transform.SetPositionAndRotation(pos, rot);
        var t = root.transform;
        B(t, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 0.8f), mWoodDark);
        B(t, new Vector3(0f, 1.18f, -0.12f), new Vector3(1.1f, 0.55f, 0.45f), mSteelDark);
        B(t, new Vector3(0f, 1.22f, 0.105f), new Vector3(0.8f, 0.28f, 0.01f), mRadioPanel, 0f, false, ShadowCastingMode.Off);
        for (int i = 0; i < 3; i++) Cyl(t, new Vector3(-0.3f + i * 0.3f, 1.0f, 0.12f), 0.035f, 0.03f, mBlack, 'z');
        Cyl(t, new Vector3(0.42f, 1.8f, -0.2f), 0.012f, 0.7f, mSteel);
        Cyl(t, new Vector3(-0.4f, 0.96f, 0.15f), 0.03f, 0.12f, mBlack);
        B(t, new Vector3(0.42f, 1.4f, 0.105f), new Vector3(0.03f, 0.03f, 0.01f), mEmergency, 0f, false, ShadowCastingMode.Off);
        AddSpot(t, new Vector3(0f, 1.55f, 0.32f), Quaternion.Euler(30f, 0f, 0f), new Color(1f, 0.6f, 0.2f), 4f, 110f, urp ? 2.4f : 0.9f, 2, true);
        AddRootCollider(t);
        root.AddComponent<RadioStation>();
        reach.Add(new ReachPoint { p = pos + root.transform.forward * 1.1f, cell = c });
    }

    static Vector3 PedestalSpot(Vector2Int c)
    {
        int k = FreeCornerAny(c);
        if (k < 0) k = RI(0, 4);
        return CornerPos(c, k, 0.8f);
    }

    static void AddFrontReach(Vector3 p, Vector2Int c)
    {
        Vector3 toCenter = CellCenter(c) - p;
        toCenter.y = 0f;
        reach.Add(new ReachPoint { p = p + toCenter.normalized * 0.9f, cell = c });
    }

    static void MakePickup(Vector2Int c, PickupType type, string label, string labelEn, Color col)
    {
        Vector3 p = PedestalSpot(c);
        Box("Pedestal", levelT, p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f), St(c).pedestal, true, false, Quaternion.Euler(0f, RR(-10f, 10f), 0f), 0f);

        var root = new GameObject("Pickup - " + label);
        root.transform.SetParent(itemsT);
        root.transform.position = p + Vector3.up * 1.15f;
        var bc = root.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = Vector3.one * 0.6f;

        var mat = Mat("Item_" + type, col, null, Vector2.one, col * 1.4f, 0.5f);
        switch (type)
        {
            case PickupType.Part: B(root.transform, Vector3.zero, Vector3.one * 0.26f, mat); B(root.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.1f, 0.06f, 0.1f), mSteelDark); break;
            case PickupType.Tracker: B(root.transform, Vector3.zero, new Vector3(0.25f, 0.08f, 0.35f), mat); break;
            case PickupType.Weapon: Cyl(root.transform, Vector3.up * 0.1f, 0.09f, 0.6f, mat); break;
            case PickupType.Battery: Cyl(root.transform, Vector3.zero, 0.05f, 0.2f, mat); break;
        }
        AddSpot(root.transform, new Vector3(0.15f, 0.85f, 0.1f), Quaternion.Euler(80f, 0f, 0f), col, 2.6f, 85f, urp ? 2.2f : 0.9f, 2, true);
        var pu = root.AddComponent<Pickup>();
        pu.type = type;
        pu.displayName = label;
        pu.displayNameEn = labelEn;
        AddFrontReach(p, c);
    }

    static void MakeNote(Vector2Int c, string title, string text, string titleEn, string textEn)
    {
        Vector3 p = PedestalSpot(c);
        Box("Pedestal", levelT, p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f), St(c).pedestal, true, false, null, 0f);
        var root = new GameObject("Note - " + title);
        root.transform.SetParent(itemsT);
        root.transform.position = p + Vector3.up * 0.92f;
        var bc = root.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.5f, 0.3f, 0.5f);
        var paper = Box("Paper", root.transform, Vector3.zero, new Vector3(0.25f, 0.01f, 0.35f), mNotePaper, false, true, Quaternion.Euler(0f, RR(-20f, 20f), 0f), 0f);
        AddSpot(root.transform, new Vector3(0.1f, 0.75f, 0.1f), Quaternion.Euler(82f, 0f, 0f), new Color(1f, 0.95f, 0.8f), 2.2f, 80f, urp ? 1.2f : 0.5f, 2, true);
        var note = root.AddComponent<Note>();
        note.title = title;
        note.text = text;
        note.titleEn = titleEn;
        note.textEn = textEn;
        note.paper = paper.transform;
        AddFrontReach(p, c);
    }

    // =====================================================================
    //  WALIDACJA PRZEJŚĆ (NavMesh w edytorze)
    // =====================================================================

    static bool SampleFloor(Vector3 p, float radius, out Vector3 r)
    {
        if (NavMesh.SamplePosition(new Vector3(p.x, 0.1f, p.z), out NavMeshHit h, radius, NavMesh.AllAreas) && h.position.y < 0.3f)
        {
            r = h.position;
            return true;
        }
        r = p;
        return false;
    }

    static bool RemoveBlocker(Vector2Int c)
    {
        if (centerProps.TryGetValue(c, out GameObject center) && center != null)
        {
            centerProps.Remove(c);
            Object.DestroyImmediate(center);
            return true;
        }
        if (!cellProps.TryGetValue(c, out var list)) return false;
        list.RemoveAll(g => g == null);
        if (list.Count == 0) return false;
        GameObject biggest = null;
        float best = -1f;
        foreach (var g in list)
        {
            Bounds b = LocalBounds(g.transform);
            float a = b.size.x * b.size.z * g.transform.localScale.x * g.transform.localScale.z;
            if (a > best) { best = a; biggest = g; }
        }
        list.Remove(biggest);
        Object.DestroyImmediate(biggest);
        return true;
    }

    /// <summary>
    /// Buduje NavMesh w edytorze i sprawdza, czy z pokoju startowego da się dojść do każdego
    /// pomieszczenia, przedmiotu, notatki, radiostacji, kryjówki i miejsca pod włazem.
    /// Jeśli nie – usuwa blokujące meble i sprawdza ponownie.
    /// </summary>
    static string ValidateLayout()
    {
        int removed = 0;
        for (int iter = 0; iter < 6; iter++)
        {
            var bad = new HashSet<Vector2Int>();
            NavMeshDataInstance inst = default(NavMeshDataInstance);
            try
            {
                var sources = new List<NavMeshBuildSource>();
                UnityEngine.AI.NavMeshBuilder.CollectSources(levelT, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                var bounds = new Bounds(new Vector3(W * Cell / 2f, 1.5f, H * Cell / 2f), new Vector3(W * Cell + 4f, 6f, H * Cell + 4f));
                var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null) return "Walidacja przejść: pominięta (nie udało się zbudować NavMesh).";
                inst = NavMesh.AddNavMeshData(data);
                if (!SampleFloor(CellCenter(Vector2Int.zero), 2.5f, out Vector3 startP))
                    return "Walidacja przejść: pominięta (brak NavMesh w pokoju startowym).";
                var path = new NavMeshPath();
                bool Reach(Vector3 to) => NavMesh.CalculatePath(startP, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;

                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        var c = new Vector2Int(x, y);
                        if (!SampleFloor(CellCenter(c), 2.4f, out Vector3 p) || !Reach(p)) bad.Add(c);
                    }
                foreach (var rp in reach)
                    if (!SampleFloor(rp.p, 1.3f, out Vector3 p) || !Reach(p)) bad.Add(rp.cell);
            }
            catch (System.Exception e)
            {
                return "Walidacja przejść: pominięta (" + e.Message + ").";
            }
            finally
            {
                if (inst.valid) NavMesh.RemoveNavMeshData(inst);
            }

            if (bad.Count == 0)
                return removed == 0 ? "Walidacja przejść: OK – wszystko dostępne." : $"Walidacja przejść: OK (usunięto {removed} blokujących mebli).";

            foreach (var c in bad)
            {
                if (RemoveBlocker(c)) { removed++; continue; }
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H) continue;
                    if (RemoveBlocker(n)) { removed++; break; }
                }
            }
        }
        return $"Walidacja przejść: usunięto {removed} mebli; część miejsc może być trudno dostępna.";
    }

    // =====================================================================
    //  GRACZ (ciało + ręce + kamera)
    // =====================================================================

    static GameObject MakePlayer(Vector3 pos, out GameObject camGo)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = pos + Vector3.up * 0.05f;
        player.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = Vector3.up * 0.9f;
        cc.stepOffset = 0.4f;

        var pivot = Pivot("CameraPivot", player.transform, new Vector3(0f, 1.65f, 0f));
        camGo = new GameObject("PlayerCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(pivot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 48f;            // dalej i tak jest mgła – mniej do rysowania
        cam.useOcclusionCulling = true;
        cam.fieldOfView = 72f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        camGo.AddComponent<AudioListener>();

        // promiennik podczerwieni (noktowizja) – włączany przez Camcorder
        var ir = new GameObject("IR (noktowizja)");
        ir.transform.SetParent(camGo.transform, false);
        var irl = ir.AddComponent<Light>();
        irl.type = LightType.Spot;
        irl.spotAngle = 100f;
        irl.innerSpotAngle = 60f;
        irl.range = 20f;
        irl.intensity = urp ? 10f : 1.2f;
        irl.color = new Color(0.9f, 1f, 0.9f);
        irl.shadows = LightShadows.None;
        irl.enabled = false;

        // ---------- Ręce FPP ----------
        var off = ShadowCastingMode.Off;
        var arms = new GameObject("FP Arms").transform;
        arms.SetParent(camGo.transform, false);
        var fpa = arms.gameObject.AddComponent<FirstPersonArms>();

        var right = Pivot("RightArm (latarka)", arms, new Vector3(0.24f, -0.27f, 0.28f));
        right.localRotation = Quaternion.Euler(4f, -6f, 0f);
        Prim(PrimitiveType.Capsule, "Rękaw", right, new Vector3(0f, -0.02f, -0.24f), new Vector3(0.09f, 0.21f, 0.09f), mJacket, new Vector3(90f, 0f, 0f), off);
        Prim(PrimitiveType.Sphere, "Dłoń", right, Vector3.zero, new Vector3(0.085f, 0.075f, 0.11f), mSkin, default, off);
        B(right, new Vector3(0f, -0.005f, 0.03f), new Vector3(0.09f, 0.05f, 0.07f), mSkin, 0f, false, off);
        Cyl(right, new Vector3(0f, 0.03f, 0.07f), 0.028f, 0.26f, mFlashBody, 'z', off);
        Cyl(right, new Vector3(0f, 0.03f, 0.215f), 0.042f, 0.06f, mFlashBody, 'z', off);
        var lens = Cyl(right, new Vector3(0f, 0.03f, 0.247f), 0.036f, 0.006f, mLens, 'z', off);

        var flGo = new GameObject("Flashlight");
        flGo.transform.SetParent(right, false);
        flGo.transform.localPosition = new Vector3(0f, 0.03f, 0.26f);
        var spot = flGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 26f;
        spot.spotAngle = 52f;
        spot.innerSpotAngle = 26f;
        spot.intensity = urp ? 22f : 2.8f;
        spot.color = new Color(1f, 0.95f, 0.84f);
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 1f;
        spot.shadowNearPlane = 0.1f;
        spot.renderMode = LightRenderMode.ForcePixel;
        if (flashCookie != null) spot.cookie = flashCookie;
        SetupShadowedLight(spot, 0, 2);
        var spillGo = new GameObject("Flashlight Spill (rozproszenie)");
        spillGo.transform.SetParent(flGo.transform, false);
        var spill = spillGo.AddComponent<Light>();
        spill.type = LightType.Spot;
        spill.range = 14f;
        spill.spotAngle = 115f;
        spill.innerSpotAngle = 70f;
        spill.intensity = urp ? 2.6f : 0.4f;
        spill.color = new Color(1f, 0.95f, 0.86f);
        // v3 miało tu cienie wyłączone – szerokie rozproszenie zalewało cień snopu. Teraz rzuca cień tak jak snop.
        spill.shadows = LightShadows.Soft;
        spill.shadowStrength = 1f;
        spill.shadowNearPlane = 0.1f;
        spill.renderMode = LightRenderMode.ForcePixel;
        SetupShadowedLight(spill, 0, 1);
        var fl = flGo.AddComponent<Flashlight>();
        fl.lensRenderer = lens.GetComponent<Renderer>();
        fl.spill = spill;
        var beam = MakeShaft(flGo.transform, 0.03f, 3.2f, 7.5f, new Color(1f, 0.95f, 0.85f), 0.05f, "Flashlight", false);
        if (beam != null)
        {
            beam.gameObject.name = "Snop latarki (wolumetryczny)";
            beam.sharedMaterial.SetFloat("_NearFade", 1.2f);
            beam.sharedMaterial.SetFloat("_LengthFade", 1.6f);
            fl.beamRenderer = beam;
        }

        var left = Pivot("LeftArm", arms, fpa.leftHiddenPos);
        Prim(PrimitiveType.Capsule, "Rękaw", left, new Vector3(0f, -0.02f, -0.24f), new Vector3(0.09f, 0.21f, 0.09f), mJacket, new Vector3(90f, 0f, 0f), off);
        Prim(PrimitiveType.Sphere, "Dłoń", left, Vector3.zero, new Vector3(0.085f, 0.075f, 0.11f), mSkin, default, off);

        // wykrywacz ruchu z ekranem (radar rysowany na teksturze)
        var tracker = Pivot("Tracker (wykrywacz)", left, new Vector3(0.02f, 0.045f, 0.08f));
        B(tracker, Vector3.zero, new Vector3(0.17f, 0.05f, 0.24f), mTrackerBody, 0f, false, off);
        B(tracker, new Vector3(0f, 0.022f, 0.02f), new Vector3(0.145f, 0.004f, 0.165f), mBlack, 0f, false, off);
        var screen = Prim(PrimitiveType.Quad, "Ekran radaru", tracker, new Vector3(0f, 0.0255f, 0.02f), new Vector3(0.13f, 0.15f, 1f), mTrackerRT, new Vector3(90f, 0f, 0f), off);
        Cyl(tracker, new Vector3(0.06f, 0.05f, -0.1f), 0.008f, 0.12f, mFlashBody, 'y', off);
        tracker.gameObject.SetActive(false);

        var ext = Pivot("Gaśnica (model)", left, new Vector3(0.02f, 0.02f, 0.08f));
        Cyl(ext, new Vector3(0f, -0.05f, 0.04f), 0.065f, 0.36f, mRedPaint, 'y', off);
        Cyl(ext, new Vector3(0f, 0.16f, 0.04f), 0.025f, 0.08f, mBlack, 'y', off);
        Cyl(ext, new Vector3(0f, 0.17f, 0.12f), 0.014f, 0.14f, mBlack, 'z', off);
        var spray = MakeParticles(ext, new Vector3(0f, 0.17f, 0.2f), Vector3.zero, new Color(0.95f, 0.97f, 1f, 0.55f),
            0.45f, 7f, 0.8f, 160f, 0.45f, 0, 9f, -0.03f);
        ext.gameObject.SetActive(false);

        // kamera z nagrania (Outlast)
        var camModel = Pivot("Kamera (model)", left, new Vector3(0.0f, 0.04f, 0.06f));
        B(camModel, Vector3.zero, new Vector3(0.08f, 0.1f, 0.18f), mCamBody, 0f, false, off);
        Cyl(camModel, new Vector3(0f, 0.01f, 0.11f), 0.035f, 0.06f, mCamBody, 'z', off);
        Cyl(camModel, new Vector3(0f, 0.01f, 0.142f), 0.028f, 0.005f, mGlassDark, 'z', off);
        B(camModel, new Vector3(0.05f, 0.01f, -0.01f), new Vector3(0.008f, 0.06f, 0.08f), mCamScreen, 0f, false, off);
        B(camModel, new Vector3(-0.025f, 0.052f, 0.07f), new Vector3(0.012f, 0.006f, 0.012f), mEmergency, 0f, false, off);
        camModel.gameObject.SetActive(false);

        fpa.rightArm = right;
        fpa.leftArm = left;
        fpa.trackerModel = tracker.gameObject;
        fpa.extinguisherModel = ext.gameObject;
        fpa.camcorderModel = camModel.gameObject;
        fpa.spray = spray;

        // ---------- Ciało (nogi widać, reszta rzuca tylko cień) ----------
        var so = ShadowCastingMode.ShadowsOnly;
        var body = new GameObject("Body (ciało + cień)");
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.12f);
        var rig = body.AddComponent<ProceduralRig>();
        rig.style = ProceduralRig.Style.Human;
        rig.thighLength = 0.46f;
        rig.shinLength = 0.46f;
        rig.footHeight = 0.07f;

        var hips = Pivot("Hips", body.transform, new Vector3(0f, 0.99f, 0f));
        B(hips, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.16f, 0.2f), mPants, 0f, false, so);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var th = Pivot(sd < 0 ? "ThighL" : "ThighR", hips, new Vector3(0.1f * sd, 0f, 0f));
            Prim(PrimitiveType.Capsule, "Udo", th, new Vector3(0f, -0.23f, 0f), new Vector3(0.15f, 0.25f, 0.15f), mPants);
            var shin = Pivot("Shin", th, new Vector3(0f, -0.46f, 0f));
            Prim(PrimitiveType.Capsule, "Łydka", shin, new Vector3(0f, -0.23f, 0f), new Vector3(0.12f, 0.24f, 0.12f), mPants);
            var foot = Pivot("Foot", shin, new Vector3(0f, -0.46f, 0f));
            B(foot, new Vector3(0f, -0.03f, 0.06f), new Vector3(0.11f, 0.08f, 0.27f), mShoe);
            if (sd < 0) { rig.thighL = th; rig.shinL = shin; rig.footL = foot; }
            else { rig.thighR = th; rig.shinR = shin; rig.footR = foot; }
        }
        var spine = Pivot("Spine", hips, new Vector3(0f, 0.04f, 0f));
        B(spine, new Vector3(0f, 0.31f, 0f), new Vector3(0.4f, 0.56f, 0.24f), mJacket, 0f, false, so);
        B(spine, new Vector3(0f, 0.33f, -0.19f), new Vector3(0.3f, 0.4f, 0.15f), mDark, 0f, false, so); // plecak
        var head = Pivot("Head", spine, new Vector3(0f, 0.6f, 0f));
        Prim(PrimitiveType.Sphere, "Głowa", head, new Vector3(0f, 0.12f, 0.01f), new Vector3(0.21f, 0.24f, 0.23f), mSkin, default, so);
        B(head, new Vector3(0f, 0.22f, 0.02f), new Vector3(0.22f, 0.08f, 0.24f), mJacket, 0f, false, so);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var ua = Pivot(sd < 0 ? "UpperArmL" : "UpperArmR", spine, new Vector3(0.24f * sd, 0.52f, 0f));
            Prim(PrimitiveType.Capsule, "Ramię", ua, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.16f, 0.1f), mJacket, default, so);
            var fa = Pivot("Forearm", ua, new Vector3(0f, -0.3f, 0f));
            Prim(PrimitiveType.Capsule, "Przedramię", fa, new Vector3(0f, -0.14f, 0f), new Vector3(0.09f, 0.15f, 0.09f), mJacket, default, so);
            if (sd < 0) { rig.upperArmL = ua; rig.forearmL = fa; }
            else { rig.upperArmR = ua; rig.forearmR = fa; }
        }
        rig.hips = hips;
        rig.spine = spine;
        rig.head = head;

        player.AddComponent<PlayerInventory>();
        var pc = player.AddComponent<PlayerController>();
        pc.cameraPivot = pivot;
        pc.cameraTransform = camGo.transform;
        pc.bodyRoot = body;
        player.AddComponent<Interactor>();
        player.AddComponent<Extinguisher>();
        var mt = player.AddComponent<MotionTracker>();
        mt.screenRenderer = screen.GetComponent<Renderer>();
        mt.range = 10f;
        player.AddComponent<FearFeedback>();
        var camc = player.AddComponent<Camcorder>();
        camc.irLight = irl;
        player.AddComponent<HandInteraction>();
        return player;
    }

    // =====================================================================
    //  STALKER – "Pracownik": istota, która źle zapamiętała człowieka
    // =====================================================================

    static void MakeStalker(Vector3 pos)
    {
        var root = new GameObject("Stalker");
        root.transform.position = pos;

        var agent = root.AddComponent<NavMeshAgent>();
        agent.speed = 1.8f;
        agent.angularSpeed = 360f;
        agent.acceleration = 10f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.4f;
        agent.height = 2.3f;
        agent.enabled = false;

        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 30f;

        var ai = root.AddComponent<StalkerAI>();

        var model = new GameObject("Model (placeholder)").transform;
        model.SetParent(root.transform, false);
        var rig = model.gameObject.AddComponent<ProceduralRig>();
        rig.style = ProceduralRig.Style.Creature;
        rig.thighLength = 0.58f;
        rig.shinLength = 0.66f;
        rig.footHeight = 0.08f;
        rig.strideLength = 1.5f;
        rig.legSwing = 26f;
        rig.kneeBend = 30f;
        rig.armSwing = 14f;
        rig.baseThigh = -30f;
        rig.baseShin = 70f;
        rig.baseSpine = 28f;
        rig.baseUpperArm = 8f;
        rig.baseForearm = -28f;
        rig.armSpread = 10f;

        var hips = Pivot("Hips", model, new Vector3(0f, 1.09f, 0f));
        B(hips, Vector3.zero, new Vector3(0.3f, 0.14f, 0.2f), mUniformDark);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var th = Pivot(sd < 0 ? "ThighL" : "ThighR", hips, new Vector3(0.13f * sd, 0f, 0f));
            Prim(PrimitiveType.Capsule, "Udo", th, new Vector3(0f, -0.29f, 0f), new Vector3(0.14f, 0.33f, 0.14f), mUniformDark);
            var shin = Pivot("Shin", th, new Vector3(0f, -0.58f, 0f));
            Prim(PrimitiveType.Capsule, "Goleń", shin, new Vector3(0f, -0.33f, 0f), new Vector3(0.09f, 0.35f, 0.09f), mCreatureSkin);
            var foot = Pivot("Foot", shin, new Vector3(0f, -0.66f, 0f));
            B(foot, new Vector3(0f, -0.04f, 0.09f), new Vector3(0.1f, 0.06f, 0.3f), mCreatureSkin);
            B(foot, new Vector3(0f, -0.05f, 0.26f), new Vector3(0.11f, 0.03f, 0.08f), mClaw);
            if (sd < 0) { rig.thighL = th; rig.shinL = shin; rig.footL = foot; }
            else { rig.thighR = th; rig.shinR = shin; rig.footR = foot; }
        }

        var spine = Pivot("Spine", hips, new Vector3(0f, 0.02f, 0f));
        B(spine, new Vector3(0f, 0.19f, 0f), new Vector3(0.3f, 0.36f, 0.2f), mUniform);
        B(spine, new Vector3(0f, 0.55f, 0.02f), new Vector3(0.44f, 0.46f, 0.26f), mUniform);
        B(spine, new Vector3(0f, 0.47f, -0.14f), new Vector3(0.06f, 0.68f, 0.05f), mCreatureSkin); // kręgosłup przebija mundur
        for (int i = 0; i < 4; i++)
            B(spine, new Vector3(0f, 0.22f + i * 0.16f, -0.175f), new Vector3(0.04f, 0.05f, 0.06f), mClaw);
        B(spine, new Vector3(0.11f, 0.66f, 0.152f), new Vector3(0.09f, 0.04f, 0.005f), mPaper); // plakietka z imieniem
        var apron = Pivot("Fartuch", spine, new Vector3(0f, 0.28f, 0.12f));
        B(apron, new Vector3(0f, -0.22f, 0f), new Vector3(0.34f, 0.6f, 0.015f), mApron);
        var sway = apron.gameObject.AddComponent<HangingSway>();
        sway.angle = 6f;
        sway.speed = 1.6f;

        var neck = Pivot("Neck", spine, new Vector3(0f, 0.78f, 0.05f));
        Prim(PrimitiveType.Capsule, "Szyja", neck, new Vector3(0f, 0.13f, 0.02f), new Vector3(0.09f, 0.15f, 0.09f), mCreatureSkin);
        var head = Pivot("Head", neck, new Vector3(0f, 0.27f, 0.04f));
        Prim(PrimitiveType.Sphere, "Czaszka", head, new Vector3(0f, 0.14f, 0.03f), new Vector3(0.27f, 0.4f, 0.31f), mCreatureSkin);
        B(head, new Vector3(0f, 0.17f, 0.183f), new Vector3(0.025f, 0.17f, 0.01f), mCreatureEye, 0f, false, ShadowCastingMode.Off);
        Prim(PrimitiveType.Sphere, "OkoL", head, new Vector3(-0.07f, 0.2f, 0.16f), new Vector3(0.03f, 0.03f, 0.02f), mCreatureEye, default, ShadowCastingMode.Off);
        Prim(PrimitiveType.Sphere, "OkoR", head, new Vector3(0.07f, 0.2f, 0.16f), new Vector3(0.03f, 0.03f, 0.02f), mCreatureEye, default, ShadowCastingMode.Off);
        // paszcza: wnętrze, górne zęby i ruchoma żuchwa (otwiera się przy wrzasku)
        B(head, new Vector3(0f, 0.03f, 0.11f), new Vector3(0.17f, 0.07f, 0.08f), mBlack);
        for (int i = 0; i < 6; i++)
            B(head, new Vector3(-0.08f + i * 0.032f, 0.05f, 0.165f), new Vector3(0.012f, 0.03f, 0.01f), mPaper);
        var jaw = Pivot("Żuchwa", head, new Vector3(0f, 0.035f, 0.08f));
        B(jaw, new Vector3(0f, -0.015f, 0.05f), new Vector3(0.19f, 0.03f, 0.12f), mCreatureSkin);
        for (int i = 0; i < 6; i++)
            B(jaw, new Vector3(-0.08f + i * 0.032f, 0.01f, 0.1f), new Vector3(0.012f, 0.025f, 0.01f), mPaper);
        var cap = Pivot("Czapka", head, new Vector3(0f, 0.31f, 0.02f));
        cap.localRotation = Quaternion.Euler(-8f, 0f, 14f);
        B(cap, Vector3.zero, new Vector3(0.28f, 0.09f, 0.3f), mUniform);
        B(cap, new Vector3(0f, -0.035f, 0.2f), new Vector3(0.25f, 0.02f, 0.14f), mUniformDark);
        AddPointLight(head, new Vector3(0f, 0.18f, 0.35f), new Color(1f, 0.08f, 0.03f), 1.4f, urp ? 0.5f : 0.3f, true);
        AddSpot(head, new Vector3(0f, 0.16f, 0.3f), Quaternion.Euler(12f, 0f, 0f), new Color(1f, 0.1f, 0.04f), 6f, 70f, urp ? 3f : 1f, 2, true)
            .GetComponent<LightCategory>().powered = false;

        for (int sd = -1; sd <= 1; sd += 2)
        {
            var ua = Pivot(sd < 0 ? "UpperArmL" : "UpperArmR", spine, new Vector3(0.27f * sd, 0.74f, 0.02f));
            Prim(PrimitiveType.Capsule, "Rękaw", ua, new Vector3(0f, -0.12f, 0f), new Vector3(0.15f, 0.13f, 0.15f), mUniform);
            Prim(PrimitiveType.Capsule, "Ramię", ua, new Vector3(0f, -0.3f, 0f), new Vector3(0.09f, 0.3f, 0.09f), mCreatureSkin);
            var fa = Pivot("Forearm", ua, new Vector3(0f, -0.58f, 0f));
            Prim(PrimitiveType.Capsule, "Przedramię", fa, new Vector3(0f, -0.3f, 0f), new Vector3(0.08f, 0.31f, 0.08f), mCreatureSkin);
            var hand = Pivot("Hand", fa, new Vector3(0f, -0.6f, 0f));
            B(hand, new Vector3(0f, -0.06f, 0f), new Vector3(0.08f, 0.13f, 0.05f), mCreatureSkin);
            for (int f = -1; f <= 1; f++)
                B(hand, new Vector3(f * 0.027f, -0.24f, 0.015f), new Vector3(0.014f, 0.24f, 0.014f), mClaw);
            if (sd < 0) { rig.upperArmL = ua; rig.forearmL = fa; }
            else { rig.upperArmR = ua; rig.forearmR = fa; }
        }
        rig.hips = hips;
        rig.spine = spine;
        rig.neck = neck;
        rig.head = head;
        rig.jaw = jaw;

        ai.eyes = head;
        ai.model = model.gameObject;
        ai.rig = rig;
        AttachEntityModel(root.transform, model, ai);
    }

    // =====================================================================
    //  POST-PROCESSING (URP, przez refleksję – kompiluje się też bez URP)
    // =====================================================================

    static System.Type FindType(string fullName)
    {
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(fullName, false);
            if (t != null) return t;
        }
        return null;
    }

    static object ConvertTo(object v, System.Type type)
    {
        if (v == null) return null;
        if (type.IsEnum) return System.Enum.ToObject(type, System.Convert.ToInt32(v));
        if (type == typeof(float)) return System.Convert.ToSingle(v);
        if (type == typeof(int)) return System.Convert.ToInt32(v);
        if (type == typeof(bool)) return System.Convert.ToBoolean(v);
        return v;
    }

    static bool SetMember(object obj, string name, object value)
    {
        if (obj == null) return false;
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, bf | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite) { p.SetValue(obj, ConvertTo(value, p.PropertyType)); return true; }
                var f = t.GetField(name, bf | BindingFlags.DeclaredOnly);
                if (f != null) { f.SetValue(obj, ConvertTo(value, f.FieldType)); return true; }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Poza Zasięgiem] Nie udało się ustawić {name}: {e.Message}");
        }
        return false;
    }

    static string ConfigureGraphics()
    {
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.pixelLightCount = 8;
        if (!urp) return "Grafika: Built-in – cienie włączone w Quality (post-processing wymaga URP).";

        int n = 0, shadowsOk = 0;
        foreach (Object a in UsedPipelineAssets())
        {
            SetMember(a, "m_AdditionalLightsRenderingMode", 1); // Per Pixel
            if (SetMember(a, "m_AdditionalLightShadowsSupported", true)) shadowsOk++;
            SetMember(a, "m_AdditionalLightsShadowmapResolution", 4096);
            SetMember(a, "m_AdditionalLightsPerObjectLimit", 8);
            SetMember(a, "m_ShadowDistance", 35f);
            SetMember(a, "m_SoftShadowsSupported", true);
            SetMember(a, "m_SupportsHDR", true);
            SetMember(a, "m_SupportsLightCookies", true);
            // v4: wszystkie świecące obiekty rzucają cień – rozdzielczości poziomów cieni
            SetMember(a, "m_AdditionalLightsShadowResolutionTierLow", 256);
            SetMember(a, "m_AdditionalLightsShadowResolutionTierMedium", 512);
            SetMember(a, "m_AdditionalLightsShadowResolutionTierHigh", 2048);
            SetMember(a, "m_RequireDepthTexture", true);
            EditorUtility.SetDirty(a);
            n++;
        }
        // Forward+ (Unity 2022.2+): brak limitu 8 świateł na obiekt – dużo świateł z cieniem bez "migania"
        int fplus = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string rdPath = AssetDatabase.GUIDToAssetPath(guid);
            if (rdPath.StartsWith("Assets/Mirza")) continue; // cudzych paczek nie ruszamy (kopia AERO jest w Generated/)
            var rd = AssetDatabase.LoadAssetAtPath<Object>(rdPath);
            if (rd == null) continue;
            var f = rd.GetType().GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType.IsEnum && System.Enum.IsDefined(f.FieldType, "ForwardPlus"))
            {
                f.SetValue(rd, System.Enum.Parse(f.FieldType, "ForwardPlus"));
                EditorUtility.SetDirty(rd);
                fplus++;
            }
        }
        AssetDatabase.SaveAssets();
        return n > 0
            ? $"Grafika: URP – cienie wszystkich świateł ({shadowsOk}/{n} assetów URP), HDR, cookie latarki" + (fplus > 0 ? ", Forward+." : ".")
            : "Grafika: URP aktywny, ale nie znaleziono assetu URP do konfiguracji – włącz ręcznie: URP Asset > Lighting > Additional Lights > Cast Shadows.";
    }

    static string SetupPostProcessing(GameObject camGo)
    {
        if (!urp) return "Post-processing: pominięty (projekt bez URP).";
        var volT = FindType("UnityEngine.Rendering.Volume");
        var profT = FindType("UnityEngine.Rendering.VolumeProfile");
        if (volT == null || profT == null) return "Post-processing: nie znaleziono typów Volume.";

        var camDataT = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
        if (camDataT != null)
        {
            var cd = camGo.GetComponent(camDataT);
            if (cd == null) cd = camGo.AddComponent(camDataT);
            SetMember(cd, "renderPostProcessing", true);
            SetMember(cd, "antialiasing", 1); // FXAA
            SetMember(cd, "renderShadows", true);
        }

        // Bazowy wygląd: ciemno, ale latarka i lampy dobrze widoczne; lekkie ziarno.
        var horror = CreateProfile(profT, "PP_Horror", prof =>
        {
            var tm = AddOverride(prof, "Tonemapping"); SetParam(tm, "mode", 2); // ACES
            var bl = AddOverride(prof, "Bloom");
            SetParam(bl, "threshold", 0.95f); SetParam(bl, "intensity", 0.7f); SetParam(bl, "scatter", 0.65f);
            SetParam(bl, "tint", new Color(1f, 0.88f, 0.75f));
            var ca = AddOverride(prof, "ColorAdjustments");
            SetParam(ca, "postExposure", 0.45f); SetParam(ca, "contrast", 14f); SetParam(ca, "saturation", -22f);
            SetParam(ca, "colorFilter", new Color(0.95f, 0.99f, 1f));
            var sp = AddOverride(prof, "SplitToning");
            SetParam(sp, "shadows", new Color(0.36f, 0.52f, 0.55f)); SetParam(sp, "highlights", new Color(1f, 0.8f, 0.6f)); SetParam(sp, "balance", -15f);
            var wb = AddOverride(prof, "WhiteBalance"); SetParam(wb, "temperature", -6f);
            var vg = AddOverride(prof, "Vignette");
            SetParam(vg, "intensity", 0.3f); SetParam(vg, "smoothness", 0.5f); SetParam(vg, "color", Color.black);
            var fg = AddOverride(prof, "FilmGrain");
            SetParam(fg, "type", 0); SetParam(fg, "intensity", 0.12f); SetParam(fg, "response", 0.85f); // Thin1, delikatne
            var cab = AddOverride(prof, "ChromaticAberration"); SetParam(cab, "intensity", 0.07f);
        });

        // Strach: gdy potwór jest blisko (FearFeedback steruje wagą).
        var fear = CreateProfile(profT, "PP_Fear", prof =>
        {
            var vg = AddOverride(prof, "Vignette");
            SetParam(vg, "intensity", 0.6f); SetParam(vg, "smoothness", 0.6f); SetParam(vg, "color", new Color(0.22f, 0f, 0f));
            var cab = AddOverride(prof, "ChromaticAberration"); SetParam(cab, "intensity", 0.85f);
            var ca = AddOverride(prof, "ColorAdjustments"); SetParam(ca, "saturation", -55f); SetParam(ca, "contrast", 30f);
            var ld = AddOverride(prof, "LensDistortion"); SetParam(ld, "intensity", -0.2f);
        });

        // Wizjer kamery: ziarno taśmy, aberracja, winieta, lekka beczka.
        var camcorder = CreateProfile(profT, "PP_Camcorder", prof =>
        {
            var fg = AddOverride(prof, "FilmGrain");
            SetParam(fg, "type", 4); SetParam(fg, "intensity", 0.55f); SetParam(fg, "response", 0.6f); // Medium3
            var cab = AddOverride(prof, "ChromaticAberration"); SetParam(cab, "intensity", 0.35f);
            var vg = AddOverride(prof, "Vignette");
            SetParam(vg, "intensity", 0.45f); SetParam(vg, "smoothness", 0.4f); SetParam(vg, "rounded", true);
            var ld = AddOverride(prof, "LensDistortion"); SetParam(ld, "intensity", -0.12f);
            var ca = AddOverride(prof, "ColorAdjustments"); SetParam(ca, "saturation", -15f); SetParam(ca, "contrast", 8f);
        });

        // Noktowizja: zielony monochrom, mocno rozjaśniony, ciężkie ziarno.
        var nv = CreateProfile(profT, "PP_NightVision", prof =>
        {
            var ca = AddOverride(prof, "ColorAdjustments");
            SetParam(ca, "postExposure", 2.0f); SetParam(ca, "contrast", 25f); SetParam(ca, "saturation", -100f);
            SetParam(ca, "colorFilter", new Color(0.4f, 1f, 0.45f));
            var bl = AddOverride(prof, "Bloom");
            SetParam(bl, "threshold", 0.6f); SetParam(bl, "intensity", 2f); SetParam(bl, "scatter", 0.7f);
            var fg = AddOverride(prof, "FilmGrain");
            SetParam(fg, "type", 8); SetParam(fg, "intensity", 0.8f); SetParam(fg, "response", 0.3f); // Large01
            var vg = AddOverride(prof, "Vignette");
            SetParam(vg, "intensity", 0.55f); SetParam(vg, "smoothness", 0.35f); SetParam(vg, "color", Color.black);
        });

        // Mocna poświata (bloom) – ustawienia "Poświata" przełączają wagę tego wolumenu.
        var glow = CreateProfile(profT, "PP_Glow", prof =>
        {
            var bl = AddOverride(prof, "Bloom");
            SetParam(bl, "threshold", 0.72f); SetParam(bl, "intensity", 2.4f); SetParam(bl, "scatter", 0.82f);
            SetParam(bl, "tint", new Color(1f, 0.86f, 0.7f));
            SetParam(bl, "highQualityFiltering", true);
        });
        // Suwak jasności w ustawieniach miesza te dwa wolumeny.
        var brightUp = CreateProfile(profT, "PP_BrightnessUp", prof =>
        {
            var ca = AddOverride(prof, "ColorAdjustments"); SetParam(ca, "postExposure", 1.6f);
        });
        var brightDown = CreateProfile(profT, "PP_BrightnessDown", prof =>
        {
            var ca = AddOverride(prof, "ColorAdjustments"); SetParam(ca, "postExposure", -1.2f);
        });

        CreateVolume(volT, "PostProcess Volume (Horror)", horror, 0f, 1f);
        CreateVolume(volT, "GlowVolume", glow, 5f, 1f);
        CreateVolume(volT, "BrightnessUpVolume", brightUp, 6f, 0f);
        CreateVolume(volT, "BrightnessDownVolume", brightDown, 6f, 0f);
        CreateVolume(volT, "FearVolume", fear, 10f, 0f);
        CreateVolume(volT, "CamcorderVolume", camcorder, 15f, 0f);
        CreateVolume(volT, "NightVisionVolume", nv, 20f, 0f);
        return "Post-processing: OK (wygląd bazowy + mocny bloom + jasność + strach + wizjer kamery + noktowizja).";
    }

    static void CreateVolume(System.Type volT, string name, Object profile, float priority, float weight)
    {
        var g = new GameObject(name);
        var v = g.AddComponent(volT);
        SetMember(v, "isGlobal", true);
        SetMember(v, "priority", priority);
        SetMember(v, "weight", weight);
        SetMember(v, "sharedProfile", profile);
    }

    static Object CreateProfile(System.Type profT, string name, System.Action<Object> fill)
    {
        string path = $"{GenDir}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        var prof = ScriptableObject.CreateInstance(profT);
        AssetDatabase.CreateAsset(prof, path);
        fill(prof);
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        return prof;
    }

    static Object AddOverride(Object prof, string typeName)
    {
        var t = FindType("UnityEngine.Rendering.Universal." + typeName);
        if (t == null) { Debug.LogWarning("[Poza Zasięgiem] Brak efektu " + typeName); return null; }
        var add = prof.GetType().GetMethod("Add", new[] { typeof(System.Type), typeof(bool) });
        if (add == null) return null;
        var comp = add.Invoke(prof, new object[] { t, false }) as Object;
        if (comp == null) return null;
        comp.name = typeName;
        comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(comp, prof);
        return comp;
    }

    /// <summary>Ustawia parametr efektu i jego overrideState = true (bez tego Volume go ignoruje).</summary>
    static void SetParam(Object comp, string field, object value)
    {
        if (comp == null) return;
        var f = comp.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"[Poza Zasięgiem] Brak parametru {comp.GetType().Name}.{field}"); return; }
        var param = f.GetValue(comp);
        if (param == null) return;
        var pt = param.GetType();
        System.Type valueType = null;
        for (var bt = pt; bt != null; bt = bt.BaseType)
            if (bt.IsGenericType && bt.GetGenericTypeDefinition().Name.StartsWith("VolumeParameter"))
            {
                valueType = bt.GetGenericArguments()[0];
                break;
            }
        if (valueType == null) return;
        object v = ConvertTo(value, valueType);
        var ov = pt.GetMethod("Override", new[] { valueType });
        if (ov != null) { ov.Invoke(param, new[] { v }); return; }
        SetMember(param, "overrideState", true);
        SetMember(param, "value", v);
    }


    // =====================================================================
    //  POMOCNICZE: siatki, bryły, światła, cząsteczki
    // =====================================================================

    /// <summary>Prostopadłościan z UV w metrach (tekstury się nie rozciągają).</summary>
    static Mesh BoxMesh(Vector3 s, float uvY)
    {
        string key = $"{s.x:0.###}_{s.y:0.###}_{s.z:0.###}_{uvY:0.##}";
        if (meshCache.TryGetValue(key, out Mesh cached)) return cached;
        var v = new List<Vector3>(24);
        var uv = new List<Vector2>(24);
        var t = new List<int>(36);
        Vector3 h = s * 0.5f;
        Face(v, uv, t, Vector3.right, Vector3.forward, Vector3.up, h, uvY);
        Face(v, uv, t, Vector3.left, Vector3.forward, Vector3.up, h, uvY);
        Face(v, uv, t, Vector3.up, Vector3.right, Vector3.forward, h, 0f);
        Face(v, uv, t, Vector3.down, Vector3.right, Vector3.forward, h, 0f);
        Face(v, uv, t, Vector3.forward, Vector3.right, Vector3.up, h, uvY);
        Face(v, uv, t, Vector3.back, Vector3.right, Vector3.up, h, uvY);
        var m = new Mesh { name = "Box " + key };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static void Face(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 n, Vector3 u, Vector3 w, Vector3 h, float uvY)
    {
        float hu = Mathf.Abs(Vector3.Dot(h, u)), hw = Mathf.Abs(Vector3.Dot(h, w));
        Vector3 c = Vector3.Scale(n, h);
        Vector3 a = c - u * hu - w * hw, b = c + u * hu - w * hw, cc = c + u * hu + w * hw, d = c - u * hu + w * hw;
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
        float vy = w == Vector3.up ? uvY : 0f;
        uv.Add(new Vector2(0f, vy));
        uv.Add(new Vector2(2f * hu, vy));
        uv.Add(new Vector2(2f * hu, vy + 2f * hw));
        uv.Add(new Vector2(0f, vy + 2f * hw));
        if (Vector3.Dot(Vector3.Cross(b - a, cc - a), n) > 0f)
        { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else
        { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider, bool local,
        Quaternion? rot, float uvY, ShadowCastingMode shadows = ShadowCastingMode.On)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        if (local)
        {
            g.transform.localPosition = pos;
            g.transform.localRotation = rot ?? Quaternion.identity;
        }
        else
        {
            g.transform.position = pos;
            g.transform.rotation = rot ?? Quaternion.identity;
        }
        g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, uvY);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = shadows;
        if (collider) g.AddComponent<BoxCollider>().size = size;
        return g;
    }

    /// <summary>Bryła w układzie lokalnym rodzica.</summary>
    static GameObject B(Transform p, Vector3 lpos, Vector3 size, Material m, float yaw = 0f, bool col = false, ShadowCastingMode sh = ShadowCastingMode.On)
        => Box("Part", p, lpos, size, m, col, true, Quaternion.Euler(0f, yaw, 0f), 0f, sh);

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lpos, Vector3 lscale, Material m,
        Vector3 euler = default, ShadowCastingMode sh = ShadowCastingMode.On, bool collider = false)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lpos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = lscale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = sh;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cyl(Transform p, Vector3 lpos, float radius, float height, Material m, char axis = 'y', ShadowCastingMode sh = ShadowCastingMode.On)
    {
        Vector3 e = axis == 'x' ? new Vector3(0f, 0f, 90f) : axis == 'z' ? new Vector3(90f, 0f, 0f) : Vector3.zero;
        return Prim(PrimitiveType.Cylinder, "Cyl", p, lpos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), m, e, sh);
    }

    static Transform Pivot(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    static GameObject AddPointLight(Transform parent, Vector3 pos, Color c, float range, float intensity, bool local = false)
    {
        var g = new GameObject("Glow");
        g.transform.SetParent(parent, false);
        if (local) g.transform.localPosition = pos; else g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return g;
    }

    static ParticleSystem MakeParticles(Transform parent, Vector3 lpos, Vector3 euler, Color col, float size, float speed,
        float lifetime, float rate, float duration, short burst, float angle, float gravity)
    {
        var g = new GameObject("Particles");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lpos;
        g.transform.localRotation = Quaternion.Euler(euler);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = col;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = ps.emission;
        em.rateOverTime = rate;
        if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = 0.05f;
        var colm = ps.colorOverLifetime;
        colm.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colm.color = new ParticleSystem.MinMaxGradient(grad);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        var pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mParticle;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    // =====================================================================
    //  v4: KORYTARZE
    // =====================================================================

    /// <summary>Komórki Backrooms (i część magazynu) stają się wąskimi korytarzami 2.4 m – krzyże, kąty i ślepe zaułki.</summary>
    static void PlanCorridors()
    {
        corridor = new bool[W, H];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (x == 0 && y == 0) continue;
                var z = zone[x, y];
                if (z == Zone.Backrooms || (z == Zone.Storage && R() < 0.3f)) corridor[x, y] = true;
            }
    }

    static void BuildCorridors()
    {
        var root = new GameObject("Korytarze").transform;
        root.SetParent(levelT);
        const float blk = 1.8f;
        float inner = Cell - 2f * blk;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (!corridor[x, y]) continue;
                var c = new Vector2Int(x, y);
                Vector3 cc = CellCenter(c);
                Material m = St(c).wall;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 sg = CornerSign(k);
                    Vector3 p = cc + new Vector3(sg.x, 0f, sg.z) * (Cell / 2f - blk / 2f) + Vector3.up * (WallH / 2f);
                    Box("Blok korytarza", root, p, new Vector3(blk, WallH, blk), m, true, false, null, 0f);
                    usedCorner[x, y, k] = true;
                }
                for (int side = 0; side < 4; side++)
                {
                    usedMid[x, y, side] = true;
                    if (SideState(c, side) != WS.Solid) continue;
                    Vector3 n = SideNormal(side);
                    Vector3 size = Mathf.Abs(n.x) > 0.5f ? new Vector3(blk, WallH, inner + 0.02f) : new Vector3(inner + 0.02f, WallH, blk);
                    Box("Ślepy zaułek", root, cc + n * (Cell / 2f - blk / 2f) + Vector3.up * (WallH / 2f), size, m, true, false, null, 0f);
                }
                usedCenter[x, y] = true;
                // listwa przypodłogowa i zacieki w korytarzu – trochę detalu
                if (R() < 0.5f)
                {
                    int side = RI(0, 4);
                    Vector3 n = SideNormal(side);
                    float d = SideState(c, side) == WS.Solid ? Cell / 2f - blk - 0.01f : inner / 2f + 0.01f;
                    Vector3 sz = Mathf.Abs(n.x) > 0.5f ? new Vector3(0.02f, RR(0.4f, 1.4f), RR(0.3f, 0.9f)) : new Vector3(RR(0.3f, 0.9f), RR(0.4f, 1.4f), 0.02f);
                    Vector3 off = Mathf.Abs(n.x) > 0.5f ? new Vector3(0f, 0f, RR(-0.6f, 0.6f)) : new Vector3(RR(-0.6f, 0.6f), 0f, 0f);
                    if (SideState(c, side) != WS.Solid) off = Vector3.zero;
                    Box("Zaciek", detailT, cc + n * d + off + Vector3.up * (WallH - sz.y / 2f), sz, mWetStain, false, false, null, 0f, ShadowCastingMode.Off);
                }
            }
    }

    // =====================================================================
    //  v4: ŚWIATŁA Z CIENIEM, SNOPY ŚWIATŁA, MGŁA
    // =====================================================================

    /// <summary>Reflektor rzucający cień (zamiast punktowego "Glow") – dla świecących obiektów.</summary>
    static GameObject AddSpot(Transform parent, Vector3 pos, Quaternion rot, Color c, float range, float angle, float intensity, int category, bool local)
    {
        var g = new GameObject("Światło (z cieniem)");
        g.transform.SetParent(parent, false);
        if (local) { g.transform.localPosition = pos; g.transform.localRotation = rot; }
        else { g.transform.position = pos; g.transform.rotation = rot; }
        var l = g.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = c;
        l.range = range;
        l.spotAngle = angle;
        l.innerSpotAngle = angle * 0.45f;
        l.intensity = intensity;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.9f;
        l.shadowNearPlane = 0.05f;
        SetupShadowedLight(l, category, 0);
        return g;
    }

    /// <summary>Kategoria (ustawienia cieni) + rozdzielczość cienia w URP (0 niska, 1 średnia, 2 wysoka).</summary>
    static void SetupShadowedLight(Light l, int category, int tier)
    {
        var lc = l.GetComponent<LightCategory>();
        if (lc == null) lc = l.gameObject.AddComponent<LightCategory>();
        lc.category = category;
        lc.powered = category != 0; // latarka ma baterie – awaria prądu jej nie gasi
        if (!urp) return;
        var t = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalLightData");
        if (t == null) return;
        var d = l.GetComponent(t);
        if (d == null) d = l.gameObject.AddComponent(t);
        SetMember(d, "m_AdditionalLightsShadowResolutionTier", tier);
    }

    /// <summary>Otwarty stożek wzdłuż +Z (od rTop przy z=0 do rBottom przy z=length). UV.y: 0 u góry, 1 na dole.</summary>
    static Mesh ConeMesh(float rTop, float rBottom, float length)
    {
        string key = $"cone_{rTop:0.00}_{rBottom:0.00}_{length:0.00}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        const int seg = 24;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        float slope = (rTop - rBottom) / length;
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var nn = (dir + new Vector3(0f, 0f, slope)).normalized;
            v.Add(dir * rTop); n.Add(nn); uv.Add(new Vector2(i / (float)seg * 3f, 0f));
            v.Add(dir * rBottom + Vector3.forward * length); n.Add(nn); uv.Add(new Vector2(i / (float)seg * 3f, 1f));
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.Add(a); tri.Add(c); tri.Add(b);
            tri.Add(b); tri.Add(c); tri.Add(d);
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static Material ShaftMaterial(string key, Color col, float alpha)
    {
        if (shaftMats.TryGetValue(key, out var cached)) return cached;
        var sh = Shader.Find("PozaZasiegiem/LightShaft");
        if (sh == null) return null;
        string path = $"{GenDir}/M_Shaft_{key}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(sh); else m.shader = sh;
        var noise = Tex("T_ShaftNoise_v4", 64, 64, (x, y) =>
        {
            float f = Fbm(x, y, 64, 64, 4) * 0.6f + Fbm(x, y, 64, 64, 8) * 0.4f;
            return new Color(f, f, f, 1f);
        });
        m.SetTexture("_Noise", noise);
        m.SetTextureScale("_Noise", new Vector2(1f, 0.6f));
        var c = col; c.a = alpha * (aero && key != "Portal" ? 0.7f : 1f);
        m.SetColor("_Color", c);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_EdgeSoft", 1.6f);
        m.SetFloat("_LengthFade", 1.3f);
        m.SetFloat("_NearFade", 1.5f);
        m.SetVector("_Scroll", new Vector4(0.015f, -0.04f, 0f, 0f));
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        shaftMats[key] = m;
        return m;
    }

    /// <summary>"God ray" – addytywny stożek światła w powietrzu, dziecko obiektu ze światłem (oś +Z = kierunek światła).</summary>
    static Renderer MakeShaft(Transform lightT, float rTop, float rBottom, float length, Color col, float alpha, string key, bool volumetricFx = true)
    {
        var mat = ShaftMaterial(key, col, alpha);
        if (mat == null) return null;
        var g = new GameObject("Snop światła (god ray)");
        g.transform.SetParent(lightT, false);
        g.AddComponent<MeshFilter>().sharedMesh = ConeMesh(rTop, rBottom, length);
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        if (volumetricFx) g.AddComponent<VolumetricFx>();
        return mr;
    }

    static ParticleSystem LoopingFx(string name, Vector3 pos, Vector3 boxSize, Color col, Vector2 size, float life, float rate, int max, float speed, float noise)
    {
        var g = new GameObject(name);
        g.transform.SetParent(fxT, false);
        g.transform.position = pos;
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = col;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission;
        em.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;
        var nz = ps.noise;
        nz.enabled = noise > 0f;
        nz.strength = noise;
        nz.frequency = 0.25f;
        nz.scrollSpeed = 0.1f;
        var colm = ps.colorOverLifetime;
        colm.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colm.color = new ParticleSystem.MinMaxGradient(grad);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        var pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mMist != null ? mMist : mParticle;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        pr.maxParticleSize = 1.5f;
        pr.sortingFudge = 10f;
        g.AddComponent<VolumetricFx>();
        ps.Play();
        return ps;
    }

    /// <summary>Niska mgła w każdej komórce. Materiał cząsteczek jest oświetlany – mgła "zapala się" w snopie latarki i pod lampami.</summary>
    static void MakeMist(Vector2Int c)
    {
        var z = zone[c.x, c.y];
        Vector3 cc = CellCenter(c);
        float w = corridor[c.x, c.y] ? 2.2f : Cell - 1f;
        switch (z)
        {
            case Zone.Freezer:
                LoopingFx("Mroźna mgła", cc + Vector3.up * 0.35f, new Vector3(w, 0.4f, w), new Color(0.8f, 0.9f, 1f, 0.12f), new Vector2(1.6f, 3f), 10f, 5f, 60, 0.12f, 0.25f);
                break;
            case Zone.Kitchen:
                LoopingFx("Para i dym", cc + Vector3.up * 1.6f, new Vector3(w, 1.6f, w), new Color(0.85f, 0.85f, 0.82f, 0.05f), new Vector2(1.8f, 3.2f), 12f, 2.5f, 40, 0.1f, 0.3f);
                break;
            case Zone.Storage:
                LoopingFx("Pył", cc + Vector3.up * 1.2f, new Vector3(w, 1.6f, w), new Color(0.75f, 0.68f, 0.55f, 0.05f), new Vector2(1.6f, 3f), 12f, 2.5f, 40, 0.06f, 0.2f);
                break;
            case Zone.Backrooms:
                LoopingFx("Wilgotna mgiełka", cc + Vector3.up * 0.9f, new Vector3(w, 1.2f, w), new Color(0.9f, 0.86f, 0.65f, 0.06f), new Vector2(1.5f, 2.8f), 12f, 2.5f, 40, 0.05f, 0.2f);
                break;
            default:
                if (R() < 0.6f)
                    LoopingFx("Mgiełka", cc + Vector3.up * 0.8f, new Vector3(w, 1.2f, w), new Color(0.8f, 0.8f, 0.8f, 0.045f), new Vector2(1.5f, 2.8f), 12f, 2f, 30, 0.05f, 0.2f);
                break;
        }
    }

    /// <summary>Kurz unoszący się w świetle lampy.</summary>
    static void MakeDustMotes(Vector2Int c, float lightY)
    {
        Vector3 cc = CellCenter(c);
        var ps = LoopingFx("Kurz w świetle", cc + Vector3.up * (lightY * 0.5f), new Vector3(2.2f, lightY * 0.8f, 2.2f), new Color(1f, 0.95f, 0.85f, 0.6f), new Vector2(0.012f, 0.03f), 9f, 7f, 70, 0.03f, 0.08f);
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.maxParticleSize = 0.05f;
    }

    // ---------------- LODÓWKA (świeci i rzuca cień) ----------------

    static void D_Fridge(Transform r)
    {
        float w = 0.95f, h = 2.0f, d = 0.72f;
        B(r, new Vector3(0f, h / 2f, -d / 2f + 0.03f), new Vector3(w, h, 0.06f), mSteelDark);                 // tył
        B(r, new Vector3(-w / 2f + 0.03f, h / 2f, 0f), new Vector3(0.06f, h, d), mSteelDark);                 // boki
        B(r, new Vector3(w / 2f - 0.03f, h / 2f, 0f), new Vector3(0.06f, h, d), mSteelDark);
        B(r, new Vector3(0f, 0.08f, 0f), new Vector3(w, 0.16f, d), mBlack);                                   // cokół
        B(r, new Vector3(0f, h - 0.11f, 0f), new Vector3(w, 0.22f, d), mSteelDark);                           // góra
        B(r, new Vector3(0f, h - 0.11f, d / 2f + 0.005f), new Vector3(w - 0.12f, 0.15f, 0.01f), mSodaPanel, 0f, false, ShadowCastingMode.Off);
        B(r, new Vector3(0f, 1.05f, -d / 2f + 0.065f), new Vector3(w - 0.14f, 1.6f, 0.01f), mFridgeInner, 0f, false, ShadowCastingMode.Off);
        Material[] bottles = { mRedPaint, mOrange, mBottleGreen, mBarrelBlue, mGlassDark };
        bool anomaly = R() < 0.15f;
        for (int sIdx = 0; sIdx < 4; sIdx++)
        {
            float y = 0.3f + sIdx * 0.42f;
            B(r, new Vector3(0f, y, 0f), new Vector3(w - 0.14f, 0.02f, d - 0.12f), mSteel);
            int count = RI(3, 7);
            for (int i = 0; i < count; i++)
            {
                if (R() < 0.15f) continue;
                float bx = -0.33f + i * 0.13f;
                float bh = anomaly && R() < 0.3f ? RR(0.3f, 0.38f) : RR(0.2f, 0.26f);
                Cyl(r, new Vector3(bx, y + 0.01f + bh / 2f, RR(-0.1f, 0.12f)), 0.035f, bh, bottles[RI(0, bottles.Length)]);
            }
        }
        // przeszklone drzwi (bez cienia – światło z wnętrza przechodzi na podłogę)
        B(r, new Vector3(0f, 1.05f, d / 2f - 0.02f), new Vector3(w - 0.1f, 1.7f, 0.02f), mFridgeGlass, 0f, false, ShadowCastingMode.Off);
        B(r, new Vector3(w / 2f - 0.12f, 1.1f, d / 2f + 0.02f), new Vector3(0.03f, 0.5f, 0.03f), mSteel);
        var g = AddSpot(r, new Vector3(0f, h - 0.3f, 0.12f), Quaternion.Euler(38f, 0f, 0f), new Color(0.85f, 0.95f, 1f), 5f, 120f, urp ? 3f : 1f, 2, true);
        if (R() < 0.25f) g.AddComponent<FlickerLight>();
    }

    static void CreateV4Materials()
    {
        mFridgeGlass = Mat("M_FridgeGlass", new Color(0.7f, 0.85f, 0.95f, 0.22f), null, Vector2.one, null, 0.95f);
        MakeTransparent(mFridgeGlass);
        mFridgeInner = Mat("M_FridgeInnerLight", new Color(0.9f, 0.95f, 1f), null, Vector2.one, new Color(0.8f, 0.9f, 1f) * 1.6f, 0.4f);
        mBottleGreen = Mat("M_BottleGreen", new Color(0.1f, 0.4f, 0.15f), null, Vector2.one, null, 0.9f);

        // oświetlana mgła: w ciemności jej nie widać, w snopie latarki "gęstnieje"
        string path = GenDir + "/M_Mist.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/T_SoftParticle.asset");
        Shader sh = null;
        if (urp)
        {
            sh = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Lit");
        }
        else sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) { mMist = null; return; }
        mMist = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mMist == null;
        if (isNew) mMist = new Material(sh); else mMist.shader = sh;
        mMist.mainTexture = tex;
        if (mMist.HasProperty("_BaseMap")) mMist.SetTexture("_BaseMap", tex);
        if (mMist.HasProperty("_BaseColor")) mMist.SetColor("_BaseColor", Color.white);
        if (mMist.HasProperty("_Color")) mMist.SetColor("_Color", Color.white);
        if (urp && sh.name.Contains("Universal")) MakeTransparent(mMist);
        if (mMist.HasProperty("_ReceiveShadows")) mMist.SetFloat("_ReceiveShadows", 1f);
        if (isNew) AssetDatabase.CreateAsset(mMist, path); else EditorUtility.SetDirty(mMist);
    }

    // =====================================================================
    //  v5: BRAMA SERWISOWA, TUNEL POŚCIGU, PORTAL AGENCJI ECHO, PRZERYWNIKI
    // =====================================================================

    class StoryRefs
    {
        public GateController gate;
        public Transform checkpoint, stalkerSpawn, sirenPoint, portalWalk, agentsLook;
        public Light flood;
        public Bounds chaseZone, endZone;
        public GameObject tunnelLights;
        public GameObject surface, introSet, endingSet;
        public Transform introCamStart, introCamEnd, introLook, introHole;
        public List<Transform> floorPieces = new List<Transform>();
        public List<Transform> friends = new List<Transform>();
        public Transform endCamStart, endCamEnd, endCamLook, endPlayerStart, endPlayerWalkTo, endPortal;
    }
    static StoryRefs story;
    static int gateY = 1;
    static Material mHazmat, mHelmet, mVisor, mPortalRing, mVanWhite, mMoon, mBeacon, mAsphalt;

    static void CreateV5Materials()
    {
        mHazmat = Mat("M_EchoHazmat", new Color(0.95f, 0.72f, 0.08f), null, Vector2.one, null, 0.35f);
        mHelmet = Mat("M_EchoHelmet", new Color(0.85f, 0.87f, 0.86f), null, Vector2.one, null, 0.65f);
        mVisor = Mat("M_EchoVisor", new Color(0.04f, 0.12f, 0.16f), null, Vector2.one, new Color(0.2f, 0.75f, 1f) * 0.7f, 0.95f);
        mPortalRing = Mat("M_PortalRing", new Color(0.3f, 0.9f, 1f), null, Vector2.one, new Color(0.3f, 0.9f, 1f) * 4f, 0.8f);
        mVanWhite = Mat("M_EchoVan", new Color(0.86f, 0.87f, 0.89f), null, Vector2.one, null, 0.55f, 0.2f);
        mMoon = Mat("M_MoonWindow", new Color(0.2f, 0.3f, 0.45f), null, Vector2.one, new Color(0.25f, 0.35f, 0.6f) * 0.8f, 0.2f);
        mBeacon = Mat("M_GateBeacon", new Color(1f, 0.6f, 0.1f), null, Vector2.one, new Color(1f, 0.55f, 0.1f) * 3f, 0.6f);
        mAsphalt = Mat("M_Asphalt", new Color(0.32f, 0.32f, 0.33f), mConcrete != null ? mConcrete.mainTexture as Texture2D : null, new Vector2(0.25f, 0.25f), null, 0.15f);
    }

    static void PlanGate()
    {
        gateY = Mathf.Clamp(1 + RI(0, 2), 0, H - 1);
        vwG[W, gateY] = WS.Door; // otwór w zewnętrznej ścianie mapy (wschód)
    }

    static Transform Marker(string name, Transform parent, Vector3 pos, float yaw = 0f)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    /// <summary>Światło fabularne: niezależne od awarii prądu (agregat Agencji / awaryjne).</summary>
    static GameObject StoryLight(Transform parent, Vector3 pos, Quaternion rot, Color c, float range, float angle, float intensity, bool local, bool neverCull)
    {
        var g = AddSpot(parent, pos, rot, c, range, angle, intensity, 2, local);
        var lc = g.GetComponent<LightCategory>();
        lc.powered = false;
        lc.neverCull = neverCull;
        lc.excludeFromBake = true; // światło fabularne – tylko na żywo
        return g;
    }

    static void BuildGate()
    {
        float X = W * Cell, gz = (gateY + 0.5f) * Cell;
        var root = new GameObject("Brama serwisowa").transform;
        root.SetParent(dynT);
        root.position = new Vector3(X, 0f, gz);
        root.gameObject.AddComponent<BlackoutImmune>();
        var gc = root.gameObject.AddComponent<GateController>();
        var bars = Pivot("Kraty", root, Vector3.zero);
        for (float z = -1.0f; z <= 1.01f; z += 0.2f) Cyl(bars, new Vector3(0f, 1.2f, z), 0.025f, 2.4f, mSteelDark);
        for (int k = 0; k < 3; k++) B(bars, new Vector3(0f, 0.3f + k * 0.9f, 0f), new Vector3(0.06f, 0.06f, 2.15f), mSteelDark);
        gc.bars = bars;
        var bc = root.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 1.225f, 0f);
        bc.size = new Vector3(0.2f, 2.45f, 2.2f);
        gc.blocker = bc;
        var obs = root.gameObject.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box;
        obs.center = bc.center;
        obs.size = new Vector3(0.6f, 2.45f, 2.4f);
        obs.carving = true;
        gc.obstacle = obs;
        var locked = Mat("M_GateLocked", new Color(0.6f, 0.05f, 0.04f), null, Vector2.one, new Color(1f, 0.08f, 0.04f) * 2.5f, 0.5f);
        gc.lampLocked = locked;
        gc.lampOpen = mExit;
        var lamp = Box("Lampka bramy", root, new Vector3(X - 0.08f, 2.72f, gz + 1.45f), new Vector3(0.06f, 0.14f, 0.14f), locked, false, false, null, 0f, ShadowCastingMode.Off);
        gc.statusLamp = lamp.GetComponent<Renderer>();
        var beacon = Pivot("Kogut bramy", root, new Vector3(-0.3f, 2.95f, 0f));
        beacon.gameObject.AddComponent<SpinLight>().degreesPerSecond = 240f;
        Cyl(beacon, Vector3.zero, 0.09f, 0.14f, mBeacon, 'y', ShadowCastingMode.Off);
        var bl = new GameObject("Światło koguta");
        bl.transform.SetParent(beacon, false);
        bl.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
        var l = bl.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.55f, 0.1f);
        l.range = 14f;
        l.spotAngle = 50f;
        l.intensity = urp ? 10f : 2.5f;
        l.shadows = LightShadows.Soft;
        gc.beacon = beacon.gameObject;
        story.gate = gc;
    }

    static void BuildTunnel()
    {
        float X = W * Cell, gz = (gateY + 0.5f) * Cell;
        var root = new GameObject("Tunel serwisowy (finał)").transform;
        root.SetParent(levelT);
        root.gameObject.AddComponent<BlackoutImmune>();
        const float res = 0.5f;
        float ox = X, oz = gz - 5f;
        const int nx = 124, nz = 54;
        var open = new bool[nx, nz];
        var hgt = new float[nx, nz];
        void AddRect(float x0, float z0, float x1, float z1, float h)
        {
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float cx = ox + (i + 0.5f) * res, cz = oz + (j + 0.5f) * res;
                    if (cx > x0 && cx < x1 && cz > z0 && cz < z1) { open[i, j] = true; hgt[i, j] = Mathf.Max(hgt[i, j], h); }
                }
        }
        float TH = WallH, CH = 6f;
        AddRect(X, gz - 1.5f, X + 16f, gz + 1.5f, TH);
        AddRect(X + 13f, gz - 1.5f, X + 16f, gz + 20f, TH);
        AddRect(X + 13f, gz + 17f, X + 34f, gz + 20f, TH);
        AddRect(X + 31f, gz + 2f, X + 34f, gz + 20f, TH);
        AddRect(X + 31f, gz + 2f, X + 48f, gz + 5f, TH);
        AddRect(X + 48f, gz - 4f, X + 60f, gz + 11f, CH);

        // podłoga i sufit: łączone prostokąty
        var used = new bool[nx, nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                if (!open[i, j] || used[i, j]) continue;
                float h = hgt[i, j];
                int i1 = i;
                while (i1 + 1 < nx && open[i1 + 1, j] && !used[i1 + 1, j] && Mathf.Approximately(hgt[i1 + 1, j], h)) i1++;
                int j1 = j;
                while (j1 + 1 < nz)
                {
                    bool ok = true;
                    for (int k = i; k <= i1; k++)
                        if (!open[k, j1 + 1] || used[k, j1 + 1] || !Mathf.Approximately(hgt[k, j1 + 1], h)) { ok = false; break; }
                    if (!ok) break;
                    j1++;
                }
                for (int a = i; a <= i1; a++) for (int b = j; b <= j1; b++) used[a, b] = true;
                float x0 = ox + i * res, x1 = ox + (i1 + 1) * res, z0 = oz + j * res, z1 = oz + (j1 + 1) * res;
                Vector3 c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
                Vector3 size = new Vector3(x1 - x0, 0.2f, z1 - z0);
                Box("Tunel – podłoga", root, c + Vector3.down * 0.1f, size, mConcrete, true, false, null, 0f, ShadowCastingMode.Off);
                Box("Tunel – sufit", ceilT, c + Vector3.up * (h + 0.1f), size, h > TH + 0.1f ? mSteelDark : mDarkCeil, true, false, null, 0f, ShadowCastingMode.Off);
            }

        // ściany na granicach (i nadproża tam, gdzie zmienia się wysokość)
        bool Op(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && open[i, j];
        float Hg(int i, int j) => Op(i, j) ? hgt[i, j] : 0f;
        const float T = 0.3f;
        for (int j = 0; j <= nz; j++)
        {
            int i = 0;
            while (i < nx)
            {
                float hs = Hg(i, j - 1), hn = Hg(i, j);
                if (Mathf.Max(hs, hn) <= 0f || Mathf.Approximately(hs, hn)) { i++; continue; }
                int i1 = i;
                while (i1 + 1 < nx && Mathf.Approximately(Hg(i1 + 1, j - 1), hs) && Mathf.Approximately(Hg(i1 + 1, j), hn)) i1++;
                float lo = Mathf.Min(hs, hn), hi = Mathf.Max(hs, hn);
                float x0 = ox + i * res, x1 = ox + (i1 + 1) * res, z = oz + j * res;
                float off = lo > 0f ? 0f : (hn > hs ? -T / 2f : T / 2f);
                Box("Tunel – ściana", root, new Vector3((x0 + x1) / 2f, (lo + hi) / 2f, z + off), new Vector3(x1 - x0 + T, hi - lo, T), mConcreteBlock, true, false, null, lo);
                i = i1 + 1;
            }
        }
        for (int i = 1; i <= nx; i++) // i == 0 to ściana mapy z otworem bramy
        {
            int j = 0;
            while (j < nz)
            {
                float hw = Hg(i - 1, j), he = Hg(i, j);
                if (Mathf.Max(hw, he) <= 0f || Mathf.Approximately(hw, he)) { j++; continue; }
                int j1 = j;
                while (j1 + 1 < nz && Mathf.Approximately(Hg(i - 1, j1 + 1), hw) && Mathf.Approximately(Hg(i, j1 + 1), he)) j1++;
                float lo = Mathf.Min(hw, he), hi = Mathf.Max(hw, he);
                float z0 = oz + j * res, z1 = oz + (j1 + 1) * res, x = ox + i * res;
                float off = lo > 0f ? 0f : (he > hw ? -T / 2f : T / 2f);
                Box("Tunel – ściana", root, new Vector3(x + off, (lo + hi) / 2f, (z0 + z1) / 2f), new Vector3(T, hi - lo, z1 - z0 + T), mConcreteBlock, true, false, null, lo);
                j = j1 + 1;
            }
        }

        // światła awaryjne (zapalają się dopiero przy pościgu) + koguty
        var lights = new GameObject("Światła awaryjne tunelu").transform;
        lights.SetParent(root);
        story.tunnelLights = lights.gameObject;
        Vector3[] path =
        {
            new Vector3(X + 1.5f, 0f, gz), new Vector3(X + 14.5f, 0f, gz), new Vector3(X + 14.5f, 0f, gz + 18.5f),
            new Vector3(X + 32.5f, 0f, gz + 18.5f), new Vector3(X + 32.5f, 0f, gz + 3.5f), new Vector3(X + 48f, 0f, gz + 3.5f)
        };
        int n = 0;
        for (int s = 0; s < path.Length - 1; s++)
        {
            Vector3 a = path[s], b = path[s + 1];
            float len = Vector3.Distance(a, b);
            Vector3 dir = (b - a).normalized;
            for (float d = 3f; d < len - 0.5f; d += 6f)
            {
                Vector3 p = Vector3.Lerp(a, b, d / len) + Vector3.up * (TH - 0.12f);
                Box("Lampa awaryjna", lights, p + Vector3.up * 0.05f, new Vector3(0.35f, 0.12f, 0.2f), mEmergency, false, false, null, 0f, ShadowCastingMode.Off);
                if (n % 3 == 1)
                {
                    var piv = new GameObject("Kogut").transform;
                    piv.SetParent(lights);
                    piv.position = p;
                    piv.gameObject.AddComponent<SpinLight>().degreesPerSecond = 260f;
                    StoryLight(piv, Vector3.zero, Quaternion.Euler(35f, 0f, 0f), new Color(1f, 0.12f, 0.04f), 10f, 55f, urp ? 9f : 2f, true, false);
                }
                else
                {
                    var g = StoryLight(lights, p + Vector3.down * 0.1f, Quaternion.Euler(90f, 0f, 0f), new Color(1f, 0.1f, 0.04f), 5.5f, 125f, urp ? 4f : 1.2f, false, false);
                    if (n % 3 == 2) g.AddComponent<FlickerLight>();
                }
                // przeszkody przy ścianach (do omijania w biegu)
                if (n % 2 == 0 && s > 0)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * ((n / 2) % 2 == 0 ? 1.05f : -1.05f);
                    var ob = new GameObject("Przeszkoda").transform;
                    ob.SetParent(root);
                    ob.position = Vector3.Lerp(a, b, (d + 1.5f) / len) + side;
                    ob.rotation = Quaternion.Euler(0f, RR(0f, 360f), 0f);
                    if (R() < 0.5f) S_SingleBox(ob); else Cyl(ob, new Vector3(0f, 0.45f, 0f), 0.28f, 0.9f, R() < 0.5f ? mBarrelBlue : mBarrelRust);
                    AddRootCollider(ob);
                }
                n++;
            }
            // zielona strzałka na ścianie na końcu odcinka
            if (s < path.Length - 2)
                Box("Strzałka EXIT", root, b + dir * 1.47f + Vector3.up * 1.9f,
                    Mathf.Abs(dir.x) > 0.5f ? new Vector3(0.03f, 0.25f, 0.7f) : new Vector3(0.7f, 0.25f, 0.03f), mExit, false, false, null, 0f, ShadowCastingMode.Off);
            if (s == 1 || s == 3)
                LoopingFx("Para z rury", Vector3.Lerp(a, b, 0.5f) + Vector3.up * 2.6f, new Vector3(0.4f, 0.2f, 0.4f), new Color(0.85f, 0.85f, 0.85f, 0.08f), new Vector2(0.6f, 1.4f), 3f, 8f, 40, 0.6f, 0.4f);
        }

        // ---------- komora z portalem Agencji ECHO ----------
        var portal = MakePortal(root, new Vector3(X + 57.6f, 0f, gz + 3.5f), -90f);
        AddRootCollider(portal);
        story.portalWalk = Marker("Portal – punkt wejścia", root, new Vector3(X + 56.6f, 0f, gz + 3.5f));
        story.agentsLook = Marker("Agenci – spojrzenie", root, new Vector3(X + 54.5f, 1.5f, gz + 3.5f));
        MakeAgent(root, new Vector3(X + 54f, 0f, gz + 0.8f), -70f, true);
        MakeAgent(root, new Vector3(X + 54.5f, 0f, gz + 6.3f), -110f, false);
        MakeAgent(root, new Vector3(X + 56.5f, 0f, gz + 8.8f), -135f, false);
        var tripod = new GameObject("Reflektor ECHO").transform;
        tripod.SetParent(root);
        tripod.position = new Vector3(X + 52.8f, 0f, gz + 5.4f);
        tripod.rotation = Quaternion.Euler(0f, -100f, 0f);
        for (int k = 0; k < 3; k++)
        {
            float ang = k * 120f;
            Vector3 foot = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, 0.45f);
            Box("Noga statywu", tripod, foot * 0.5f + Vector3.up * 0.85f, new Vector3(0.04f, 1.75f, 0.04f), mSteelDark, false, true, Quaternion.LookRotation(-foot) * Quaternion.Euler(-14f, 0f, 0f), 0f);
        }
        B(tripod, new Vector3(0f, 1.9f, 0f), new Vector3(0.6f, 0.45f, 0.25f), mSteelDark);
        B(tripod, new Vector3(0f, 1.9f, 0.13f), new Vector3(0.5f, 0.36f, 0.01f), mLens, 0f, false, ShadowCastingMode.Off);
        var flood = StoryLight(tripod, new Vector3(0f, 1.9f, 0.3f), Quaternion.Euler(6f, 0f, 0f), new Color(0.95f, 0.97f, 1f), 20f, 60f, urp ? 10f : 2f, true, true);
        story.flood = flood.GetComponent<Light>();
        StoryLight(root, new Vector3(X + 49.5f, CH - 0.3f, gz - 3f), Quaternion.Euler(65f, 50f, 0f), new Color(1f, 0.92f, 0.8f), 14f, 90f, urp ? 7f : 1.6f, false, false);
        StoryLight(root, new Vector3(X + 59f, CH - 0.3f, gz + 10f), Quaternion.Euler(65f, 230f, 0f), new Color(0.8f, 0.92f, 1f), 14f, 90f, urp ? 7f : 1.6f, false, false);
        for (int k = 0; k < 3; k++)
        {
            var crate = new GameObject("Skrzynia ECHO").transform;
            crate.SetParent(root);
            crate.position = new Vector3(X + 50f + k * 3.2f, 0f, gz + 10.2f);
            crate.rotation = Quaternion.Euler(0f, RR(-8f, 8f), 0f);
            B(crate, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f), mSteelDark, 0f, false);
            B(crate, new Vector3(0f, 0.55f, -0.405f), new Vector3(1.0f, 0.06f, 0.01f), mPortalRing, 0f, false, ShadowCastingMode.Off);
            AddRootCollider(crate);
        }
        var gen = new GameObject("Agregat ECHO").transform;
        gen.SetParent(root);
        gen.position = new Vector3(X + 58.5f, 0f, gz - 2.5f);
        B(gen, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1f), mVanWhite);
        B(gen, new Vector3(0f, 0.8f, 0.505f), new Vector3(0.5f, 0.2f, 0.01f), mPortalRing, 0f, false, ShadowCastingMode.Off);
        AddRootCollider(gen);
        for (int k = 0; k < 4; k++)
            Box("Kabel", root, new Vector3(X + 55f + k * 0.15f, 0.02f, gz - 1f + k * 1.3f), new Vector3(5f, 0.04f, 0.05f), mBlack, false, false, Quaternion.Euler(0f, 20f + k * 15f, 0f), 0f, ShadowCastingMode.Off);

        story.chaseZone = new Bounds(new Vector3(X + 9f, 1.5f, gz), new Vector3(2f, 4f, 3.4f));
        story.endZone = new Bounds(new Vector3(X + 50f, 1.5f, gz + 3.5f), new Vector3(3f, 4f, 4f));
        story.checkpoint = Marker("Punkt kontrolny (brama)", root, new Vector3(X + 2.5f, 0.05f, gz), 90f);
        story.stalkerSpawn = Marker("Pościg – start potwora", root, new Vector3(X - 3f, 0f, gz));
        story.sirenPoint = Marker("Syrena", root, new Vector3(X + 14.5f, 2.8f, gz + 9f));
    }

    /// <summary>Portal Agencji ECHO: świecący pierścień, wirująca tafla, cząsteczki i światło.</summary>
    static Transform MakePortal(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("Portal ECHO").transform;
        root.SetParent(parent);
        root.position = pos;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        root.gameObject.AddComponent<BlackoutImmune>();
        const float R0 = 1.5f, cy = 1.75f;
        for (int i = 0; i < 28; i++)
        {
            float a = i / 28f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * R0, cy + Mathf.Sin(a) * R0, 0f);
            Box("Pierścień", root, p, new Vector3(0.36f, 0.16f, 0.24f), mPortalRing, false, true, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), 0f, ShadowCastingMode.Off);
        }
        B(root, new Vector3(0f, 0.12f, 0f), new Vector3(3.8f, 0.24f, 0.9f), mSteelDark);
        B(root, new Vector3(-1.85f, 0.9f, 0f), new Vector3(0.25f, 1.6f, 0.5f), mSteelDark);
        B(root, new Vector3(1.85f, 0.9f, 0f), new Vector3(0.25f, 1.6f, 0.5f), mSteelDark);
        var surf = ShaftMaterial("Portal", new Color(0.35f, 0.9f, 1f), 0.55f);
        if (surf != null)
        {
            surf.SetFloat("_LengthFade", 0f);
            surf.SetFloat("_EdgeSoft", 0.5f);
            surf.SetFloat("_NearFade", 0.4f);
            surf.SetVector("_Scroll", new Vector4(0.08f, 0.25f, 0f, 0f));
        }
        var disk = Prim(PrimitiveType.Cylinder, "Tafla portalu", root, new Vector3(0f, cy, 0f), new Vector3(2.9f, 0.01f, 2.9f), surf != null ? surf : mPortalRing, new Vector3(90f, 0f, 0f), ShadowCastingMode.Off);
        var sp = disk.AddComponent<SpinLight>();
        sp.degreesPerSecond = 35f;
        AddPointLight(root, new Vector3(0f, cy, 0.8f), new Color(0.35f, 0.9f, 1f), 12f, urp ? 5f : 1.5f, true);

        var g = new GameObject("Iskry portalu");
        g.transform.SetParent(root, false);
        g.transform.localPosition = new Vector3(0f, cy, 0f);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new Color(0.5f, 0.95f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 120;
        var em = ps.emission;
        em.rateOverTime = 45f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 1.45f;
        sh.radiusThickness = 0.15f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.6f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.4f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mParticle;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        ps.Play();
        return root;
    }

    /// <summary>Agent ECHO w żółtym kombinezonie ochronnym.</summary>
    static Transform MakeAgent(Transform parent, Vector3 pos, float yaw, bool lamp)
    {
        var root = new GameObject("Agent ECHO").transform;
        root.SetParent(parent);
        root.position = pos;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        root.gameObject.AddComponent<BlackoutImmune>();
        var body = Pivot("Ciało", root, Vector3.zero);
        body.gameObject.AddComponent<IdleSway>().amount = 1.5f;
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Capsule, "Noga", body, new Vector3(0.12f * s, 0.46f, 0f), new Vector3(0.19f, 0.46f, 0.19f), mHazmat);
            B(body, new Vector3(0.12f * s, 0.05f, 0.05f), new Vector3(0.16f, 0.1f, 0.28f), mBlack);
        }
        Prim(PrimitiveType.Capsule, "Tułów", body, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.42f, 0.34f), mHazmat);
        B(body, new Vector3(0f, 1.25f, -0.24f), new Vector3(0.4f, 0.5f, 0.2f), mSteelDark);
        Prim(PrimitiveType.Sphere, "Hełm", body, new Vector3(0f, 1.74f, 0f), new Vector3(0.33f, 0.35f, 0.33f), mHelmet);
        B(body, new Vector3(0f, 1.75f, 0.14f), new Vector3(0.24f, 0.12f, 0.08f), mVisor, 0f, false, ShadowCastingMode.Off);
        B(body, new Vector3(0f, 1.38f, 0.175f), new Vector3(0.14f, 0.05f, 0.01f), mPortalRing, 0f, false, ShadowCastingMode.Off);
        Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(-0.31f, 1.15f, 0.02f), new Vector3(0.13f, 0.3f, 0.13f), mHazmat, new Vector3(-10f, 0f, 8f));
        if (lamp)
        {
            Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(0.3f, 1.28f, 0.22f), new Vector3(0.13f, 0.3f, 0.13f), mHazmat, new Vector3(80f, 0f, 0f));
            Cyl(body, new Vector3(0.3f, 1.28f, 0.52f), 0.06f, 0.2f, mFlashBody, 'z');
            StoryLight(body, new Vector3(0.3f, 1.28f, 0.64f), Quaternion.Euler(8f, 0f, 0f), new Color(0.85f, 0.95f, 1f), 12f, 40f, urp ? 8f : 2f, true, false);
        }
        else Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(0.31f, 1.15f, 0.02f), new Vector3(0.13f, 0.3f, 0.13f), mHazmat, new Vector3(-10f, 0f, -8f));
        return root;
    }

    /// <summary>Znajomy z intro (Kuba / Ola).</summary>
    static Transform MakeFriend(Transform parent, Vector3 pos, float yaw, Material jacket, bool flashlight)
    {
        var root = new GameObject("Znajomy").transform;
        root.SetParent(parent);
        root.position = pos;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var body = Pivot("Ciało", root, Vector3.zero);
        body.gameObject.AddComponent<IdleSway>().amount = 2f;
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Capsule, "Noga", body, new Vector3(0.11f * s, 0.45f, 0f), new Vector3(0.15f, 0.45f, 0.15f), mPants);
            B(body, new Vector3(0.11f * s, 0.04f, 0.04f), new Vector3(0.12f, 0.08f, 0.26f), mShoe);
        }
        Prim(PrimitiveType.Capsule, "Tułów", body, new Vector3(0f, 1.2f, 0f), new Vector3(0.42f, 0.38f, 0.26f), jacket);
        Prim(PrimitiveType.Sphere, "Głowa", body, new Vector3(0f, 1.68f, 0f), new Vector3(0.22f, 0.25f, 0.23f), mSkin);
        B(body, new Vector3(0f, 1.8f, -0.02f), new Vector3(0.23f, 0.08f, 0.24f), mWoodDark);
        Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(-0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), jacket, new Vector3(-5f, 0f, 8f));
        if (flashlight)
        {
            Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(0.26f, 1.2f, 0.2f), new Vector3(0.11f, 0.28f, 0.11f), jacket, new Vector3(70f, 0f, 0f));
            Cyl(body, new Vector3(0.26f, 1.15f, 0.45f), 0.03f, 0.18f, mFlashBody, 'z');
            StoryLight(body, new Vector3(0.26f, 1.15f, 0.56f), Quaternion.Euler(30f, -10f, 0f), new Color(1f, 0.93f, 0.8f), 16f, 45f, urp ? 14f : 2.5f, true, true);
        }
        else Prim(PrimitiveType.Capsule, "Ręka", body, new Vector3(0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), jacket, new Vector3(-5f, 0f, -8f));
        return root;
    }

    /// <summary>Plany przerywników na "powierzchni": wnętrze magazynu (intro) i plac przed nim (zakończenie).</summary>
    static void BuildSurfaceSets()
    {
        var off = ShadowCastingMode.Off;
        var surf = new GameObject("Powierzchnia (przerywniki)").transform;
        surf.gameObject.AddComponent<BlackoutImmune>();
        story.surface = surf.gameObject;

        // ---------- INTRO: opuszczony magazyn "Kogucika" ----------
        var intro = new GameObject("Magazyn – intro").transform;
        intro.SetParent(surf);
        story.introSet = intro.gameObject;
        Vector3 O = new Vector3(-80f, 60f, 0f);
        float hw = 8f, hd = 6f, h = 6f, hole = 1.5f;
        Box("Podłoga", intro, O + new Vector3(-(hw + hole) / 2f, -0.15f, 0f), new Vector3(hw - hole, 0.3f, hd * 2f), mConcrete, true, false, null, 0f, off);
        Box("Podłoga", intro, O + new Vector3((hw + hole) / 2f, -0.15f, 0f), new Vector3(hw - hole, 0.3f, hd * 2f), mConcrete, true, false, null, 0f, off);
        Box("Podłoga", intro, O + new Vector3(0f, -0.15f, -(hd + hole) / 2f), new Vector3(hole * 2f, 0.3f, hd - hole), mConcrete, true, false, null, 0f, off);
        Box("Podłoga", intro, O + new Vector3(0f, -0.15f, (hd + hole) / 2f), new Vector3(hole * 2f, 0.3f, hd - hole), mConcrete, true, false, null, 0f, off);
        for (int a = 0; a < 2; a++)
            for (int c = 0; c < 2; c++)
            {
                var piece = Box("Spróchniałe deski", intro, O + new Vector3(-0.75f + a * 1.5f, -0.1f, -0.75f + c * 1.5f), new Vector3(1.46f, 0.2f, 1.46f), mWood, false, false,
                    Quaternion.Euler(RR(-2f, 2f), RR(0f, 8f), RR(-2f, 2f)), 0f);
                story.floorPieces.Add(piece.transform);
            }
        float depth = 22f;
        Box("Szyb", intro, O + new Vector3(-hole - 0.1f, -depth / 2f, 0f), new Vector3(0.2f, depth, hole * 2f + 0.4f), mBlack, false, false, null, 0f, off);
        Box("Szyb", intro, O + new Vector3(hole + 0.1f, -depth / 2f, 0f), new Vector3(0.2f, depth, hole * 2f + 0.4f), mBlack, false, false, null, 0f, off);
        Box("Szyb", intro, O + new Vector3(0f, -depth / 2f, -hole - 0.1f), new Vector3(hole * 2f, depth, 0.2f), mBlack, false, false, null, 0f, off);
        Box("Szyb", intro, O + new Vector3(0f, -depth / 2f, hole + 0.1f), new Vector3(hole * 2f, depth, 0.2f), mBlack, false, false, null, 0f, off);
        Box("Ściana", intro, O + new Vector3(0f, h / 2f, hd), new Vector3(hw * 2f, h, 0.4f), mConcreteBlock, true, false, null, 0f);
        Box("Ściana", intro, O + new Vector3(0f, h / 2f, -hd), new Vector3(hw * 2f, h, 0.4f), mConcreteBlock, true, false, null, 0f);
        Box("Ściana", intro, O + new Vector3(-hw, h / 2f, 0f), new Vector3(0.4f, h, hd * 2f), mConcreteBlock, true, false, null, 0f);
        Box("Ściana", intro, O + new Vector3(hw, h / 2f, 0f), new Vector3(0.4f, h, hd * 2f), mConcreteBlock, true, false, null, 0f);
        Box("Dach", intro, O + new Vector3(0f, h + 0.15f, 0f), new Vector3(hw * 2f, 0.3f, hd * 2f), mDarkCeil, false, false, null, 0f);
        for (int k = 0; k < 3; k++)
            Box("Okno", intro, O + new Vector3(-hw + 0.21f, 3.8f, -3.5f + k * 3.5f), new Vector3(0.02f, 1.2f, 1.8f), mMoon, false, false, null, 0f, off);
        StoryLight(intro, O + new Vector3(-hw + 0.7f, 4.4f, 0f), Quaternion.Euler(38f, 90f, 0f), new Color(0.45f, 0.55f, 0.95f), 22f, 85f, urp ? 6f : 1.4f, false, true);
        void P(System.Action<Transform> build, Vector3 lp, float yaw)
        {
            var g = new GameObject("Rekwizyt").transform;
            g.SetParent(intro);
            g.position = O + lp;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            build(g);
        }
        P(S_Rack, new Vector3(-6.4f, 0f, 5.2f), 180f);
        P(S_Rack, new Vector3(-3.6f, 0f, 5.3f), 180f);
        P(S_Rack, new Vector3(7.3f, 0f, 2f), -90f);
        P(S_Pallet, new Vector3(5f, 0f, -4f), 20f);
        P(S_Barrels, new Vector3(-6.5f, 0f, -4.5f), 0f);
        P(S_BoxPile, new Vector3(4f, 0f, 5.1f), 180f);
        P(D_Mascot, new Vector3(-2.6f, 0f, 4.8f), 165f);
        story.friends.Add(MakeFriend(intro, O + new Vector3(1.3f, 0f, 3.4f), 195f, mRedPaint, true));
        story.friends.Add(MakeFriend(intro, O + new Vector3(-1.4f, 0f, 3.9f), 160f, mBarrelBlue, false));
        story.introCamStart = Marker("Intro – kamera (start)", intro, O + new Vector3(-0.6f, 1.62f, -5.2f));
        story.introCamEnd = Marker("Intro – kamera (nad dziurą)", intro, O + new Vector3(0f, 1.6f, -0.1f));
        story.introLook = Marker("Intro – znajomi", intro, O + new Vector3(0f, 1.45f, 3.6f));
        story.introHole = Marker("Intro – dziura", intro, O + new Vector3(0f, 0f, 0.6f));

        // ---------- ZAKOŃCZENIE: plac przed magazynem, noc, furgonetka ECHO ----------
        var end = new GameObject("Plac – zakończenie").transform;
        end.SetParent(surf);
        story.endingSet = end.gameObject;
        Vector3 E = new Vector3(-80f, 60f, 70f);
        Box("Asfalt", end, E + new Vector3(0f, -0.15f, 0f), new Vector3(50f, 0.3f, 50f), mAsphalt, false, false, null, 0f, off);
        Box("Magazyn – fasada", end, E + new Vector3(0f, 4f, 12f), new Vector3(30f, 8f, 1f), mConcreteBlock, false, false, null, 0f);
        Box("Brama magazynu", end, E + new Vector3(-5f, 2.2f, 11.45f), new Vector3(5f, 4.4f, 0.1f), mSteelDark, false, false, null, 0f);
        Box("Szyld", end, E + new Vector3(4f, 6.2f, 11.4f), new Vector3(5f, 1.1f, 0.1f), mSodaPanel, false, false, null, 0f, off);
        story.endPortal = MakePortal(end, E + new Vector3(0f, 0f, 2f), 180f);
        var van = new GameObject("Furgonetka ECHO").transform;
        van.SetParent(end);
        van.position = E + new Vector3(6.5f, 0f, -6.5f);
        van.rotation = Quaternion.Euler(0f, 30f, 0f);
        B(van, new Vector3(0f, 1.35f, 0f), new Vector3(2.1f, 1.9f, 4.6f), mVanWhite);
        B(van, new Vector3(0f, 1.0f, 2.7f), new Vector3(2.1f, 1.2f, 0.9f), mVanWhite);
        B(van, new Vector3(0f, 1.8f, 2.6f), new Vector3(1.9f, 0.55f, 0.7f), mGlassDark);
        for (int s = -1; s <= 1; s += 2)
        {
            B(van, new Vector3(1.06f * s, 1.25f, 0f), new Vector3(0.02f, 0.2f, 4.4f), mPortalRing, 0f, false, off);
            Cyl(van, new Vector3(1.0f * s, 0.38f, 1.9f), 0.38f, 0.28f, mBlack, 'x');
            Cyl(van, new Vector3(1.0f * s, 0.38f, -1.5f), 0.38f, 0.28f, mBlack, 'x');
        }
        var bar = Pivot("Belka świetlna", van, new Vector3(0f, 2.38f, 1.2f));
        B(bar, Vector3.zero, new Vector3(1.2f, 0.1f, 0.25f), mBlack);
        var spinner = Pivot("Obrotowe światła", bar, new Vector3(0f, 0.12f, 0f));
        spinner.gameObject.AddComponent<SpinLight>().degreesPerSecond = 320f;
        StoryLight(spinner, Vector3.zero, Quaternion.Euler(12f, 0f, 0f), new Color(0.15f, 0.35f, 1f), 16f, 50f, urp ? 14f : 2.5f, true, true);
        StoryLight(spinner, Vector3.zero, Quaternion.Euler(12f, 180f, 0f), new Color(1f, 0.1f, 0.08f), 16f, 50f, urp ? 14f : 2.5f, true, true);
        Vector3 vanLamp = van.TransformPoint(new Vector3(0.7f, 2.3f, 2.4f));
        StoryLight(end, vanLamp, Quaternion.LookRotation(E + new Vector3(0f, 1.2f, 1.5f) - vanLamp), new Color(1f, 0.97f, 0.9f), 20f, 45f, urp ? 18f : 3f, false, true);
        Cyl(end, E + new Vector3(-8f, 3f, -4f), 0.08f, 6f, mSteelDark);
        B(end, E + new Vector3(-7.6f, 6f, -4f), new Vector3(0.9f, 0.12f, 0.3f), mSteelDark);
        StoryLight(end, E + new Vector3(-7.4f, 5.9f, -4f), Quaternion.Euler(90f, 0f, 0f), new Color(1f, 0.6f, 0.25f), 14f, 110f, urp ? 16f : 3f, false, true);
        Vector3 moon = E + new Vector3(-10f, 25f, -16f);
        StoryLight(end, moon, Quaternion.LookRotation(E - moon), new Color(0.5f, 0.6f, 1f), 70f, 50f, urp ? 300f : 1.5f, false, true);
        MakeAgent(end, E + new Vector3(2.6f, 0f, -2.6f), 200f, false);
        MakeAgent(end, E + new Vector3(5.2f, 0f, -3.4f), 230f, true);
        MakeFriend(end, E + new Vector3(4.3f, 0f, -7.2f), 330f, mRedPaint, false);
        MakeFriend(end, E + new Vector3(3.1f, 0f, -7.9f), 350f, mBarrelBlue, false);
        Vector3 ps0 = E + new Vector3(0f, 0.05f, 1.2f), ps1 = E + new Vector3(2.6f, 0.05f, -4.4f);
        Vector3 wd = ps1 - ps0;
        story.endPlayerStart = Marker("Koniec – gracz wychodzi z portalu", end, ps0, Mathf.Atan2(wd.x, wd.z) * Mathf.Rad2Deg);
        story.endPlayerWalkTo = Marker("Koniec – gracz idzie do", end, ps1);
        story.endCamStart = Marker("Koniec – kamera (start)", end, E + new Vector3(-6.5f, 1.7f, -9.5f));
        story.endCamEnd = Marker("Koniec – kamera (koniec)", end, E + new Vector3(-3.4f, 1.5f, -7.6f));
        story.endCamLook = Marker("Koniec – kamera patrzy", end, E + new Vector3(1.5f, 1.3f, -1.5f));
        surf.gameObject.SetActive(false);
    }

    // =====================================================================
    //  v5: NOWY MODEL POTWORA (entity.fbx + MonsterAnimationDriver)
    // =====================================================================

    static string entityReport = "";

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void AttachEntityModel(Transform stalkerRoot, Transform placeholder, StalkerAI ai)
    {
        entityReport = "Model potwora: nie znaleziono entity.fbx – został placeholder z brył.";
        string path = null;
        foreach (var g in AssetDatabase.FindAssets("entity t:Model"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant() != "entity") continue;
            if (path == null || p.StartsWith(Root)) path = p;
        }
        if (path == null) return;
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp != null && (imp.importCameras || imp.importLights || imp.importAnimation))
        {
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { entityReport = "Model potwora: nie da się wczytać " + path; return; }

        var wrap = new GameObject("Model (entity.fbx)").transform;
        wrap.SetParent(placeholder, false);
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.name = "entity";
        inst.transform.SetParent(wrap, false);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { Object.DestroyImmediate(wrap.gameObject); entityReport = "Model potwora: entity.fbx nie ma siatek."; return; }

        Bounds Measure()
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }
        Bounds RB(Transform t) { var r = t.GetComponent<Renderer>(); return r != null ? r.bounds : new Bounds(t.position, Vector3.zero); }

        var bb = Measure();
        float target = 2.15f;
        if (bb.size.y > 0.01f) inst.transform.localScale *= target / bb.size.y;

        // przód potwora = kierunek od środka głowy do oczu
        var eyeA = FindDeep(inst.transform, "Cylinder.010");
        var eyeB = FindDeep(inst.transform, "Cylinder.011");
        var headM = FindDeep(inst.transform, "Cylinder.008");
        if (eyeA != null && eyeB != null && headM != null)
        {
            Vector3 eyes = (RB(eyeA).center + RB(eyeB).center) * 0.5f;
            Vector3 dir = eyes - RB(headM).center;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
                inst.transform.RotateAround(wrap.position, Vector3.up, Vector3.SignedAngle(dir, wrap.forward, Vector3.up));
        }
        bb = Measure();
        var hip = FindDeep(inst.transform, "Bone");
        Vector3 c0 = hip != null ? hip.position : bb.center;
        inst.transform.position += new Vector3(wrap.position.x - c0.x, wrap.position.y - bb.min.y, wrap.position.z - c0.z);

        var mHead = Mat("M_EntityHead", new Color(0.3f, 0.2f, 0.15f), null, Vector2.one, null, 0.55f);
        var mFlesh = Mat("M_EntityFlesh", new Color(0.16f, 0.03f, 0.03f), null, Vector2.one, null, 0.7f);
        foreach (var r in rends)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name : "";
                if (n == "Material.011" || n == "Material.012") mats[i] = mCreatureEye;
                else if (n == "Material.010") mats[i] = mHead;
                else if (n == "Material.002" || n == "Material.005" || n == "Material.006") mats[i] = mFlesh;
                else mats[i] = mCreatureSkin;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        Transform Bn(string n) => FindDeep(inst.transform, n);
        var drv = wrap.gameObject.AddComponent<MonsterAnimationDriver>();
        drv.Configure(Bn("Bone"), Bn("Bone.002"),
            new[] { Bn("Bone.006"), Bn("Bone.009") }, new[] { Bn("Bone.007"), Bn("Bone.010") },
            new[] { Bn("Bone.003"), Bn("Bone.004") }, new Transform[0]);
        EditorUtility.SetDirty(drv);
        var headBone = Bn("Bone.002");
        if (headBone != null) ai.headOverride = headBone;

        // oczy: światła placeholdera przenosimy na głowę nowego modelu
        Vector3 eyePos = eyeA != null && eyeB != null ? (RB(eyeA).center + RB(eyeB).center) * 0.5f : wrap.position + Vector3.up * 1.9f;
        foreach (var l in placeholder.GetComponentsInChildren<Light>(true))
        {
            if (l.transform.IsChildOf(wrap)) continue;
            l.transform.SetParent(headBone != null ? headBone : wrap, true);
            l.transform.position = eyePos + wrap.forward * 0.12f;
            l.transform.rotation = Quaternion.LookRotation(wrap.forward) * Quaternion.Euler(12f, 0f, 0f);
        }
        // placeholder zostaje (szkielet dla jumpscare'ów), ale jest niewidoczny
        foreach (var r in placeholder.GetComponentsInChildren<Renderer>(true))
            if (!r.transform.IsChildOf(wrap)) r.enabled = false;

        var bridge = stalkerRoot.gameObject.AddComponent<StalkerModelBridge>();
        bridge.ai = ai;
        bridge.driver = drv;
        entityReport = $"Model potwora: entity.fbx podpięty ({path}), wysokość {target:0.00} m, animacja MonsterAnimationDriver.";
    }

    // =====================================================================
    //  v5: AERO – Volumetric Fog and Mist (Mirza Beig)
    // =====================================================================

    static bool aero;
    static Material aeroMat;
    static string[] aeroDensityProps = new string[0];
    static float[] aeroDensityBase = new float[0];

    static List<Object> GetFeatures(Object rd)
    {
        var res = new List<Object>();
        if (rd == null) return res;
        var p = rd.GetType().GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.GetValue(rd) is System.Collections.IList l)
            foreach (var o in l) if (o is Object uo && uo != null) res.Add(uo);
        return res;
    }

    static object GetMember(object obj, string name)
    {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, bf | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(obj);
            var p = t.GetProperty(name, bf | BindingFlags.DeclaredOnly);
            if (p != null && p.CanRead) return p.GetValue(obj);
        }
        return null;
    }

    /// <summary>W kopii renderera podmienia KAŻDE pole typu Material, które wskazuje materiał z shaderem mgły,
    /// na naszą kopię. Feature oświetlenia AERO wpisuje do „swojego” materiału liczbę świateł i ambient –
    /// jeśli zostanie przy oryginale, nasza mgła nie widzi żadnej lampy. Zwraca liczbę podmian.</summary>
    public static int RetargetFogMaterial(Object rendererData, Material from, Material to)
    {
        if (rendererData == null || from == null || to == null) return 0;
        int n = 0;
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var f in GetFeatures(rendererData))
        {
            bool changed = false;
            for (var t = f.GetType(); t != null && t != typeof(ScriptableObject); t = t.BaseType)
                foreach (var fld in t.GetFields(bf | BindingFlags.DeclaredOnly))
                {
                    if (fld.FieldType != typeof(Material)) continue;
                    var m = fld.GetValue(f) as Material;
                    if (m == null || m == to || m.shader != from.shader) continue;
                    fld.SetValue(f, to);
                    changed = true;
                    n++;
                }
            if (changed) EditorUtility.SetDirty(f);
        }
        return n;
    }

    /// <summary>Podmienia w assecie renderera feature oświetlenia mgły z paczki AERO na nasz FogLightingFeature
    /// (ta sama kolejność na liście, materiał = nasza kopia mgły). Zwraca liczbę podmian.</summary>
    public static int ReplaceAeroLightingFeature(Object rendererData, Material fogMaterial)
    {
        if (rendererData == null) return 0;
        var so = new SerializedObject(rendererData);
        var list = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        if (list == null || map == null) return 0;
        int n = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            var old = list.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Rendering.Universal.ScriptableRendererFeature;
            if (old == null || old.GetType().Name != "FullscreenCustomLightingFeature") continue;
            var ours = ScriptableObject.CreateInstance<FogLightingFeature>();
            ours.name = "Fog Lighting (Poza Zasiegiem)";
            ours.material = fogMaterial;
            if (GetMember(old, "renderPassEvent") is UnityEngine.Rendering.Universal.RenderPassEvent ev) ours.renderPassEvent = ev;
            AssetDatabase.AddObjectToAsset(ours, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ours, out string _, out long id);
            list.GetArrayElementAtIndex(i).objectReferenceValue = ours;
            if (i < map.arraySize) map.GetArrayElementAtIndex(i).longValue = id;
            Object.DestroyImmediate(old, true);
            n++;
        }
        if (n > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }
        return n;
    }

    /// <summary>Wartości startowe mgły AERO dobrane do ciasnych, ciemnych wnętrz (porównanie klatek, 2026-11-21):
    /// bez morskiego „koloru cienia”, rzadsza mgła widoczna w stożkach lamp, krótszy zasięg (dalej i tak ciemno),
    /// mniejsze przyklejenie do podłogi, tańszy samocień od świateł dodatkowych.</summary>
    public static void TuneAeroMaterial(Material m)
    {
        if (m == null) return;
        void F(string n, float v) { if (m.HasProperty(n)) m.SetFloat(n, v); }
        F("_Enable_Shadow_Colour", 0f);
        if (m.HasProperty("_Colour")) m.SetColor("_Colour", new Color(0.92f, 0.9f, 0.86f, 1f));
        F("_Density", 0.12f); // 0.25 zalewało obraz poświatą przy włączonej latarce (rozproszenie 115° przy kamerze)
        F("_Max_Distance", 25f);
        F("_Height_Mask_Blend", 0.45f);
        F("_Ambient_Light_Scale", 0.15f);
        // Wydajność (pomiar 1080p, 2026-11-21): przy automatach z napojami mgła kosztowała 45 ms/klatkę.
        // 10 kroków, bez samocienia i bez cieni świateł w mgle (+ limit świateł w FogLightingFeature) → ~9 ms,
        // a stożki lamp wyglądają prawie tak samo (gładsze, mniej szumu).
        F("_Steps", 10f);
        F("_Enable_Additional_Light_Self_Shadow", 0f);
        F("_Enable_Main_Light_Self_Shadow", 0f);
        F("_ENABLE_ADDITIONAL_LIGHT_SHADOWS", 0f);
        m.DisableKeyword("_ENABLE_ADDITIONAL_LIGHT_SHADOWS");
    }

    /// <summary>Ocena renderera z mgłą: AERO, z feature'em oświetlenia mgły, wersja PC przed mobilną.</summary>
    public static int ScoreAeroRenderer(string path, bool hasLightingFeature)
    {
        string p = path.ToLowerInvariant();
        string file = System.IO.Path.GetFileName(p);
        return 3 + (hasLightingFeature ? 2 : 0) + (p.Contains("aero") ? 2 : 0)
             + (file.StartsWith("pc") ? 1 : 0) - (file.Contains("mobile") ? 1 : 0);
    }

    /// <summary>Assety URP, których gra faktycznie używa (Graphics + poziomy jakości). Kopie w cudzych paczkach
    /// (np. ustawienia przykładowe AERO) nie są ruszane.</summary>
    public static List<RenderPipelineAsset> UsedPipelineAssets()
    {
        var res = new List<RenderPipelineAsset>();
        void Add(RenderPipelineAsset a)
        {
            if (a == null || res.Contains(a)) return;
            if (AssetDatabase.GetAssetPath(a).StartsWith("Assets/Mirza")) return;
            res.Add(a);
        }
        Add(GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++) Add(QualitySettings.GetRenderPipelineAssetAt(i));
        return res;
    }

    static string SetupAero()
    {
        aero = false;
        aeroMat = null;
        aeroDensityProps = new string[0];
        aeroDensityBase = new float[0];
        if (!urp) return "AERO: pominięte (projekt bez URP).";

        Object best = null;
        Material bestMat = null;
        int bestScore = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(GenDir)) continue;
            var rd = AssetDatabase.LoadAssetAtPath<Object>(path);
            Material fm = null;
            bool lighting = false;
            foreach (var f in GetFeatures(rd))
            {
                string tn = f.GetType().Name;
                if (tn.Contains("FullScreenPass"))
                {
                    var m = GetMember(f, "passMaterial") as Material;
                    if (m != null && ((m.shader != null && m.shader.name.ToLowerInvariant().Contains("fog")) || m.name.ToLowerInvariant().Contains("fog"))) fm = m;
                }
                if (tn.ToLowerInvariant().Contains("lighting")) lighting = true;
            }
            if (fm == null || !AeroFog.IsMaterialUsable(fm)) continue;
            int score = ScoreAeroRenderer(path, lighting);
            if (score > bestScore) { bestScore = score; best = rd; bestMat = fm; }
        }
        if (best == null) return "AERO: nie znaleziono w projekcie – zostaje mgła z cząsteczek.";

        string dst = GenDir + "/" + AeroFog.RendererName + ".asset";
        var copy = AssetDatabase.LoadAssetAtPath<Object>(dst);
        if (copy == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(best), dst)) return "AERO: nie udało się skopiować renderera.";
            AssetDatabase.ImportAsset(dst);
            copy = AssetDatabase.LoadAssetAtPath<Object>(dst);
        }
        string mdst = GenDir + "/M_PZ_AeroFog.mat";
        aeroMat = AssetDatabase.LoadAssetAtPath<Material>(mdst);
        if (aeroMat == null)
        {
            string msrc = AssetDatabase.GetAssetPath(bestMat);
            if (!string.IsNullOrEmpty(msrc) && msrc.EndsWith(".mat") && AssetDatabase.CopyAsset(msrc, mdst))
            {
                aeroMat = AssetDatabase.LoadAssetAtPath<Material>(mdst);
                // wartości startowe tylko dla świeżej kopii – ręczne strojenie M_PZ_AeroFog.mat przetrwa przebudowę
                TuneAeroMaterial(aeroMat);
                EditorUtility.SetDirty(aeroMat);
            }
            if (aeroMat == null) aeroMat = bestMat;
        }
        RetargetFogMaterial(copy, bestMat, aeroMat);
        // skrypt oświetlenia mgły z paczki AERO → nasz odpowiednik (bez AERO w projekcie nie ma „missing feature”)
        ReplaceAeroLightingFeature(copy, aeroMat);
        EditorUtility.SetDirty(copy);
        // oryginalny renderer AERO niesie porzucone podobiekty ze skryptami, których nie ma nawet w paczce
        // (stare wersje feature'ów) – w repozytorium nie mogą zostać, bo wskazują też na materiały AERO
        AssetDatabase.SaveAssets();
        AssetDatabase.RemoveScriptableObjectsWithMissingScript(dst);

        // dopisz renderer z mgłą do każdego assetu URP (kamera przełącza się na niego w ustawieniach)
        int added = 0;
        foreach (Object a in UsedPipelineAssets())
        {
            var so = new SerializedObject(a);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray) continue;
            bool has = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var r = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (r == copy) has = true;
                else if (r == null && i > 0) { list.GetArrayElementAtIndex(i).objectReferenceValue = null; list.DeleteArrayElementAtIndex(i); }
            }
            if (!has)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = copy;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            added++;
        }

        // właściwości gęstości (suwak w ustawieniach)
        var names = new List<string>();
        var vals = new List<float>();
        var all = new List<string>();
        if (aeroMat != null && aeroMat.shader != null)
        {
            var sh = aeroMat.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i);
                var t = sh.GetPropertyType(i);
                all.Add(n);
                if ((t == ShaderPropertyType.Float || t == ShaderPropertyType.Range) && n.ToLowerInvariant().Contains("density"))
                {
                    names.Add(n);
                    vals.Add(aeroMat.GetFloat(n));
                }
            }
        }
        aeroDensityProps = names.ToArray();
        aeroDensityBase = vals.ToArray();
        aero = true;
        Debug.Log("[Poza Zasięgiem] AERO – właściwości materiału mgły: " + string.Join(", ", all));
        return $"AERO: OK – renderer „{best.name}” skopiowany jako {AeroFog.RendererName} ({added} assety URP). Gęstość: {(names.Count > 0 ? string.Join(", ", names) : "brak suwaka")}.";
    }

    // =====================================================================
    //  v5: OCCLUSION CULLING
    // =====================================================================

    static string BakeOcclusion()
    {
        StaticOcclusionCulling.smallestOccluder = 1.5f;
        StaticOcclusionCulling.smallestHole = 0.2f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool ok = StaticOcclusionCulling.Compute();
        return ok
            ? $"Occlusion Culling: wypalony w {sw.Elapsed.TotalSeconds:0} s – karta rysuje tylko to, co widać."
            : "Occlusion Culling: nie udało się wypalić (Window > Rendering > Occlusion Culling > Bake).";
    }

    static void MarkStatic(Transform root, bool occluders)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponentInParent<HangingSway>() != null) continue;
            if (t.GetComponent<ParticleSystem>() != null) continue;
            if (t.GetComponentInParent<SpinLight>() != null || t.GetComponentInParent<IdleSway>() != null) continue;
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            // tylko nieprzezroczyste bryły zasłaniają (snopy światła, szkło i paski PCV – nie)
            var r = t.GetComponent<Renderer>();
            bool transparent = r != null && r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 2500;
            bool fx = t.GetComponentInParent<VolumetricFx>() != null;
            if (occluders && r != null && !transparent && !fx) flags |= StaticEditorFlags.OccluderStatic;
            if (fx) flags = StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    static void SetupRendering()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.055f, 0.06f, 0.07f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.015f, 0.016f, 0.02f);
    }

    static void AddSceneToBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath))
        {
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // =====================================================================
    //  MATERIAŁY I TEKSTURY PROCEDURALNE
    // =====================================================================

    static Shader LitShader()
    {
        Shader s = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    static Material Mat(string name, Color c, Texture2D tex, Vector2 tiling, Color? emission = null, float smooth = 0.2f, float metallic = 0f)
    {
        string path = $"{GenDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(LitShader());
        else m.shader = LitShader();

        m.color = c;
        m.mainTexture = tex;
        m.mainTextureScale = tiling;
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    static Vector2 Tile(float wMeters, float hMeters) => new Vector2(1f / wMeters, 1f / hMeters);

    static float Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>Szum wartości zapętlający się z okresem 'period' (tekstury bez szwów).</summary>
    static float PN(float u, float v, int period)
    {
        int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
        float fx = u - x0, fy = v - y0;
        float a = Hash(Mod(x0, period), Mod(y0, period)), b = Hash(Mod(x0 + 1, period), Mod(y0, period));
        float c = Hash(Mod(x0, period), Mod(y0 + 1, period)), d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period));
        float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
    }

    static float Fbm(int x, int y, int w, int h, int freq)
    {
        float u = (float)x / w, v = (float)y / h;
        return PN(u * freq, v * freq, freq) * 0.55f + PN(u * freq * 2, v * freq * 2, freq * 2) * 0.3f + PN(u * freq * 4, v * freq * 4, freq * 4) * 0.15f;
    }

    static Texture2D Tex(string name, int w, int h, System.Func<int, int, Color> f, TextureWrapMode wrap = TextureWrapMode.Repeat)
    {
        string path = $"{GenDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
        t.SetPixels(px);
        t.Apply(true);
        t.wrapMode = wrap;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 4;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

    static void CreateMaterials()
    {
        // ---------- ŚCIANY ----------
        var tWallpaper = Tex("T_Wallpaper_v2", 128, 128, (x, y) =>
        {
            Color c = new Color(0.78f, 0.72f, 0.44f);
            if ((x / 8) % 2 == 0) c = Mul(c, 0.94f);
            c = Mul(c, 0.8f + 0.28f * Fbm(x, y, 128, 128, 4));
            if (Fbm(x, y, 128, 128, 2) > 0.62f) c = new Color(c.r * 0.85f, c.g * 0.78f, c.b * 0.6f);
            return c;
        });
        var tKitchen = Tex("T_KitchenTile_v2", 256, 256, (x, y) =>
        {
            float grime = 0.75f + 0.3f * Fbm(x, y, 256, 256, 4);
            if (x % 32 < 2 || y % 32 < 2) return Mul(new Color(0.3f, 0.29f, 0.26f), grime);
            return Mul(new Color(0.86f, 0.88f, 0.85f), (0.92f + 0.08f * Hash(x / 32, y / 32)) * grime);
        });
        var tFreezer = Tex("T_FreezerPanel_v2", 128, 128, (x, y) =>
        {
            Color c = new Color(0.66f, 0.71f, 0.75f);
            if (x % 64 < 2 || y < 2) c = new Color(0.3f, 0.32f, 0.35f);
            else if (x % 64 >= 6 && x % 64 <= 8 && y % 16 >= 4 && y % 16 <= 6) c = Mul(c, 0.6f);
            c = Mul(c, 0.85f + 0.2f * Fbm(x, y, 128, 128, 4));
            return Color.Lerp(c, new Color(0.88f, 0.93f, 1f), Fbm(x, y, 128, 128, 8) * 0.3f);
        });
        var tBlock = Tex("T_ConcreteBlock_v2", 256, 256, (x, y) =>
        {
            int row = y / 64;
            int bx = (x + (row % 2) * 64) % 256;
            if (y % 64 < 3 || bx % 128 < 3) return new Color(0.33f, 0.32f, 0.3f);
            float id = Hash(bx / 128 + row * 7, row);
            return Mul(new Color(0.5f, 0.5f, 0.48f), (0.85f + 0.15f * id) * (0.78f + 0.32f * Fbm(x, y, 256, 256, 8)));
        });
        var tDining = Tex("T_DiningWall_v2", 128, 256, (x, y) =>
        {
            float n = Fbm(x, y, 128, 256, 4);
            if (y < 88)
            {
                if (x % 32 == 0) return new Color(0.1f, 0.06f, 0.04f);
                float grain = PN(x / 128f * 16f, y / 256f * 16f, 16);
                return Mul(new Color(0.32f, 0.18f, 0.1f), (0.8f + 0.25f * grain) * (0.8f + 0.25f * n));
            }
            if (y < 93) return new Color(0.12f, 0.07f, 0.04f);
            Color c = new Color(0.36f, 0.08f, 0.07f);
            if (x % 32 >= 14 && x % 32 <= 18) c = Mul(c, 1.07f);
            return Mul(c, 0.82f + 0.25f * n);
        });
        var tOffice = Tex("T_OfficeWall_v2", 128, 256, (x, y) =>
        {
            float n = Fbm(x, y, 128, 256, 4);
            if (y < 72) return Mul(new Color(0.22f, 0.27f, 0.24f), 0.82f + 0.25f * n);
            if (y < 76) return new Color(0.15f, 0.15f, 0.13f);
            return Mul(new Color(0.42f, 0.47f, 0.43f), 0.82f + 0.22f * n);
        });

        // ---------- PODŁOGI ----------
        var tCarpet = Tex("T_Carpet_v2", 128, 128, (x, y) =>
        {
            float k = 0.75f + 0.15f * Hash(x, y) + 0.25f * Fbm(x, y, 128, 128, 8);
            Color c = Mul(new Color(0.55f, 0.47f, 0.28f), k);
            if (Fbm(x, y, 128, 128, 2) > 0.6f) c = Mul(c, 0.6f);
            return c;
        });
        var tQuarry = Tex("T_QuarryTiles_v2", 256, 256, (x, y) =>
        {
            float grime = 0.78f + 0.3f * Fbm(x, y, 256, 256, 4);
            if (x % 32 < 2 || y % 32 < 2) return Mul(new Color(0.2f, 0.18f, 0.16f), grime);
            return Mul(new Color(0.48f, 0.18f, 0.12f), (0.85f + 0.15f * Hash(x / 32, y / 32)) * grime);
        });
        var tDiamond = Tex("T_DiamondPlate_v2", 128, 128, (x, y) =>
        {
            Color c = Mul(new Color(0.5f, 0.52f, 0.55f), 0.82f + 0.2f * Fbm(x, y, 128, 128, 4));
            int cx = x / 16, cy = y / 16;
            float u = x % 16 - 8f, v = y % 16 - 8f;
            float s = ((cx + cy) % 2 == 0) ? 1f : -1f;
            if (Mathf.Abs(u - v * s) < 1.5f && Mathf.Abs(u) < 6f) c = Mul(c, 1.3f);
            return c;
        });
        var tConcrete = Tex("T_Concrete_v2", 256, 256, (x, y) =>
        {
            float n = Fbm(x, y, 256, 256, 6);
            Color c = Mul(new Color(0.42f, 0.42f, 0.41f), 0.75f + 0.4f * n);
            if (Mathf.Abs(PN(x / 32f, y / 32f, 8) - 0.5f) < 0.012f) c = Mul(c, 0.55f);
            if (Fbm(x, y, 256, 256, 3) > 0.66f) c = Mul(c, 0.6f);
            return c;
        });
        var tChecker = Tex("T_Checker_v2", 256, 256, (x, y) =>
        {
            float grime = 0.8f + 0.25f * Fbm(x, y, 256, 256, 4);
            if (x % 64 < 2 || y % 64 < 2) return Mul(new Color(0.22f, 0.2f, 0.18f), grime);
            bool alt = ((x / 64 + y / 64) & 1) == 0;
            return Mul(alt ? new Color(0.8f, 0.76f, 0.64f) : new Color(0.42f, 0.12f, 0.09f), grime);
        });
        var tOfficeCarpet = Tex("T_OfficeCarpet_v2", 128, 128, (x, y) =>
        {
            Color c = Mul(new Color(0.2f, 0.22f, 0.26f), 0.82f + 0.2f * Hash(x, y) + 0.1f * Fbm(x, y, 128, 128, 8));
            if (Fbm(x, y, 128, 128, 2) > 0.63f) c = Mul(c, 0.7f);
            return c;
        });

        // ---------- SUFITY ----------
        var tDrop = Tex("T_DropCeiling_v2", 128, 128, (x, y) =>
        {
            if (x % 64 < 3 || y % 64 < 3) return new Color(0.6f, 0.6f, 0.57f);
            Color c = new Color(0.8f, 0.79f, 0.74f);
            if (Hash(x, y) > 0.93f) c = Mul(c, 0.8f);
            if (Fbm(x, y, 128, 128, 2) > 0.6f) c = new Color(c.r * 0.85f, c.g * 0.75f, c.b * 0.55f);
            return c;
        });
        var tDarkCeil = Tex("T_DarkCeiling_v2", 128, 128, (x, y) =>
        {
            if (x % 64 < 2) return new Color(0.08f, 0.08f, 0.085f);
            return Mul(new Color(0.18f, 0.18f, 0.19f), 0.7f + 0.5f * Fbm(x, y, 128, 128, 4));
        });

        // ---------- SPECJALNE ----------
        var tParticle = Tex("T_SoftParticle", 64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a);
        }, TextureWrapMode.Clamp);
        flashCookie = Tex("T_FlashlightCookie_v3", 128, 128, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
            if (r >= 0.98f) return Color.black;
            float v = (0.78f + 0.22f * (1f - Mathf.SmoothStep(0f, 0.45f, r))) * (1f - Mathf.SmoothStep(0.72f, 0.98f, r));
            v += 0.15f * Mathf.Exp(-(r - 0.62f) * (r - 0.62f) / 0.002f);
            v *= 0.93f + 0.07f * Hash(x, y);
            v = Mathf.Clamp01(v);
            return new Color(v, v, v, v);
        }, TextureWrapMode.Clamp);

        mWallpaper = Mat("M_Wall_Backrooms", Color.white, tWallpaper, Tile(1.5f, 1.5f), null, 0.1f);
        mKitchenTile = Mat("M_Wall_Kitchen", Color.white, tKitchen, Tile(1.2f, 1.2f), null, 0.55f);
        mFreezerPanel = Mat("M_Wall_Freezer", Color.white, tFreezer, Tile(1.2f, 1.2f), null, 0.6f, 0.3f);
        mConcreteBlock = Mat("M_Wall_Storage", Color.white, tBlock, Tile(1.6f, 1.6f), null, 0.08f);
        mDiningWall = Mat("M_Wall_Dining", Color.white, tDining, Tile(1f, 3.2f), null, 0.2f);
        mOfficeWall = Mat("M_Wall_Office", Color.white, tOffice, Tile(1f, 3.2f), null, 0.15f);

        mCarpet = Mat("M_Floor_Backrooms", Color.white, tCarpet, Tile(1f, 1f), null, 0.05f);
        mQuarry = Mat("M_Floor_Kitchen", Color.white, tQuarry, Tile(2f, 2f), null, 0.45f);
        mDiamond = Mat("M_Floor_Freezer", Color.white, tDiamond, Tile(0.6f, 0.6f), null, 0.6f, 0.6f);
        mConcrete = Mat("M_Floor_Storage", Color.white, tConcrete, Tile(3f, 3f), null, 0.15f);
        mChecker = Mat("M_Floor_Dining", Color.white, tChecker, Tile(2f, 2f), null, 0.5f);
        mOfficeCarpet = Mat("M_Floor_Office", Color.white, tOfficeCarpet, Tile(1f, 1f), null, 0.05f);

        mDropCeil = Mat("M_Ceiling_Drop", Color.white, tDrop, Tile(1.2f, 1.2f), null, 0.05f);
        mDarkCeil = Mat("M_Ceiling_Dark", Color.white, tDarkCeil, Tile(2f, 2f), null, 0.2f);

        mSteel = Mat("M_Steel", new Color(0.6f, 0.62f, 0.65f), null, Vector2.one, null, 0.7f, 0.7f);
        mSteelDark = Mat("M_SteelDark", new Color(0.25f, 0.26f, 0.28f), null, Vector2.one, null, 0.5f, 0.6f);
        mBlack = Mat("M_Black", new Color(0.02f, 0.02f, 0.02f), null, Vector2.one, null, 0.2f);
        mDark = Mat("M_Dark", new Color(0.08f, 0.08f, 0.08f), null, Vector2.one, null, 0.3f);
        mBox = Mat("M_Cardboard", new Color(0.55f, 0.42f, 0.28f), null, Vector2.one, null, 0.05f);
        mBoxFrost = Mat("M_CardboardFrost", new Color(0.75f, 0.8f, 0.85f), null, Vector2.one, null, 0.3f);
        mIce = Mat("M_Ice", new Color(0.6f, 0.78f, 0.9f), null, Vector2.one, null, 0.95f);
        mMeat = Mat("M_Meat", new Color(0.78f, 0.6f, 0.56f), null, Vector2.one, null, 0.6f);
        mFrost = Mat("M_Frost", new Color(0.85f, 0.9f, 0.95f), null, Vector2.one, null, 0.3f);
        mRackOrange = Mat("M_RackOrange", new Color(0.85f, 0.4f, 0.08f), null, Vector2.one, null, 0.35f, 0.2f);
        mWood = Mat("M_Wood", new Color(0.45f, 0.3f, 0.18f), null, Vector2.one, null, 0.15f);
        mWoodDark = Mat("M_WoodDark", new Color(0.22f, 0.14f, 0.09f), null, Vector2.one, null, 0.35f);
        mBarrelBlue = Mat("M_BarrelBlue", new Color(0.1f, 0.2f, 0.45f), null, Vector2.one, null, 0.5f, 0.3f);
        mBarrelRust = Mat("M_BarrelRust", new Color(0.4f, 0.18f, 0.08f), null, Vector2.one, null, 0.25f, 0.4f);
        mSafetyYellow = Mat("M_SafetyYellow", new Color(0.9f, 0.75f, 0.1f), null, Vector2.one, null, 0.3f);
        mTableTop = Mat("M_TableTop", new Color(0.85f, 0.8f, 0.7f), null, Vector2.one, null, 0.6f);
        mChairRed = Mat("M_ChairRed", new Color(0.55f, 0.08f, 0.06f), null, Vector2.one, null, 0.5f);
        mLeatherRed = Mat("M_LeatherRed", new Color(0.45f, 0.06f, 0.05f), null, Vector2.one, null, 0.7f);
        mRedPaint = Mat("M_RedPaint", new Color(0.65f, 0.07f, 0.05f), null, Vector2.one, null, 0.5f);
        mYellowPaint = Mat("M_YellowPaint", new Color(0.95f, 0.75f, 0.15f), null, Vector2.one, null, 0.4f);
        mOrange = Mat("M_Orange", new Color(0.95f, 0.45f, 0.05f), null, Vector2.one, null, 0.4f);
        mSodaPanel = Mat("M_SodaPanel", new Color(1f, 0.4f, 0.3f), null, Vector2.one, new Color(1f, 0.35f, 0.25f) * 1.8f, 0.8f);
        mMenuPanel = Mat("M_MenuPanel", new Color(1f, 0.85f, 0.6f), null, Vector2.one, new Color(1f, 0.8f, 0.5f) * 1.2f, 0.6f);
        mBeige = Mat("M_Beige", new Color(0.75f, 0.7f, 0.6f), null, Vector2.one, null, 0.3f);
        mCRT = Mat("M_CRT", new Color(0.3f, 0.7f, 0.55f), null, Vector2.one, new Color(0.4f, 0.9f, 0.7f) * 1.0f, 0.9f);
        mGreyMetal = Mat("M_GreyMetal", new Color(0.45f, 0.47f, 0.5f), null, Vector2.one, null, 0.45f, 0.4f);
        mPaper = Mat("M_Paper", new Color(0.92f, 0.9f, 0.82f), null, Vector2.one, null, 0.05f);
        mLocker = Mat("M_Locker", new Color(0.25f, 0.32f, 0.38f), null, Vector2.one, null, 0.45f, 0.4f);
        mLockerWhite = Mat("M_LockerWhite", new Color(0.82f, 0.84f, 0.86f), null, Vector2.one, null, 0.5f, 0.3f);
        mLockerGreen = Mat("M_LockerGreen", new Color(0.25f, 0.35f, 0.28f), null, Vector2.one, null, 0.4f, 0.3f);
        mPipe = Mat("M_Pipe", new Color(0.3f, 0.28f, 0.26f), null, Vector2.one, null, 0.5f, 0.6f);
        mGrease = Mat("M_Grease", new Color(0.05f, 0.04f, 0.02f), null, Vector2.one, null, 0.95f);
        mWetStain = Mat("M_WetStain", new Color(0.18f, 0.15f, 0.08f), null, Vector2.one, null, 0.85f);
        mOilStain = Mat("M_OilStain", new Color(0.05f, 0.05f, 0.05f), null, Vector2.one, null, 0.9f);
        mStrip = Mat("M_FreezerStrip", new Color(0.7f, 0.78f, 0.82f), null, Vector2.one, null, 0.8f);
        mVentMetal = Mat("M_VentMetal", new Color(0.35f, 0.36f, 0.38f), null, Vector2.one, null, 0.45f, 0.6f);
        mExit = Mat("M_ExitSign", new Color(0.1f, 0.8f, 0.3f), null, Vector2.one, new Color(0.1f, 1f, 0.3f) * 2f, 0.5f);
        mEmergency = Mat("M_EmergencyLight", new Color(1f, 0.1f, 0.05f), null, Vector2.one, new Color(1f, 0.05f, 0.02f) * 2.5f, 0.5f);
        mLampOff = Mat("M_LampBroken", new Color(0.25f, 0.25f, 0.24f), null, Vector2.one, null, 0.3f);
        mEmber = Mat("M_GrillEmber", new Color(0.3f, 0.08f, 0.02f), null, Vector2.one, new Color(1f, 0.3f, 0.05f) * 0.6f, 0.3f);
        mRadioPanel = Mat("M_RadioPanel", new Color(1f, 0.6f, 0.2f), null, Vector2.one, new Color(1f, 0.55f, 0.15f) * 1.2f, 0.5f);

        mJacket = Mat("M_PlayerJacket", new Color(0.12f, 0.13f, 0.15f), null, Vector2.one, null, 0.25f);
        mPants = Mat("M_PlayerPants", new Color(0.1f, 0.1f, 0.12f), null, Vector2.one, null, 0.2f);
        mShoe = Mat("M_PlayerShoe", new Color(0.05f, 0.05f, 0.05f), null, Vector2.one, null, 0.3f);
        mSkin = Mat("M_PlayerSkin", new Color(0.85f, 0.68f, 0.58f), null, Vector2.one, null, 0.3f);
        mFlashBody = Mat("M_FlashlightBody", new Color(0.08f, 0.08f, 0.08f), null, Vector2.one, null, 0.6f, 0.7f);
        mLens = Mat("M_FlashlightLens", new Color(1f, 0.97f, 0.85f), null, Vector2.one, new Color(1f, 0.97f, 0.85f) * 3f, 0.9f);
        mTrackerBody = Mat("M_TrackerBody", new Color(0.15f, 0.17f, 0.15f), null, Vector2.one, null, 0.4f, 0.3f);
        mTrackerScreen = Mat("M_TrackerScreen", new Color(0.2f, 0.9f, 0.4f), null, Vector2.one, new Color(0.2f, 1f, 0.4f) * 1.5f, 0.9f);

        mCreatureSkin = Mat("M_CreatureSkin", new Color(0.05f, 0.045f, 0.045f), null, Vector2.one, null, 0.85f);
        mUniform = Mat("M_CreatureUniform", new Color(0.32f, 0.05f, 0.04f), null, Vector2.one, null, 0.2f);
        mUniformDark = Mat("M_CreatureUniformDark", new Color(0.12f, 0.03f, 0.03f), null, Vector2.one, null, 0.2f);
        mApron = Mat("M_CreatureApron", new Color(0.55f, 0.5f, 0.42f), null, Vector2.one, null, 0.1f);
        mClaw = Mat("M_CreatureClaw", new Color(0.12f, 0.12f, 0.1f), null, Vector2.one, null, 0.9f, 0.3f);
        mCreatureEye = Mat("M_CreatureEye", Color.red, null, Vector2.one, new Color(3f, 0.15f, 0.05f), 0.9f);

        string pPath = GenDir + "/M_Particle.mat";
        mParticle = AssetDatabase.LoadAssetAtPath<Material>(pPath);
        if (mParticle == null)
        {
            var sh = Shader.Find("Sprites/Default");
            mParticle = new Material(sh != null ? sh : LitShader()) { mainTexture = tParticle };
            AssetDatabase.CreateAsset(mParticle, pPath);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Przezroczysty materiał (paski PCV w chłodni).</summary>
    static void MakeTransparent(Material m)
    {
        if (m == null) return;
        if (urp)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }

    static void CreateExtraMaterials()
    {
        var tPaper = Tex("T_NotePaper_v3", 128, 180, (x, y) =>
        {
            Color c = new Color(0.9f, 0.87f, 0.76f) * (0.92f + 0.08f * Fbm(x, y, 128, 180, 4));
            if (y % 11 == 0 && y < 160) c = Color.Lerp(c, new Color(0.45f, 0.55f, 0.75f), 0.35f);
            if (x == 16) c = Color.Lerp(c, new Color(0.8f, 0.3f, 0.3f), 0.35f);
            if (Fbm(x, y, 128, 180, 2) > 0.64f) c = new Color(c.r * 0.9f, c.g * 0.82f, c.b * 0.62f);
            c.a = 1f;
            return c;
        });
        var tRust = Tex("T_PipeRust_v3", 128, 128, (x, y) =>
        {
            float n = Fbm(x, y, 128, 128, 8);
            Color baseC = new Color(0.27f, 0.24f, 0.22f);
            Color rust = new Color(0.36f, 0.17f, 0.08f);
            Color c = Color.Lerp(baseC, rust, Mathf.SmoothStep(0.45f, 0.7f, n));
            c *= 0.8f + 0.3f * Fbm(x, y, 128, 128, 4);
            c.a = 1f;
            return c;
        });

        mNotePaper = Mat("M_NotePaper", Color.white, tPaper, new Vector2(1f / 0.25f, 1f / 0.35f), new Color(0.55f, 0.53f, 0.47f), 0.05f);
        mStripClear = Mat("M_FreezerStripClear", new Color(0.75f, 0.85f, 0.9f, 0.42f), null, Vector2.one, null, 0.85f);
        MakeTransparent(mStripClear);
        mPipeBig = Mat("M_PipeBig", Color.white, tRust, new Vector2(2f, 1f), null, 0.4f, 0.55f);
        mGlassDark = Mat("M_GlassDark", new Color(0.05f, 0.08f, 0.1f), null, Vector2.one, null, 0.95f);
        mCamBody = Mat("M_CamcorderBody", new Color(0.12f, 0.12f, 0.13f), null, Vector2.one, null, 0.5f, 0.2f);
        mCamScreen = Mat("M_CamcorderScreen", new Color(0.2f, 0.35f, 0.8f), null, Vector2.one, new Color(0.3f, 0.5f, 1f) * 0.8f, 0.9f);
        mTrackerRT = Mat("M_TrackerScreenRuntime", Color.white, null, Vector2.one, new Color(0.1f, 0.4f, 0.15f), 0.9f);
        mDrool = Mat("M_Drool", new Color(0.8f, 0.85f, 0.7f, 0.85f), null, Vector2.one, null, 0.95f);
        var sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            string path = GenDir + "/M_DroolParticle.mat";
            var dm = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (dm == null)
            {
                dm = new Material(sh) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/T_SoftParticle.asset") };
                AssetDatabase.CreateAsset(dm, path);
            }
            mDrool = dm;
        }
        AssetDatabase.SaveAssets();
    }

    static void CreateStyles()
    {
        CreateExtraMaterials();
        CreateV4Materials();
        CreateV5Materials();
        styles = new Dictionary<Zone, Style>
        {
            [Zone.Backrooms] = new Style { wall = mWallpaper, floor = mCarpet, ceil = mDropCeil, light = new Color(1f, 0.9f, 0.62f), intensity = 1f, lightChance = 0.55f, flicker = 0.35f, anomaly = 0.6f, shadowChance = 0.6f, fixture = 0, pedestal = mBox },
            [Zone.Storage] = new Style { wall = mConcreteBlock, floor = mConcrete, ceil = mDarkCeil, light = new Color(1f, 0.62f, 0.3f), intensity = 1.1f, lightChance = 0.55f, flicker = 0.15f, anomaly = 0.15f, shadowChance = 0.7f, fixture = 2, pedestal = mBox },
            [Zone.Kitchen] = new Style { wall = mKitchenTile, floor = mQuarry, ceil = mDarkCeil, light = new Color(0.85f, 1f, 0.92f), intensity = 1f, lightChance = 0.7f, flicker = 0.2f, anomaly = 0.2f, shadowChance = 0.6f, fixture = 1, pedestal = mSteel },
            [Zone.Freezer] = new Style { wall = mFreezerPanel, floor = mDiamond, ceil = mFreezerPanel, light = new Color(0.55f, 0.75f, 1f), intensity = 0.95f, lightChance = 0.65f, flicker = 0.15f, anomaly = 0.15f, shadowChance = 0.6f, fixture = 1, pedestal = mSteel },
            [Zone.Dining] = new Style { wall = mDiningWall, floor = mChecker, ceil = mDropCeil, light = new Color(1f, 0.72f, 0.48f), intensity = 0.95f, lightChance = 0.5f, flicker = 0.25f, anomaly = 0.35f, shadowChance = 0.6f, fixture = 3, pedestal = mTableTop },
            [Zone.Office] = new Style { wall = mOfficeWall, floor = mOfficeCarpet, ceil = mDropCeil, light = new Color(0.8f, 0.95f, 0.82f), intensity = 0.85f, lightChance = 0.55f, flicker = 0.4f, anomaly = 0.2f, shadowChance = 0.5f, fixture = 0, pedestal = mGreyMetal },
        };
        // v4: każda lampa rzuca cień (shadowChance nieużywane)
        foreach (var kv in styles)
            kv.Value.lamp = Mat("M_Lamp_" + kv.Key, Color.white * 0.9f, null, Vector2.one, kv.Value.light * 2.4f, 0.1f);
        kitchenProps = null; // odśwież listy (nowe materiały)
    }

    // =====================================================================
    //  NOTATKI (fabuła)
    // =====================================================================

    const string StartNote =
        "Dzień 34.\n\nJeśli ktoś to czyta – NIE BIEGAJ. To coś słyszy kroki.\n" +
        "Kucanie (Ctrl) jest ciche. Latarka (F) pomaga, ale widać cię z daleka – i nie świeć NA NIEGO.\n\n" +
        "Szafki ratują życie (E). Jeśli zacznie węszyć przy twojej – wstrzymaj oddech (Spacja).\n" +
        "Ale jeśli zobaczy, jak do niej wchodzisz...\n\n" +
        "Pod sufitem biegną grube rury. Ono w nich chodzi – słychać stukanie, a rury drżą.\n" +
        "Twoja kamera wciąż działa. Ma noktowizję (PPM, potem F). Ono jej nie widzi.\n\n" +
        "Zostawiłem wykrywacz ruchu niedaleko stąd. Radiostacja w biurze kierownika jeszcze działa.\n" +
        "Tu nie ma zasięgu. Tylko ona może nas stąd wyciągnąć.\n\n- K.";

    const string StartNoteEn =
        "Day 34.\n\nIf anyone reads this – DON'T RUN. That thing hears footsteps.\n" +
        "Crouching (Ctrl) is quiet. The flashlight (F) helps, but it can see you from far away – and don't shine it AT IT.\n\n" +
        "Lockers save lives (E). If it starts sniffing at yours – hold your breath (Space).\n" +
        "But if it sees you getting in...\n\n" +
        "Thick pipes run under the ceiling. It moves inside them – you can hear knocking, and the pipes shake.\n" +
        "Your camera still works. It has night vision (RMB, then F). It can't see the camera's light.\n\n" +
        "I left a motion tracker not far from here. The radio in the manager's office still works.\n" +
        "There's no signal down here. Only the radio can get us out.\n\n- K.";

    const string KitchenNoteEn =
        "SHIFT SCHEDULE\n\nMonday: Kate, Tom, Employee\nMonday: Kate, Tom, Employee\n" +
        "Monday: Kate, Employee\nMonday: Employee\nMonday: Employee\n\n" +
        "(written in pen)\nWho is Employee? Nobody hired him.\n" +
        "The fryer is too big. Yesterday it was normal. The extinguisher hangs by the grill.";

    const string FreezerNoteEn =
        "To all shifts:\n\nSomething walks in the pipes under the ceiling. First you hear knocking,\n" +
        "the pipes shake, dust falls from the joints. When the dust falls – DON'T MOVE.\n\n" +
        "Sometimes it waits above a hatch. You can hear a quiet purring, and the pipe drips.\n" +
        "DON'T WALK UNDERNEATH.\n\n" +
        "The extinguisher scares it off. I saw it flee into a pipe.\n" +
        "But it comes back. It always comes back to where it last heard people.\n\n– shift manager";

    const string DiningNoteEn =
        "CUSTOMER SURVEY\n\nHow would you rate your visit?   1/5\n\n" +
        "Comments: I can't find the exit. The chairs are too big. The tables hang from the ceiling.\n" +
        "Someone in our cap has been standing behind the counter for a week and doesn't move.\n" +
        "When I turn around – he's standing closer.";

    const string OfficeNoteEn =
        "TRANSCRIPT – 108.7 MHz (fragment)\n\n\"...if anyone can hear this, this is the Agency.\n" +
        "This zone is OUT OF RANGE of our networks. The only channel is the radio in the manager's office.\n" +
        "Required: antenna, transmitter tube, power supply.\n" +
        "Send the signal and we will come. Don't trust anything that wears a uniform...\"\n\n[static]";

    const string KitchenNote =
        "GRAFIK ZMIAN\n\nPoniedziałek: Kasia, Tomek, Pracownik\nPoniedziałek: Kasia, Tomek, Pracownik\n" +
        "Poniedziałek: Kasia, Pracownik\nPoniedziałek: Pracownik\nPoniedziałek: Pracownik\n\n" +
        "(dopisane długopisem)\nKto to jest Pracownik? Nikt go nie zatrudniał.\n" +
        "Frytownica jest za duża. Wczoraj była normalna. Gaśnica wisi przy grillu.";

    const string FreezerNote =
        "Do wszystkich zmian:\n\nCoś chodzi w rurach pod sufitem. Najpierw słychać stukanie,\n" +
        "rury drżą, z połączeń sypie się kurz. Kiedy kurz leci – NIE RUSZAJ SIĘ.\n\n" +
        "Czasem czeka nad włazem. Słychać wtedy ciche mruczenie i z rury kapie.\n" +
        "NIE PRZECHODŹ POD SPODEM.\n\n" +
        "Gaśnica je odstrasza. Widziałem, jak uciekło do rury.\n" +
        "Ale wraca. Zawsze wraca tam, gdzie ostatnio słyszało ludzi.\n\n– kierownik zmiany";

    const string DiningNote =
        "ANKIETA KLIENTA\n\nJak oceniasz swoją wizytę?   1/5\n\n" +
        "Uwagi: Nie mogę znaleźć wyjścia. Krzesła są za duże. Stoły wiszą pod sufitem.\n" +
        "Ktoś w naszej czapce stoi za ladą od tygodnia i się nie rusza.\n" +
        "Kiedy się odwracam – stoi bliżej.";

    const string OfficeNote =
        "TRANSKRYPCJA – 108.7 MHz (fragment)\n\n\"...jeśli ktoś to słyszy, tu Agencja.\n" +
        "Ta strefa jest POZA ZASIĘGIEM naszych sieci. Jedyny kanał to radiostacja w biurze kierownika.\n" +
        "Potrzebne: antena, lampa nadawcza, zasilacz.\n" +
        "Nadaj sygnał, a przyjdziemy. Nie ufaj niczemu, co nosi uniform...\"\n\n[szum]";
}
