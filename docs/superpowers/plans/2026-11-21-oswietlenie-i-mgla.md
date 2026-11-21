# Oświetlenie globalne (APV) i mgła AERO: plan wdrożenia

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Cel:** Mgła wolumetryczna AERO oraz odbite światło wypalone do sond APV w dwóch scenariuszach („Prad” i „Awaria”), przełączanych przy awarii prądu, plus pozorne odbicie latarki.

**Architektura:**
- Generator (`LevelBuilder`) przygotowuje scenę do GI: URP z APV, Lighting Settings, flagi statyczne, światła `Mixed`/`Realtime`, Probe Volume, Baking Set.
- Osobne menu w nowym `GiBaker` (Editor) wypala oba scenariusze.
- W grze nowy `GiScenario` przełącza scenariusze na żądanie `Blackout`.
- `AeroFog` dostaje twardsze sprawdzenie dostępności, żeby projekt bez AERO działał.
- Nowy `FlashlightBounce` daje odbłysk latarki.

**Stack:** Unity 6000.6.10f1, URP 17.6, `UnityEngine.Rendering.AdaptiveProbeVolumes` (Editor), `ProbeReferenceVolume`, `ProbeVolumeBakingSet`, Unity Test Framework (EditMode), Unity CLI (`unity command …`) do sterowania otwartym edytorem.

**Spec:** `docs/superpowers/specs/2026-11-21-oswietlenie-i-mgla-design.md`

## Global Constraints

- Ścieżka projektu: `F:/Unity/Unity Projekty/Gra Hackathon Mlode Byczki`. Każde `unity command` dostaje `--project-path "<ta ścieżka>" --caller plugin --skill executing-plans`.
- Nazwy scenariuszy dokładnie: `"Prad"` i `"Awaria"`.
- Pozycja menu dokładnie: `Poza Zasiegiem/Wypal oswietlenie (GI)`.
- Kod gry zostaje w `Assembly-CSharp` (bez asmdef). Testy EditMode trafiają do `Assets/PozaZasiegiem/Editor/Tests/` (`Assembly-CSharp-Editor`).
- `Assets/Mirza` (AERO) **nigdy** nie trafia do gita (jest w `.gitignore`). Projekt musi się kompilować i uruchamiać bez niego.
- Kod gry odwołujący się do AERO robi to tylko przez refleksję, jak dziś `AeroFog`.
- Cienie lamp zostają liczone na żywo. Ustawienia cieni (Niskie/Średnie/Wysokie) i `LightCuller` działają jak dotąd.
- Awaria nie może być jaśniejsza niż dziś.
- Plik danych wypalania > 50 MB: **STOP**, zgłoś autorowi (Git LFS albo rzadsze sondy), nie commituj.
- FPS z mgłą AERO < ~60 na komputerze autora: obniż jakość mgły (kroki raymarchingu / materiał „Mobile”), nie wyłączaj jej.
- Generator po zmianach musi nadal budować scenę stałym ziarnem 12345 (`LevelBuilder.BuildFixed`).
- Komentarze w kodzie po polsku, jak w reszcie projektu.
- Przed `unity command script eval` po kompilacji: `unity status --until-ready --project-path "<ścieżka>"`.

## Review Focus

1. **Projekt bez AERO** (kolega nie zaimportował assetu): brak błędów w konsoli, brak czarnego ekranu, kamera na domyślnym rendererze, widać mgłę z cząsteczek. Test w zadaniu 3.
2. **Restart w trakcie awarii** (śmierć w pościgu → R): po przeładowaniu sceny scenariusz to `"Prad"`, nie `"Awaria"`. Test w zadaniu 7.
3. **Scena bez wypalonych danych** (świeżo przebudowana generatorem): `GiScenario.Set/Blend` nie rzucają wyjątków, gra działa z płaskim ambientem. Test w zadaniu 7.
4. **Światła fabularne** (czerwone w tunelu, przerywniki, migające, koguty): zostają `Realtime` i nie świecą odbiciem, gdy są zgaszone skryptem. Test w zadaniu 4.
5. **Latarka celująca w pustkę / niebo / bardzo blisko ściany**: odbłysk wyłączony albo nie wchodzi w ścianę. Test w zadaniu 8.

---

### Task 1: Punkt odniesienia: zdjęcia i FPS „przed”

**Files:**
- Create: `docs/superpowers/plans/gi-shots.json` (pozycje kamer)
- Create: `docs/superpowers/plans/gi-shots/before_*.png`
- Create: `docs/superpowers/plans/gi-baseline.md` (pomiary)

**Interfaces:**
- Produces: `gi-shots.json` jako lista `{ "name": string, "pos": [x,y,z], "euler": [x,y,z] }` z nazwami dokładnie: `lit_room`, `corridor_next_to_lit`, `vending_corner`, `stalker_under_lamp`, `fog_under_lamp`. Zadanie 9 robi zdjęcia „po” z tych samych pozycji.

- [ ] **Step 1:** W otwartej scenie `TestLevel` wybierz 5 miejsc z listy powyżej (np. przez `script eval`: najbliższa lampa kat. 1 od spawnu gracza, punkt 4–6 m obok w sąsiednim korytarzu, automat z napojami, lampa z potworem ustawionym pod nią w trybie Play). Zapisz pozy kamer do `gi-shots.json`.
- [ ] **Step 2:** W trybie Play (zatrzymane `StalkerAI`, `MenuSystem` i `StoryDirector` jak w dotychczasowych testach; stalker przestawiony pod lampę dla `stalker_under_lamp`) wyrenderuj kamerą tymczasową (`HideFlags.DontSave`) każde ujęcie do 960×540 PNG jako `before_<name>.png`. Zrób też `before_lit_room_blackout.png` po `Blackout.Instance.Begin(-1, false)`.
- [ ] **Step 3:** Zmierz FPS: w Play przy `lit_room` odczytaj `Time.frameCount` i `Time.realtimeSinceStartup` dwa razy w odstępie ~10 s. Zapisz średnie FPS do `gi-baseline.md` z adnotacją „edytor, nie build”.
- [ ] **Step 4:** Wyjdź z Play i upewnij się, że scena nie jest „dirty” (`SceneManager.GetActiveScene().isDirty == false`).
- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/plans/gi-shots.json docs/superpowers/plans/gi-shots docs/superpowers/plans/gi-baseline.md
git commit -m "GI: zdjecia i FPS przed zmianami"
```

---

### Task 2: Twarde sprawdzenie dostępności AERO

**Files:**
- Modify: `Assets/PozaZasiegiem/Scripts/AeroFog.cs`
- Modify: `Assets/PozaZasiegiem/Editor/LevelBuilder.cs` (`SetupAero`, ok. linia 3716)
- Test: `Assets/PozaZasiegiem/Editor/Tests/AeroFogTests.cs`

**Interfaces:**
- Produces:
  - `public static bool AeroFog.IsMaterialUsable(Material m)`
  - `public static bool AeroFog.HasMissingFeatures(Object rendererData)`
  - `AeroFog.Available` zwraca `true` tylko gdy: `Instance != null`, `IsMaterialUsable(Instance.fogMaterial)`, `FindRendererIndex() >= 0`, a renderer pod tym indeksem nie ma brakujących feature'ów.

- [ ] **Step 1: Napisz testy (najpierw sprawdź, że runner widzi testy z folderu Editor)**

```csharp
public class AeroFogTests
{
    [Test] public void NullMaterial_IsNotUsable() => Assert.IsFalse(AeroFog.IsMaterialUsable(null));
    [Test] public void ErrorShaderMaterial_IsNotUsable()
    {
        var m = new Material(Shader.Find("Hidden/InternalErrorShader"));
        Assert.IsFalse(AeroFog.IsMaterialUsable(m));
        Object.DestroyImmediate(m);
    }
    [Test] public void UrpLitMaterial_IsUsable()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Assert.IsTrue(AeroFog.IsMaterialUsable(m));
        Object.DestroyImmediate(m);
    }
    [Test] public void RendererWithNullFeature_HasMissingFeatures()
    {
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        rd.rendererFeatures.Add(null);
        Assert.IsTrue(AeroFog.HasMissingFeatures(rd));
        Object.DestroyImmediate(rd);
    }
    [Test] public void RendererWithoutFeatures_HasNoMissingFeatures()
    {
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        Assert.IsFalse(AeroFog.HasMissingFeatures(rd));
        Object.DestroyImmediate(rd);
    }
}
```

- [ ] **Step 2:** Uruchom `unity command test run --filter AeroFogTests` i sprawdź, że testy są wykryte i padają (brak metod). Jeśli runner nie wykrywa testów z `Assembly-CSharp-Editor`, dodaj do `Packages/manifest.json` `"testables": ["com.unity.test-framework"]` i powtórz. Nie twórz asmdef, bo kod gry jest w `Assembly-CSharp`.
- [ ] **Step 3:** Zaimplementuj obie metody i nowe `Available` w `AeroFog.cs`:
  - `IsMaterialUsable`: `m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader"`.
  - `HasMissingFeatures`: refleksja jak `LevelBuilder.GetFeatures`, czyli własność `rendererFeatures` zawiera element `null`.
  - Renderer pod indeksem pobierz z `m_RendererDataList` tak jak w `FindRendererIndex`.
  - `Apply` używa nowego `Available`.
- [ ] **Step 4:** W `LevelBuilder.SetupAero` pomiń kandydata, jeśli `!AeroFog.IsMaterialUsable(fm)`.
- [ ] **Step 5:** `unity recompile`, potem `unity command test run --filter AeroFogTests`. Oczekiwane: 5/5 PASS.
- [ ] **Step 6: Commit**

```bash
git add Assets/PozaZasiegiem/Scripts/AeroFog.cs Assets/PozaZasiegiem/Editor/LevelBuilder.cs Assets/PozaZasiegiem/Editor/Tests
git commit -m "AERO: mgla tylko gdy shader i renderer sa kompletne"
```

---

### Task 3: Włączenie AERO, strojenie i test bez AERO

**Files:**
- Modify (generowane): `Assets/PozaZasiegiem/Scenes/TestLevel.unity`, `Assets/PozaZasiegiem/Generated/*` (w tym nowe `PZ_AeroRenderer.asset`, `M_PZ_AeroFog.mat`), `Assets/Settings/*_RPAsset.asset`
- Modify (jeśli strojenie wymaga): `Assets/PozaZasiegiem/Editor/LevelBuilder.cs` (wartości mgły w `SetupAero` / tworzeniu `AeroFog`)
- Create: `docs/superpowers/plans/gi-aero-report.md`

**Interfaces:**
- Consumes: `AeroFog.Available` (Task 2).

- [ ] **Step 1:** Uruchom `LevelBuilder.BuildFixed()` przez `script eval`. W `Logs/Editor.log` raport musi zawierać `AERO: OK` i nazwę renderera AERO. Lista właściwości materiału mgły jest logowana: zapisz ją do `gi-aero-report.md`.
- [ ] **Step 2:** W Play (jak w Task 1) sprawdź `AeroFog.Available == true` i że `UniversalAdditionalCameraData` kamery gracza używa indeksu `AeroFog.FindRendererIndex()`. Zrób klatkę `fog_under_lamp`.
- [ ] **Step 3:** Stroj `M_PZ_AeroFog.mat` (gęstość, kolor, kroki raymarchingu) według specu:
  - widoczna pod lampami i w snopie latarki,
  - w awarii prawie niewidoczna.

  Wartości wpisz do generatora (żeby przebudowa ich nie gubiła), zmierz FPS jak w Task 1 i dopisz do raportu. Przy FPS < ~60 obniż jakość według Global Constraints.
- [ ] **Step 4: Test bez AERO (Review Focus 1):**
  1. Przenieś `Assets/Mirza` i `Assets/Mirza.meta` do katalogu tymczasowego poza projektem.
  2. Uruchom `unity recompile` i `unity status --until-ready`.
  3. Wejdź w Play.

  Oczekiwane:
  - zero nowych linii `error`/`Exception` w `Logs/Editor.log` od momentu przeniesienia (poza znanymi `SuperProxy`/`NoSubscription`),
  - `AeroFog.Available == false`,
  - kamera na rendererze 0,
  - obraz nie jest czarny (średnia jasność wyrenderowanej klatki > 0.02).

  Wyjdź z Play, przenieś folder z powrotem, `unity recompile`, sprawdź `AeroFog.Available == true` w Play. Wynik opisz w raporcie.
- [ ] **Step 5:** Jeśli krok 4 pokazał błędy z kopii renderera z brakującym skryptem, popraw w kodzie: renderer AERO dopisany do listy URP nie może psuć domyślnego renderera. Rozwiązanie w gestii wykonawcy, np. generator dodaje renderer tylko do kopii assetu URP używanej przez AERO. Powtórz krok 4.
- [ ] **Step 6:** `git status`: upewnij się, że nic z `Assets/Mirza` nie jest dodane. Commit:

```bash
git add Assets/PozaZasiegiem Assets/Settings docs/superpowers/plans/gi-aero-report.md
git commit -m "AERO: wlaczona mgla wolumetryczna, strojenie i test bez assetu"
```

---

### Task 4: Które światła są wypalane

**Files:**
- Modify: `Assets/PozaZasiegiem/Scripts/LightCategory.cs` (pole)
- Create: `Assets/PozaZasiegiem/Editor/GiBaker.cs` (na razie tylko klasyfikacja)
- Modify: `Assets/PozaZasiegiem/Editor/LevelBuilder.cs` (`StoryLight` ~3074; tunel po `story.tunnelLights = …` ~3221; plany przerywników `BuildSurfaceSets`)
- Test: `Assets/PozaZasiegiem/Editor/Tests/GiBakerTests.cs`

**Interfaces:**
- Produces:
  - `public bool LightCategory.excludeFromBake` (serializowane, domyślnie `false`)
  - `public static class GiBaker { public static bool ShouldBake(Light l); }`

- [ ] **Step 1: Testy**

```csharp
public class GiBakerTests
{
    GameObject Make(int cat, params System.Type[] extra)
    {
        var g = new GameObject("L", typeof(Light));
        g.AddComponent<LightCategory>().category = cat;
        foreach (var t in extra) g.AddComponent(t);
        return g;
    }
    [TearDown] public void Clean() { foreach (var g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)) if (g.name.StartsWith("L") || g.name == "P") Object.DestroyImmediate(g); }

    [Test] public void CeilingLamp_IsBaked() => Assert.IsTrue(GiBaker.ShouldBake(Make(1).GetComponent<Light>()));
    [Test] public void GlowingObject_IsBaked() => Assert.IsTrue(GiBaker.ShouldBake(Make(2).GetComponent<Light>()));
    [Test] public void Flashlight_IsNotBaked() => Assert.IsFalse(GiBaker.ShouldBake(Make(0).GetComponent<Light>()));
    [Test] public void NoCategory_IsNotBaked() => Assert.IsFalse(GiBaker.ShouldBake(new GameObject("L", typeof(Light)).GetComponent<Light>()));
    [Test] public void Flicker_IsNotBaked() => Assert.IsFalse(GiBaker.ShouldBake(Make(1, typeof(FlickerLight)).GetComponent<Light>()));
    [Test] public void Excluded_IsNotBaked() { var g = Make(1); g.GetComponent<LightCategory>().excludeFromBake = true; Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>())); }
    [Test] public void UnderSpinLight_IsNotBaked() { var p = new GameObject("P"); p.AddComponent<SpinLight>(); var g = Make(2); g.transform.SetParent(p.transform); Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>())); }
    [Test] public void UnderHangingSway_IsNotBaked() { var p = new GameObject("P"); p.AddComponent<HangingSway>(); var g = Make(1); g.transform.SetParent(p.transform); Assert.IsFalse(GiBaker.ShouldBake(g.GetComponent<Light>())); }
}
```

- [ ] **Step 2:** Uruchom `unity command test run --filter GiBakerTests`. Oczekiwane: FAIL (brak `GiBaker` / pola).
- [ ] **Step 3:** Dodaj pole i zaimplementuj `ShouldBake`. Zwraca `true` tylko gdy:
  - jest `LightCategory` z `category` 1 lub 2 i `!excludeFromBake`,
  - w rodzicach (łącznie z samym obiektem) nie ma żadnego z: `FlickerLight`, `SpinLight`, `HangingSway`, `IdleSway`, `SwingDoor`, `StripCurtain`, `PlayerController`, `StalkerAI`, `Pickup`, `Note`, `PipeHatch`.
- [ ] **Step 4:** W generatorze ustaw `excludeFromBake = true`:
  - w `StoryLight`,
  - na każdym `LightCategory` pod `story.tunnelLights`,
  - na każdym `LightCategory` pod `story.surface` (po `BuildSurfaceSets`).
- [ ] **Step 5:** Testy: 8/8 PASS. Commit:

```bash
git add Assets/PozaZasiegiem/Scripts/LightCategory.cs Assets/PozaZasiegiem/Editor
git commit -m "GI: klasyfikacja swiatel do wypalania"
```

---

### Task 5: Generator przygotowuje scenę do APV

**Files:**
- Modify: `Assets/PozaZasiegiem/Editor/GiBaker.cs`
- Create: `Assets/PozaZasiegiem/Scripts/GiScenario.cs` (na razie tylko stałe nazw; metody dojdą w Task 7)
- Modify: `Assets/PozaZasiegiem/Editor/LevelBuilder.cs` (wywołanie w `Build`, przed pierwszym `SaveScene` ~239; `MarkStatic` ~3846)
- Generated: `Assets/PozaZasiegiem/Generated/PZ_Lighting.lighting`, `Assets/PozaZasiegiem/Generated/PZ_GI_BakingSet.asset`
- Test: `Assets/PozaZasiegiem/Editor/Tests/GiBakerTests.cs` (dopisz)

**Interfaces:**
- Consumes: `GiBaker.ShouldBake` (Task 4).
- Produces:
  - `public static string GiBaker.PrepareScene(Scene scene)`: zwraca linię raportu dla generatora.
  - `public static class GiScenario { public const string Power = "Prad", Outage = "Awaria"; }`: jedyne miejsce z nazwami scenariuszy; edytor i gra używają tych stałych.
  - `public const string GiBaker.BakingSetPath = "Assets/PozaZasiegiem/Generated/PZ_GI_BakingSet.asset";`
  - `public static bool GiBaker.ContributesGI(Renderer r)`

- [ ] **Step 1: Testy reguły statycznej:**
  - `ContributesGI` = `true` dla nieprzezroczystego `MeshRenderer` bez ruchomych rodziców,
  - `false` dla materiału z `renderQueue >= 2500`,
  - `false` dla `ParticleSystemRenderer`,
  - `false` dla `SkinnedMeshRenderer`,
  - `false` pod `IdleSway` i pod `Pickup`.
- [ ] **Step 2:** Testy FAIL, potem implementacja `ContributesGI` (te same wykluczenia rodziców co `ShouldBake` plus `VolumetricFx`). Testy PASS.
- [ ] **Step 3:** Zaimplementuj `PrepareScene`:
  1. Każdy `UniversalRenderPipelineAsset` w projekcie przez `SerializedObject`:
     - `m_LightProbeSystem` = Probe Volumes,
     - `m_SupportProbeVolumeScenarios = true`,
     - `m_SupportProbeVolumeScenarioBlending = true`,
     - `m_ProbeVolumeSHBands` = L1.
  2. `LightingSettings` zapisane jako `PZ_Lighting.lighting`:
     - `bakedGI = true`, `realtimeGI = false`,
     - `mixedBakeMode = MixedLightingMode.IndirectOnly`,
     - `lightmapper = ProgressiveGPU`,

     i przypisane przez `Lightmapping.lightingSettings`.
  3. Dla każdego renderera z `ContributesGI`:
     - dodaj flagę `ContributeGI` do istniejących flag statycznych,
     - `MeshRenderer.receiveGI = ReceiveGI.LightProbes`.
  4. Każde `Light`: `lightmapBakeType = ShouldBake ? Mixed : Realtime`. Każdy materiał w `Generated/` z włączoną emisją dostaje `globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack`, bo materiały świecące nie biorą udziału w wypalaniu (spec, punkt 3).
  5. Obiekt `"Adaptive Probe Volume (GI)"` z `ProbeVolume` w trybie globalnym.
  6. Baking Set w `BakingSetPath` ze scenariuszami `"Prad"` i `"Awaria"`, z tą sceną. Przypisanie sceny do zestawu przez publiczne API, a jeśli go nie ma, przez refleksję na `ProbeVolumeBakingSet`. Wybór należy do wykonawcy.

     Stare dane wypalania zestawu usuń (nowa mapa = nieważne dane).
  7. Zwróć `"GI: scena przygotowana (X świateł Mixed, Y Realtime, Z obiektów w GI). Uruchom Poza Zasiegiem → Wypal oswietlenie (GI)."`.
- [ ] **Step 4:** Wywołaj `PrepareScene` w `LevelBuilder.Build` przed `MarkStatic` i dopisz wynik do `report`. `MarkStatic` nie może kasować flagi `ContributeGI`: ustaw flagi tak, żeby `PrepareScene` działało po nim, albo przenieś wywołanie za `MarkStatic`.
- [ ] **Step 5:** `unity recompile`, `BuildFixed()` przez `script eval`. Sprawdź przez `script eval`:
  - `Lightmapping.lightingSettings.name == "PZ_Lighting"`,
  - liczba świateł `Mixed` > 100,
  - światła pod `tunnelLightsRoot` są `Realtime`,
  - liczba rendererów z `ContributeGI` > 1000,
  - istnieje dokładnie jeden `ProbeVolume`,
  - Baking Set ma oba scenariusze.

  W Play gra startuje bez wyjątków.
- [ ] **Step 6: Commit**

```bash
git add Assets/PozaZasiegiem Assets/Settings
git commit -m "GI: generator przygotowuje scene do Adaptive Probe Volumes"
```

---

### Task 6: Menu „Wypal oswietlenie (GI)”

**Files:**
- Modify: `Assets/PozaZasiegiem/Editor/GiBaker.cs`
- Generated: dane APV obok Baking Setu (`*.bytes`)

**Interfaces:**
- Consumes: `ShouldBake`, `GiScenario.Power`, `GiScenario.Outage`, `BakingSetPath` (Task 4–5).
- Produces:
  - `[MenuItem("Poza Zasiegiem/Wypal oswietlenie (GI)", false, 20)] public static void BakeBoth()`
  - `public static bool GiBaker.IsBaking`
  - `public static string GiBaker.LastReport`

- [ ] **Step 1:** Zaimplementuj `BakeBoth` jako maszynę stanów na `EditorApplication.update`:
  1. Zapisz scenę.
  2. `ProbeReferenceVolume.instance.lightingScenario = "Prad"`, wszystkie światła `Mixed` aktywne, `AdaptiveProbeVolumes.BakeAsync()`. Czekaj, aż `AdaptiveProbeVolumes.isRunning == false`.
  3. `"Awaria"`: dezaktywuj światła `Mixed` z `LightCategory.powered == true`, które nie leżą pod `BlackoutImmune`, potem `BakeAsync()` i czekaj.
  4. Przywróć dokładnie poprzedni stan `activeSelf` każdego światła, ustaw scenariusz `"Prad"`, zapisz scenę.
  5. Zbuduj `LastReport` z czasem obu wypaleń, łącznym rozmiarem plików `*.bytes` w folderze Baking Setu i największym pojedynczym plikiem w MB. Pokaż `EditorUtility.DisplayDialog` i `Debug.Log("[Poza Zasięgiem] " + LastReport)`.

  Pasek postępu: `EditorUtility.DisplayProgressBar`, czyszczony w `finally`. Przy wyjątku przywróć światła.
- [ ] **Step 2:** Uruchom przez `script eval`: `GiBaker.BakeBoth(); return "started";`. Potem odpytuj `return GiBaker.IsBaking + " " + GiBaker.LastReport;` co ~30 s (komendą w tle, bez pętli w eval), aż `IsBaking == false`. Modalne okno zamknij przez `unity command editor dialog`.

  Oczekiwane: `bakingSet.HasBakedData("Prad") && bakingSet.HasBakedData("Awaria")`.
- [ ] **Step 3:** Sprawdź raport. Plik > 50 MB oznacza **STOP** (Global Constraints). W przeciwnym razie dostrój parametry z sekcji „Decyzje wykonawcy” specu: odstęp sond, dylatacja, virtual offset, jasność odbić. Dostrajaj, aż na klatce `corridor_next_to_lit` nie będzie przecieków przez ściany. Parametry wpisz do `PrepareScene`, przebuduj i wypal ponownie.
- [ ] **Step 4: Commit** (sprawdź `git status`, czy dodawane są pliki `*.bytes` < 50 MB)

```bash
git add Assets/PozaZasiegiem
git commit -m "GI: menu wypalania scenariuszy Prad i Awaria, wypalone dane"
```

---

### Task 7: Przełączanie scenariuszy w grze

**Files:**
- Modify: `Assets/PozaZasiegiem/Scripts/GiScenario.cs` (utworzony w Task 5 ze stałymi)
- Modify: `Assets/PozaZasiegiem/Scripts/Blackout.cs` (`Start` ~59, `SetPower` ~101, `EndRoutine`)
- Test: `Assets/PozaZasiegiem/Editor/Tests/GiScenarioTests.cs`

**Interfaces:**
- Consumes: `GiScenario.Power`, `GiScenario.Outage` (Task 5).
- Produces (dopisane do `GiScenario`):
  - `public static bool IsAvailable { get; }`: APV włączone w aktualnym URP, `ProbeReferenceVolume.instance` zainicjalizowany i ma wczytane dane.
  - `public static void Set(string scenario)`
  - `public static void Blend(string to, float factor)`: `factor` 0..1. Oba to no-op, gdy `!IsAvailable`.

- [ ] **Step 1: Testy (Review Focus 3):** w EditMode, bez danych i z dowolnym stanem APV:
  - `Assert.DoesNotThrow(() => GiScenario.Set(GiScenario.Outage))`,
  - `Assert.DoesNotThrow(() => GiScenario.Blend(GiScenario.Power, 0.5f))`,
  - `Assert.AreEqual("Prad", GiScenario.Power)`,
  - `Assert.AreEqual("Awaria", GiScenario.Outage)`.
- [ ] **Step 2:** FAIL, potem implementacja (`ProbeReferenceVolume.instance.lightingScenario` / `BlendLightingScenario`), PASS.
- [ ] **Step 3:** `Blackout`:
  - `SetPower(on)` wywołuje `GiScenario.Set(on ? Power : Outage)`, więc każde mrugnięcie migotania przełącza odbicia razem z lampami.
  - `EndRoutine` po ostatnim włączeniu: zamiast natychmiastowego `"Prad"` rób `Blend("Prad", t)` przez 0,5 s od stanu `"Awaria"`, a na koniec `Set("Prad")`.
  - `Start()` wywołuje `GiScenario.Set(GiScenario.Power)` (Review Focus 2).
- [ ] **Step 4: Sprawdzenie w Play** (przez `script eval`, scena z wypalonymi danymi z Task 6):
  1. `ProbeReferenceVolume.instance.lightingScenario == "Prad"`.
  2. `Blackout.Instance.Begin(-1,false)`, po 1 s scenariusz to `"Awaria"`.
  3. Klatka `lit_room` jest wyraźnie ciemniejsza od klatki z prądem (średnia jasność ≤ wersji `before_lit_room_blackout.png`).
  4. `Blackout.Instance.End()`, po 1,5 s scenariusz to `"Prad"`.
  5. Restart: `Begin(-1,false)`, potem `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`, po 2 s scenariusz to `"Prad"`.
  6. Zero wyjątków w logu.
- [ ] **Step 5: Commit**

```bash
git add Assets/PozaZasiegiem/Scripts Assets/PozaZasiegiem/Editor/Tests
git commit -m "GI: awaria pradu przelacza scenariusz oswietlenia"
```

---

### Task 8: Odbłysk latarki

**Files:**
- Create: `Assets/PozaZasiegiem/Scripts/FlashlightBounce.cs`
- Modify: `Assets/PozaZasiegiem/Editor/LevelBuilder.cs` (`MakePlayer`, po `var fl = flGo.AddComponent<Flashlight>();` ~2047)
- Test: `Assets/PozaZasiegiem/Editor/Tests/FlashlightBounceTests.cs`

**Interfaces:**
- Consumes: `Blackout.Active`, `Blackout.Instance.flashlightPower`, `Flashlight` (istniejący komponent na tym samym obiekcie).
- Produces:
  - `public class FlashlightBounce : MonoBehaviour`
  - `public static float Intensity(float hitDistance, float range, float baseIntensity)`
  - `public static Color Tint(Color lightColor, Color surface)`
  - `public static Vector3 Placement(Vector3 hitPoint, Vector3 normal, Vector3 toCamera, float hitDistance)`

- [ ] **Step 1: Testy (Review Focus 5):**
  - `Intensity(1, 26, 2)` > `Intensity(10, 26, 2)` > 0,
  - `Intensity(26, 26, 2)` == 0,
  - `Intensity(0.05f, 26, 2)` ≤ 2,
  - `Tint(Color.white, Color.red).r` > `Tint(Color.white, Color.red).g`,
  - `Tint(white, black)` nie jest całkiem czarny (min. 15% koloru latarki),
  - `Placement(hit, normal, toCam, 0.2f)` leży nie dalej niż `0.2f * 0.5f` od trafienia (przy bliskiej ścianie odbłysk nie przechodzi za kamerę),
  - `Placement` przy dużym dystansie leży 0,3 m od trafienia wzdłuż normalnej.
- [ ] **Step 2:** FAIL, potem implementacja funkcji, PASS.
- [ ] **Step 3:** Zaimplementuj `Update`:
  - raycast z pozycji światła wzdłuż `forward`, zasięg = `Light.range` latarki, maska bez warstwy gracza,
  - brak trafienia albo zgaszona latarka: światło odbłysku wyłączone,
  - w przeciwnym razie punktowe światło (dziecko, `Realtime`, bez cieni, `range = 3`, `LightCategory` kat. 0) w `Placement`, z jasnością `Intensity(...)` × (w awarii `Blackout.Instance.flashlightPower`) i kolorem `Tint(latarka, _BaseColor trafionego sharedMaterial lub szary 0.5)`.

  Bazowa jasność w URP ~1.2 (dostroić wizualnie).
- [ ] **Step 4:** Dodaj komponent w `MakePlayer`. Przebuduj scenę (`BuildFixed`), ponownie uruchom `BakeBoth` (przebudowa kasuje dane). W Play sprawdź:
  - odbłysk włączony przy celowaniu w ścianę,
  - wyłączony przy zgaszonej latarce,
  - słabszy w awarii.

  Zrób klatkę porównawczą z latarką wycelowaną w ścianę.
- [ ] **Step 5: Commit**

```bash
git add Assets/PozaZasiegiem
git commit -m "GI: pozorne odbicie swiatla latarki"
```

---

### Task 9: Zdjęcia „po”, pomiary, wypchnięcie

**Files:**
- Create: `docs/superpowers/plans/gi-shots/after_*.png`
- Modify: `docs/superpowers/plans/gi-baseline.md` (dopisz wyniki „po”)
- Modify: `README.md` (sekcja „Generowanie poziomu od nowa”: dopisać krok wypalania)

- [ ] **Step 1:** Wyrenderuj te same ujęcia co w Task 1 (z `gi-shots.json`) jako `after_*.png`, plus `after_lit_room_blackout.png`.
- [ ] **Step 2:** FPS jak w Task 1, w trzech wariantach:
  - mgła wyłączona w ustawieniach,
  - GI bez AERO (`AeroFog.Apply(false)`),
  - GI + AERO.

  Dopisz do `gi-baseline.md`.
- [ ] **Step 3:** Przejście bez wyjątków: start gry, `Blackout.Begin` / `End`, `StoryDirector` w fazie pościgu (światła tunelu włączone skryptem, nadal `Realtime`).
- [ ] **Step 4:** W `README.md` po menu generatora dopisz: po przebudowie mapy uruchomić **Poza Zasiegiem → Wypal oswietlenie (GI)** (kilka–kilkanaście minut), bez tego gra działa, ale bez odbitego światła.
- [ ] **Step 5:** Commit i push:

```bash
git add docs README.md
git commit -m "GI: zdjecia i pomiary po zmianach, instrukcja wypalania w README"
git push origin main
```

- [ ] **Step 6:** Pokaż autorowi pary zdjęć przed/po i tabelę FPS.
