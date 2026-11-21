using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Oświetlenie globalne (Adaptive Probe Volumes): które światła i obiekty biorą udział w wypalaniu,
/// przygotowanie sceny i wypalanie scenariuszy „Prad” / „Awaria”.
/// </summary>
public static class GiBaker
{
    // obiekty ruchome / sterowane skryptem – ich światło nie może być wypalone (odbicie zostałoby w miejscu)
    static readonly System.Type[] MovingOrScripted =
    {
        typeof(FlickerLight), typeof(SpinLight), typeof(HangingSway), typeof(IdleSway), typeof(SwingDoor),
        typeof(StripCurtain), typeof(PlayerController), typeof(StalkerAI), typeof(Pickup), typeof(Note), typeof(PipeHatch),
    };

    /// <summary>Światło jest Mixed (odbicia wypalone, bezpośrednie i cienie na żywo): stała lampa (kat. 1)
    /// albo świecący obiekt (kat. 2), nie wykluczone i nie pod niczym ruchomym ani fabularnym.</summary>
    public static bool ShouldBake(Light l)
    {
        if (l == null) return false;
        var lc = l.GetComponent<LightCategory>();
        if (lc == null || lc.excludeFromBake || (lc.category != 1 && lc.category != 2)) return false;
        return !UnderAny(l.transform, MovingOrScripted);
    }

    /// <summary>Każda mapa ma własny zestaw wypalania (i dane GI), żeby mapy się nie nadpisywały.</summary>
    public static string BakingSetPathFor(string sceneName) => "Assets/PozaZasiegiem/Generated/PZ_GI_BakingSet_" + sceneName + ".asset";
    /// <summary>Zestaw wypalania aktywnej sceny.</summary>
    public static string BakingSetPath => BakingSetPathFor(SceneManager.GetActiveScene().name);
    public const string LightingPath = "Assets/PozaZasiegiem/Generated/PZ_Lighting.lighting";
    const string VolumeName = "Adaptive Probe Volume (GI)";
    /// <summary>Mnożnik światła odbitego dla wypalanych lamp. Lampy to wąskie reflektory o krótkim zasięgu:
    /// przy 1× i 3× odbicie było niewidoczne, przy 8× pokój wypełnia ciepłe światło od podłogi bez przecieków
    /// przez ściany (porównanie klatek 2026-11-21, sam składnik GI: 1× 0.012 → 8× 0.018 luma).</summary>
    public const float BounceIntensity = 8f;

    /// <summary>Obiekt bierze udział w GI (odbija światło do sond): zwykła siatka, wszystkie materiały
    /// nieprzezroczyste, nic ruchomego ani efektów (snopy, mgła) w rodzicach.</summary>
    public static bool ContributesGI(Renderer r)
    {
        if (!(r is MeshRenderer)) return false;
        foreach (var m in r.sharedMaterials)
            if (m == null || m.renderQueue >= 2500) return false;
        if (UnderAny(r.transform, MovingOrScripted)) return false;
        if (r.GetComponentInParent<VolumetricFx>(true) != null || r.GetComponentInParent<GateController>(true) != null) return false;
        return true;
    }

    /// <summary>Przygotowuje zapisaną scenę do wypalania APV (dane wypalania usuwa – nowa mapa = stare dane nieważne).
    /// Zwraca linię raportu dla generatora.</summary>
    public static string PrepareScene(Scene scene)
    {
        // 1) URP: sondy APV, scenariusze i ich mieszanie
        foreach (var a in LevelBuilder.UsedPipelineAssets())
        {
            var so = new SerializedObject(a);
            SetEnumContaining(so, "m_LightProbeSystem", "ProbeVolume");
            SetBool(so, "m_SupportProbeVolumeScenarios", true);
            SetBool(so, "m_SupportProbeVolumeScenarioBlending", true);
            SetEnumContaining(so, "m_ProbeVolumeSHBands", "L1");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
        }

        // 2) Lighting Settings sceny: tylko wypalane GI (bez lightmap – obiekty biorą światło z sond)
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
        if (ls == null)
        {
            ls = new LightingSettings { name = "PZ_Lighting" };
            AssetDatabase.CreateAsset(ls, LightingPath);
        }
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        EditorUtility.SetDirty(ls);
        Lightmapping.SetLightingSettingsForScene(scene, ls);

        // 3) obiekty: statyczne nieprzezroczyste odbijają światło, wszystkie biorą je z sond
        int gi = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                bool on = ContributesGI(r);
                flags = on ? flags | StaticEditorFlags.ContributeGI : flags & ~StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                if (r is MeshRenderer mr) mr.receiveGI = ReceiveGI.LightProbes;
                if (on) gi++;
            }

        // 4) światła: Mixed = odbicia wypalone, bezpośrednie i cienie na żywo
        int mixed = 0, realtime = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                bool bake = ShouldBake(l);
                l.lightmapBakeType = bake ? LightmapBakeType.Mixed : LightmapBakeType.Realtime;
                if (bake) l.bounceIntensity = BounceIntensity;
                if (bake) mixed++; else realtime++;
            }

        // materiały świecące nie wysyłają światła do wypalania – ich światło daje źródło kategorii 2
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/PozaZasiegiem/Generated" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            ExcludeEmissionFromBake(m);
            EditorUtility.SetDirty(m);
        }

        // 5) jeden globalny Probe Volume na całą scenę
        foreach (var old in Object.FindObjectsByType<ProbeVolume>(FindObjectsInactive.Include))
            if (old.gameObject.scene == scene) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(VolumeName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ProbeVolume>().mode = ProbeVolume.Mode.Global;

        // 6) Baking Set ze scenariuszami „Prad” i „Awaria” (stare dane usuwamy razem z zestawem)
        string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
        DeleteBakingSet(BakingSetPathFor(scene.name));
        var set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
        set.name = "PZ_GI_BakingSet";
        AssetDatabase.CreateAsset(set, BakingSetPathFor(scene.name));
        var scenarios = new System.Collections.Generic.List<string>(set.lightingScenarios);
        if (scenarios.Count > 0 && !scenarios.Contains(GiScenario.Power)) set.RenameScenario(scenarios[0], GiScenario.Power);
        else set.TryAddScenario(GiScenario.Power);
        set.TryAddScenario(GiScenario.Outage);
        if (!string.IsNullOrEmpty(sceneGuid)) set.TryAddScene(sceneGuid);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        return $"GI: scena przygotowana ({mixed} świateł Mixed, {realtime} Realtime, {gi} obiektów w GI). " +
               "Uruchom Poza Zasiegiem → Wypal oswietlenie (GI).";
    }

    /// <summary>Materiał świecący nie wysyła światła do wypalania, ale NADAL świeci: flaga RealtimeEmissive
    /// (dotyczy tylko GI liczonego na żywo, które jest wyłączone), bez BakedEmissive.
    /// Uwaga: przy EmissiveIsBlack i przy None postprocesor URP przy kolejnym odświeżeniu assetów wyłącza _EMISSION –
    /// gasły kartki notatek, ekrany, znaki i żarówki. Tu też naprawiamy tak zepsute materiały.</summary>
    public static void ExcludeEmissionFromBake(Material m)
    {
        if (m == null) return;
        bool emissive = m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f;
        m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        if (emissive) m.EnableKeyword("_EMISSION");
    }

    /// <summary>W scenariuszu „Awaria” gaśnie wypalane (Mixed) światło zasilane z sieci, chyba że leży pod BlackoutImmune
    /// – dokładnie te same, które gasi Blackout w grze.</summary>
    public static bool OffInOutage(Light l)
    {
        if (l == null || l.lightmapBakeType != LightmapBakeType.Mixed) return false;
        var lc = l.GetComponent<LightCategory>();
        return lc != null && lc.powered && l.GetComponentInParent<BlackoutImmune>(true) == null;
    }

    // ---------------- wypalanie obu scenariuszy ----------------

    public static bool IsBaking { get; private set; }
    public static string LastReport { get; private set; } = "";
    /// <summary>Bez okna z raportem na końcu (wypalanie sterowane z zewnątrz).</summary>
    public static bool Quiet;

    enum Step { Power, Outage }
    static Step step;
    static bool started;
    static double t0, tPower, tOutage;
    static readonly System.Collections.Generic.List<(GameObject go, bool active)> savedLights = new System.Collections.Generic.List<(GameObject, bool)>();

    [MenuItem("Poza Zasiegiem/Wypal oswietlenie (GI)", false, 20)]
    public static void BakeBoth()
    {
        if (IsBaking) return;
        var scene = SceneManager.GetActiveScene();
        if (AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(BakingSetPath) == null)
        {
            Finish("GI: brak zestawu wypalania – najpierw zbuduj scenę (Poza Zasiegiem → Zbuduj scene testowa).", false);
            return;
        }
        EditorSceneManager.SaveScene(scene);
        savedLights.Clear();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var l in root.GetComponentsInChildren<Light>(true))
                if (l.lightmapBakeType == LightmapBakeType.Mixed) savedLights.Add((l.gameObject, l.gameObject.activeSelf));
        IsBaking = true;
        LastReport = "";
        // przeładowanie skryptów w trakcie zgubiłoby stan wypalania i zostawiło lampy „Awarii” wyłączone w scenie
        EditorApplication.LockReloadAssemblies();
        t0 = EditorApplication.timeSinceStartup;
        BeginStep(Step.Power);
        EditorApplication.update += Tick;
    }

    static void BeginStep(Step s)
    {
        step = s;
        started = false;
        foreach (var (go, _) in savedLights)
        {
            if (go == null) continue;
            var l = go.GetComponent<Light>();
            go.SetActive(s == Step.Power || !OffInOutage(l));
        }
        ProbeReferenceVolume.instance.lightingScenario = s == Step.Power ? GiScenario.Power : GiScenario.Outage;
    }

    static void Tick()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Poza Zasięgiem – wypalanie GI",
                step == Step.Power ? "Scenariusz „Prad” (lampy włączone)..." : "Scenariusz „Awaria” (zasilane lampy zgaszone)...",
                step == Step.Power ? 0.25f : 0.75f);
            if (!started)
            {
                started = AdaptiveProbeVolumes.BakeAsync();
                if (!started) { Fail("GI: nie udało się rozpocząć wypalania (APV)."); }
                return;
            }
            if (AdaptiveProbeVolumes.isRunning) return;
            if (step == Step.Power)
            {
                tPower = EditorApplication.timeSinceStartup - t0;
                BeginStep(Step.Outage);
                return;
            }
            tOutage = EditorApplication.timeSinceStartup - t0 - tPower;
            Restore();
            var set = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(BakingSetPath);
            bool ok = set != null && set.HasBakedData(GiScenario.Power) && set.HasBakedData(GiScenario.Outage);
            Finish(ok ? $"GI: wypalone – „{GiScenario.Power}” {tPower:0} s, „{GiScenario.Outage}” {tOutage:0} s. {DataSizeReport()}"
                      : "GI: wypalanie skończone, ale brak danych dla obu scenariuszy.", true);
        }
        catch (System.Exception e)
        {
            Fail("GI: błąd wypalania – " + e.Message);
        }
    }

    static void Restore()
    {
        foreach (var (go, active) in savedLights) if (go != null) go.SetActive(active);
        ProbeReferenceVolume.instance.lightingScenario = GiScenario.Power;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    static void Fail(string msg)
    {
        Restore();
        Finish(msg, true);
    }

    static void Finish(string msg, bool wasRunning)
    {
        if (wasRunning)
        {
            EditorApplication.update -= Tick;
            EditorApplication.UnlockReloadAssemblies();
        }
        EditorUtility.ClearProgressBar();
        IsBaking = false;
        LastReport = msg;
        Debug.Log("[Poza Zasięgiem] " + msg);
        if (!Quiet) EditorUtility.DisplayDialog("Poza Zasięgiem", msg, "OK");
    }

    /// <summary>Łączny rozmiar danych APV i największy plik (limit GitHuba: ostrzeżenie od 50 MB).</summary>
    static string DataSizeReport()
    {
        string dir = System.IO.Path.GetDirectoryName(BakingSetPath);
        long total = 0, biggest = 0; string biggestName = "";
        string baseName = System.IO.Path.GetFileNameWithoutExtension(BakingSetPath);
        foreach (var f in System.IO.Directory.GetFiles(dir, baseName + "*.bytes"))
        {
            long len = new System.IO.FileInfo(f).Length;
            total += len;
            if (len > biggest) { biggest = len; biggestName = System.IO.Path.GetFileName(f); }
        }
        return $"Dane: {total / 1048576f:0.0} MB, największy plik {biggestName} {biggest / 1048576f:0.0} MB.";
    }

    /// <summary>Usuwa zestaw wypalania i jego dane. APV zapisuje je OBOK zestawu jako „Nazwa.Cell*.bytes”
    /// i „Nazwa-Scenariusz.Cell*.bytes” (sprawdzone w Unity 6000.6).</summary>
    public static void DeleteBakingSet() => DeleteBakingSet(BakingSetPath);

    public static void DeleteBakingSet(string setPath)
    {
        string dir = System.IO.Path.GetDirectoryName(setPath).Replace('\\', '/');
        string baseName = System.IO.Path.GetFileNameWithoutExtension(setPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(setPath) != null) AssetDatabase.DeleteAsset(setPath);
        foreach (var f in System.IO.Directory.GetFiles(dir, baseName + "*.bytes"))
        {
            string name = System.IO.Path.GetFileName(f);
            if (name.StartsWith(baseName + ".") || name.StartsWith(baseName + "-"))
                AssetDatabase.DeleteAsset(dir + "/" + name);
        }
    }

    static void SetBool(SerializedObject so, string prop, bool v)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = v;
    }

    static void SetEnumContaining(SerializedObject so, string prop, string part)
    {
        var p = so.FindProperty(prop);
        if (p == null || p.propertyType != SerializedPropertyType.Enum) return;
        for (int i = 0; i < p.enumNames.Length; i++)
            if (p.enumNames[i].Contains(part)) { p.enumValueIndex = i; return; }
    }

    static bool UnderAny(Transform t, System.Type[] types)
    {
        foreach (var type in types)
            if (t.GetComponentInParent(type, true) != null) return true;
        return false;
    }
}
