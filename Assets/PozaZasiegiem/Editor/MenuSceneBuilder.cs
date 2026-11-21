using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scena menu głównego: żółty korytarz Backrooms w kształcie litery L z kratką wentylacyjną,
/// świetlówkami (jedna miga), mgłą i potworem (kopia modelu z mapy A) sterowanym przez MenuStalkerDirector.
/// Budowana przez „Zbuduj wszystkie mapy i menu” (po mapach – potrzebuje materiałów i potwora z TestLevel_A).
/// </summary>
public static class MenuSceneBuilder
{
    const string ScenesDir = "Assets/PozaZasiegiem/Scenes";
    static string ScenePath => ScenesDir + "/" + MapPicker.MenuScene + ".unity";

    static Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>("Assets/PozaZasiegiem/Generated/" + n + ".mat");

    [MenuItem("Poza Zasiegiem/Zbuduj tylko menu", false, 3)]
    public static void Build()
    {
        // potwór z mapy A ma już podpięty model, materiały i animację – kopiujemy go
        var levelPath = ScenesDir + "/" + MapPicker.Maps[0] + ".unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject stalkerCopy = null;
        if (System.IO.File.Exists(levelPath))
        {
            var level = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Additive);
            foreach (var root in level.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Model (entity.fbx)" && stalkerCopy == null)
                    {
                        stalkerCopy = Object.Instantiate(t.gameObject);
                        stalkerCopy.name = "Potwór (menu)";
                        SceneManager.MoveGameObjectToScene(stalkerCopy, scene);
                    }
            EditorSceneManager.CloseScene(level, true);
        }
        SceneManager.SetActiveScene(scene);
        LevelBuilder.SetupRendering();

        var root0 = new GameObject("Korytarz menu").transform;
        Material wall = M("M_Wall_Backrooms"), floor = M("M_Floor_Backrooms"), ceil = M("M_Ceiling_Drop"),
                 lampMat = M("M_Lamp_Backrooms"), vent = M("M_VentMetal"), dark = M("M_Black");
        const float H = 2.8f;

        // korytarz główny: x -1.5..1.5, z -1..14; boczny w prawo: z 6.5..9.5, x 1.5..8 (róg blisko kamery)
        LevelBuilder.WorldBox("Podłoga", root0, new Vector3(0f, -0.05f, 6.5f), new Vector3(3f, 0.1f, 15f), floor);
        LevelBuilder.WorldBox("Podłoga", root0, new Vector3(4.75f, -0.05f, 8f), new Vector3(6.5f, 0.1f, 3f), floor);
        LevelBuilder.WorldBox("Sufit", root0, new Vector3(0f, H + 0.05f, 6.5f), new Vector3(3f, 0.1f, 15f), ceil);
        LevelBuilder.WorldBox("Sufit", root0, new Vector3(4.75f, H + 0.05f, 8f), new Vector3(6.5f, 0.1f, 3f), ceil);
        // lewa ściana z otworem wentylacyjnym (z 5.6..6.4, y 1.95..2.45)
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(-1.6f, H / 2f, 2.3f), new Vector3(0.2f, H, 6.6f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(-1.6f, H / 2f, 10.2f), new Vector3(0.2f, H, 7.6f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(-1.6f, 0.975f, 6f), new Vector3(0.2f, 1.95f, 0.8f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(-1.6f, 2.625f, 6f), new Vector3(0.2f, 0.35f, 0.8f), wall);
        // kanał za kratką (ciemny) i rama kratki
        LevelBuilder.WorldBox("Kanał wentylacji", root0, new Vector3(-2.3f, 1.7f, 6f), new Vector3(1.2f, 0.06f, 0.9f), vent);
        LevelBuilder.WorldBox("Kanał wentylacji", root0, new Vector3(-2.3f, 2.5f, 6f), new Vector3(1.2f, 0.06f, 0.9f), vent);
        LevelBuilder.WorldBox("Kanał wentylacji", root0, new Vector3(-2.3f, 2.1f, 5.55f), new Vector3(1.2f, 0.8f, 0.06f), vent);
        LevelBuilder.WorldBox("Kanał wentylacji", root0, new Vector3(-2.3f, 2.1f, 6.45f), new Vector3(1.2f, 0.8f, 0.06f), vent);
        LevelBuilder.WorldBox("Kanał wentylacji", root0, new Vector3(-2.95f, 2.1f, 6f), new Vector3(0.06f, 0.8f, 0.9f), dark ?? vent);
        foreach (float y in new[] { 1.95f, 2.45f }) LevelBuilder.WorldBox("Kratka", root0, new Vector3(-1.49f, y, 6f), new Vector3(0.04f, 0.04f, 0.84f), vent, false);
        foreach (float z in new[] { 5.6f, 6.4f }) LevelBuilder.WorldBox("Kratka", root0, new Vector3(-1.49f, 2.2f, z), new Vector3(0.04f, 0.54f, 0.04f), vent, false);
        // prawa ściana z wejściem w boczny korytarz, końce korytarzy
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(1.6f, H / 2f, 2.75f), new Vector3(0.2f, H, 7.5f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(1.6f, H / 2f, 11.75f), new Vector3(0.2f, H, 4.5f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(0f, H / 2f, 14.1f), new Vector3(3.4f, H, 0.2f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(0f, H / 2f, -1.1f), new Vector3(3.4f, H, 0.2f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(4.75f, H / 2f, 6.4f), new Vector3(6.5f, H, 0.2f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(4.75f, H / 2f, 9.6f), new Vector3(6.5f, H, 0.2f), wall);
        LevelBuilder.WorldBox("Ściana", root0, new Vector3(8.1f, H / 2f, 8f), new Vector3(0.2f, H, 3.4f), wall);

        // świetlówki
        var lamps = new System.Collections.Generic.List<Light>();
        Light Lamp(Vector3 p, bool shadows, bool flicker)
        {
            LevelBuilder.WorldBox("Świetlówka", root0, p + Vector3.up * 0.02f, new Vector3(0.6f, 0.04f, 1.2f), lampMat, false);
            var g = new GameObject("Lamp Light");
            g.transform.SetParent(root0, false);
            g.transform.position = p - Vector3.up * 0.05f;
            g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 140f; l.innerSpotAngle = 90f; l.range = 5f; l.intensity = 4.5f; // bez lampy nad kamerą – inaczej mgła przed obiektywem zalewa obraz
            l.color = new Color(1f, 0.95f, 0.78f);
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (flicker) g.AddComponent<FlickerLight>();
            lamps.Add(l);
            return l;
        }
        Lamp(new Vector3(0f, H - 0.02f, 5.5f), true, false);
        Lamp(new Vector3(0f, H - 0.02f, 9.5f), true, false);
        Lamp(new Vector3(0f, H - 0.02f, 13f), false, true);
        Lamp(new Vector3(5f, H - 0.02f, 8f), false, false);

        // kamera
        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(-0.3f, 1.55f, 0.1f);
        camGo.transform.rotation = Quaternion.Euler(4f, 8f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f; cam.fieldOfView = 62f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();
        LevelBuilder.SetupPostProcessing(camGo);

        // mgła AERO (jak w mapach) – bez AERO AeroFog.Available = false i kamera zostaje przy zwykłym rendererze
        var fogMat = M("M_PZ_AeroFog");
        if (fogMat != null)
        {
            var fog = new GameObject("Mgła AERO").AddComponent<AeroFog>();
            fog.fogMaterial = fogMat;
            fog.densityProperties = new[] { "_Density" };
            fog.densityBase = new[] { 0.06f }; // w krótkim korytarzu menu rzadziej niż w mapach (0.12)
        }

        new GameObject("Menu").AddComponent<MenuSystem>();

        // potwór i jego reżyser
        var dirGo = new GameObject("Reżyser potwora (menu)");
        var dir = dirGo.AddComponent<MenuStalkerDirector>();
        Transform P(string n, Vector3 pos, float yaw)
        {
            var t = new GameObject(n).transform;
            t.SetParent(dirGo.transform, false);
            t.position = pos; t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }
        dir.cornerHidden = P("Za rogiem", new Vector3(2.7f, 0f, 7.9f), -90f);
        dir.cornerPeek = P("Wychylony", new Vector3(1.85f, 0f, 7.8f), -90f);
        dir.ventInside = P("W kanale", new Vector3(-2.5f, 1.6f, 6f), 90f);
        dir.ventMouth = P("Wylot kratki", new Vector3(-1.2f, 1.6f, 6f), 90f);
        dir.ventFloor = P("Pod kratką", new Vector3(-0.8f, 0f, 6.2f), 60f);
        dir.walkEnd = P("Za róg", new Vector3(6.8f, 0f, 8f), 90f);
        dir.farSpot = P("W głębi", new Vector3(0f, 0f, 13.3f), 180f);
        dir.cam = cam;
        dir.lamps = lamps.ToArray();
        if (stalkerCopy != null)
        {
            stalkerCopy.transform.SetParent(null);
            stalkerCopy.transform.localScale = Vector3.one;
            dir.stalker = stalkerCopy.transform;
            dir.driver = stalkerCopy.GetComponent<MonsterAnimationDriver>();
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Poza Zasięgiem] Menu zbudowane: " + ScenePath + (stalkerCopy == null ? " (UWAGA: brak potwora – najpierw zbuduj mapę A)" : ""));
    }
}
