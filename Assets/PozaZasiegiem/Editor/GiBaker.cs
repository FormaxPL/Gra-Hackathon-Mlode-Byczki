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

    public const string BakingSetPath = "Assets/PozaZasiegiem/Generated/PZ_GI_BakingSet.asset";
    public const string LightingPath = "Assets/PozaZasiegiem/Generated/PZ_Lighting.lighting";
    const string VolumeName = "Adaptive Probe Volume (GI)";

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
                if (bake) mixed++; else realtime++;
            }

        // materiały świecące nie wysyłają światła do wypalania – ich światło daje źródło kategorii 2
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/PozaZasiegiem/Generated" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.globalIlluminationFlags == MaterialGlobalIlluminationFlags.EmissiveIsBlack) continue;
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
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
        DeleteBakingSet();
        var set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
        set.name = "PZ_GI_BakingSet";
        AssetDatabase.CreateAsset(set, BakingSetPath);
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

    static void DeleteBakingSet()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(BakingSetPath) != null) AssetDatabase.DeleteAsset(BakingSetPath);
        // dane APV leżą w folderze o nazwie zestawu obok niego
        string dataDir = BakingSetPath.Substring(0, BakingSetPath.Length - ".asset".Length);
        if (AssetDatabase.IsValidFolder(dataDir)) AssetDatabase.DeleteAsset(dataDir);
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
