# Menu Backrooms, losowe mapy i losowe awarie: plan wdrożenia

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Cel:** Osobna scena menu Backrooms z potworem odgrywającym losowe zachowania, 3 gotowe mapy losowane przy starcie i losowe awarie prądu w czasie eksploracji.

**Architektura:**
- Czyste funkcje losowania są testowalne w EditMode: `MapPicker`, harmonogram awarii, wybór zachowania potwora.
- Generator buduje 3 mapy i scenę menu, a zestawy GI nazywa po scenie.
- `MenuSystem` rozróżnia scenę menu od mapy.

**Stack:** Unity 6000.6.10f1, URP, APV, Unity Test Framework, Unity CLI (`unity command …` na otwartym edytorze).

**Spec:** `docs/superpowers/specs/2026-11-21-menu-mapy-awarie-design.md`

## Global Constraints

- Nazwy scen: `MainMenu`, `TestLevel_A`, `TestLevel_B`, `TestLevel_C` w `Assets/PozaZasiegiem/Scenes/`. Ziarna: A = 12345, B = 24680, C = 36912.
- Build Settings w kolejności: `MainMenu`, `TestLevel_A`, `TestLevel_B`, `TestLevel_C`. Stary `TestLevel.unity` i jego dane są usuwane.
- Awaria losowa: odstęp 180–360 s, długość 20–45 s, `showHint: false`.
- Zachowania potwora w menu: co 12–25 s, bez powtórzenia tego samego dwa razy z rzędu.
- Tytuł: „THE BACKROOMS: ISOLATION”. Teksty w menu przez `Loc.T(pl, en)`.
- Restart (R) i „Zacznij od nowa” wczytują bieżącą mapę, a „Menu główne” wczytuje `MainMenu`.
- Pojedynczy plik w repozytorium < 50 MB.
- Komentarze po polsku. Testy w `Assets/PozaZasiegiem/Editor/Tests/`.

## Review Focus

1. **Restart na mapie B lub C** wczytuje tę samą mapę, a nie A. Test w zadaniu 1, sprawdzenie w zadaniu 5.
2. **Losowa awaria w trakcie czytania notatki, kryjówki albo przerywnika** nie startuje i jest przesuwana na później. Test w zadaniu 2.
3. **Losowa awaria przed awarią fabularną** (po radiu) nie blokuje tamtej, bo fabularna i tak startuje. Sprawdzenie w zadaniu 2 (Play).
4. **Menu bez AERO** działa jak mapy, z mgłą z cząsteczek albo bez niej. Sprawdzenie w zadaniu 4.
5. **Potwór w menu nie wchodzi w ściany ani w kamerę**: ścieżki są stałe, a klatki potwierdzają wszystkie 3 zachowania. Zadanie 4.

---

### Task 1: Losowanie mapy i przejścia między scenami

**Files:**
- Create: `Assets/PozaZasiegiem/Scripts/MapPicker.cs`
- Modify: `Assets/PozaZasiegiem/Scripts/MenuSystem.cs`: `Start` ~40, `StartGame` ~61, `Restart` ~174
- Test: `Assets/PozaZasiegiem/Editor/Tests/MapPickerTests.cs`

**Interfaces:**
- Produces:
  - `public static class MapPicker`
  - `public const string MenuScene = "MainMenu";`
  - `public static readonly string[] Maps = { "TestLevel_A", "TestLevel_B", "TestLevel_C" };`
  - `public static string Pick(System.Random r)`
  - `public static bool IsMenuScene(string sceneName)`

- [ ] Testy:
  - `Pick` zawsze zwraca element `Maps`,
  - przy 3000 losowaniach (ziarno 1) każda mapa wypada 800–1200 razy,
  - `IsMenuScene("MainMenu")` jest prawdą, a `IsMenuScene("TestLevel_B")` fałszem.

  Uruchom: FAIL (brak klasy).
- [ ] Implementacja `MapPicker`. Potem PASS.
- [ ] `MenuSystem`:
  - w scenie menu (`MapPicker.IsMenuScene(SceneManager.GetActiveScene().name)`) zawsze otwiera `Page.Main`,
  - w mapie nigdy nie otwiera `Page.Main` na starcie, tylko od razu `StartGame` (intro),
  - `StartGame` w scenie menu wczytuje `MapPicker.Pick(new System.Random())`, a w mapie zachowuje dotychczasowe zachowanie,
  - `Restart(false)` („Menu główne”) wczytuje `MapPicker.MenuScene`,
  - `Restart(true)` i `GameManager.Restart` bez zmian, bo wczytują bieżącą scenę.
- [ ] Commit: `Mapy: losowanie mapy i przejscia menu <-> mapa`.

### Task 2: Losowe awarie

**Files:**
- Create: `Assets/PozaZasiegiem/Scripts/RandomBlackouts.cs`
- Test: `Assets/PozaZasiegiem/Editor/Tests/RandomBlackoutsTests.cs`

**Interfaces:**
- Consumes: `Blackout.Instance.Begin(float duration, bool showHint)`, `Blackout.Active`, `StoryDirector.CurrentPhase`, `PlayerController.CutsceneLock`, `GameManager.Instance.InputBlocked`.
- Produces:
  - `public class RandomBlackouts : MonoBehaviour`
  - `public static float NextInterval(System.Random r)` → [180, 360]
  - `public static float NextDuration(System.Random r)` → [20, 45]
  - `public static bool CanStart(bool explorePhase, bool cutscene, bool blackoutActive, bool inputBlocked, bool hiding)`

- [ ] Testy:
  - 1000 losowań: `NextInterval` w [180, 360], `NextDuration` w [20, 45],
  - `CanStart(true, false, false, false, false)` jest prawdą,
  - każde z pozostałych pól ustawione „źle” daje fałsz (Review Focus 2).

  FAIL, potem implementacja, potem PASS.
- [ ] `Update`: odliczanie czasu w grze. Po upływie, gdy `CanStart` jest prawdą, wywołaj `Begin(NextDuration, false)` i wylosuj nowy odstęp. Gdy `CanStart` jest fałszem, ponów za 10 s. „W kryjówce” = `HidingSpot` z aktywnym graczem: wykonawca znajduje to w kodzie.
- [ ] Generator dodaje komponent obok `Blackout` (w `LevelBuilder`, tam gdzie powstaje `Blackout`).
- [ ] Play (po przebudowie w zadaniu 3): wymuszenie odstępu 1 s przez refleksję. Awaria startuje, gasi światła, scenariusz GI to „Awaria”. Po zakończeniu GI wraca do „Prad”. Fabularna awaria po radiu nadal startuje (Review Focus 3).
- [ ] Commit: `Losowe awarie pradu w czasie eksploracji`.

### Task 3: Trzy mapy z generatora

**Files:**
- Modify: `Assets/PozaZasiegiem/Editor/LevelBuilder.cs`: `ScenePath`, `Build(int seed)`, `AddSceneToBuild`
- Modify: `Assets/PozaZasiegiem/Editor/GiBaker.cs`: `BakingSetPath` → ścieżka zależna od sceny
- Test: `Assets/PozaZasiegiem/Editor/Tests/GiBakerTests.cs` (dopisz)

**Interfaces:**
- Produces:
  - `public static string GiBaker.BakingSetPathFor(string sceneName)` → `Assets/PozaZasiegiem/Generated/PZ_GI_BakingSet_<sceneName>.asset`
  - `public static void LevelBuilder.BuildMap(string sceneName, int seed)`
  - menu `Poza Zasiegiem/Zbuduj wszystkie mapy i menu` → `LevelBuilder.BuildAll()` (mapy, menu, wypalanie po kolei)

- [ ] Test: `BakingSetPathFor("TestLevel_B")` kończy się na `PZ_GI_BakingSet_TestLevel_B.asset`, a ścieżki dla A i B są różne. FAIL, potem implementacja, potem PASS.
- [ ] Generator:
  - `Build(seed)` przyjmuje ścieżkę sceny,
  - stare pozycje menu („staly/losowy uklad”) budują `TestLevel_A`,
  - `GiBaker` używa zestawu sceny aktywnej (`BakingSetPathFor(scene.name)`),
  - `BakeBoth` działa na aktywnej scenie.
- [ ] `BuildAll`: maszyna stanów na `EditorApplication.update`, bo wypalanie jest asynchroniczne. Dla każdej mapy: `BuildMap`, potem `BakeBoth`, potem czekanie na `!GiBaker.IsBaking`. Na końcu menu (zadanie 4), Build Settings w kolejności z Global Constraints i usunięcie starego `TestLevel.unity` z danymi.
- [ ] Uruchom `BuildAll` (Quiet) w tle i odpytuj co 30 s. Oczekiwane: 3 sceny i 3 zestawy GI z danymi „Prad”/„Awaria”, każdy plik < 50 MB.
- [ ] Commit: `Trzy mapy z generatora, osobne GI dla kazdej`.

### Task 4: Scena menu z potworem

**Files:**
- Create: `Assets/PozaZasiegiem/Scripts/MenuStalkerDirector.cs`
- Create: `Assets/PozaZasiegiem/Editor/MenuSceneBuilder.cs`
- Test: `Assets/PozaZasiegiem/Editor/Tests/MenuStalkerTests.cs`

**Interfaces:**
- Consumes: `MonsterAnimationDriver.SetMode/SetSpeed`, materiały strefy Backrooms z `Generated/` (`M_Wall_Backrooms`, `M_Floor_Backrooms`, `M_Ceiling_Drop`, `M_Lamp_Backrooms`), `LevelBuilder.AttachEntityModel` lub jej odpowiednik do podpięcia `entity.fbx`.
- Produces:
  - `public enum MenuStalkerAct { PeekCorner, VentCrawl, StandFar }`
  - `public static MenuStalkerAct NextAct(MenuStalkerAct last, System.Random r)`: nigdy `last`
  - `public static float NextDelay(System.Random r)` → [12, 25]
  - `public static void MenuSceneBuilder.Build()` → zapisuje `Assets/PozaZasiegiem/Scenes/MainMenu.unity`

- [ ] Testy:
  - 1000 losowań `NextAct` nigdy nie zwraca poprzedniego zachowania, a każde z 3 wypada,
  - `NextDelay` mieści się w [12, 25].

  FAIL, potem implementacja, potem PASS.
- [ ] `MenuStalkerDirector`:
  - korutyna z 3 zachowaniami po stałych punktach (`Transform` ustawiane przez generator: `corner`, `cornerPeek`, `vent`, `ventFloor`, `walkEnd`, `farSpot`),
  - ruch `Vector3.MoveTowards` (1,3 m/s chód), obrót głowy (`MonsterAnimationDriver` + `LookAt` dla kości głowy) na kamerę przy PeekCorner i StandFar,
  - przy StandFar 1–2 świetlówki migają (`FlickerLight`) na znikanie,
  - między zachowaniami potwór ukryty (`SetActive(false)`).
- [ ] `MenuSceneBuilder.Build()`:
  - korytarz w kształcie litery L (ok. 3×12 m + skręt) ze ścianami, podłogą i sufitem z materiałów Backrooms,
  - 4 świetlówki (`Lamp Light`, jedna z `FlickerLight`) i kratka wentylacyjna na ścianie przy suficie,
  - kamera z dryfem (prosty skrypt w `MenuStalkerDirector`, sinusoidalne kołysanie ±1,5°), post-processing jak w mapach (`SetupPostProcessing`) i mgła AERO (`AeroFog` z tym samym materiałem),
  - `GameSettings` / `MenuSystem` (`showMainMenuOnStart`), potwór z animacją i `MenuStalkerDirector` z punktami.

  Bez wypalania GI, bo menu jest małe (płaski ambient).
- [ ] Play scena `MainMenu`: przez refleksję wymuś po kolei 3 zachowania i zrób klatkę każdego (`docs/superpowers/plans/gi-shots/menu_*.png`). Zmierz czas klatki 1080p (cel < 10 ms). Bez wyjątków.
- [ ] Commit: `Menu glowne Backrooms z potworem`.

### Task 5: Sprawdzenie całości i wypchnięcie

- [ ] Play od `MainMenu`:
  - „Rozpocznij grę” wczytuje jedną z 3 map,
  - w mapie R wczytuje tę samą mapę (Review Focus 1),
  - pauza → „Menu główne” wczytuje `MainMenu`.
- [ ] Testy EditMode: wszystkie zielone.
- [ ] README: nowa pozycja menu generatora („Zbuduj wszystkie mapy i menu”, ok. 6–8 min), opis losowych map i awarii.
- [ ] Commit i `git push origin feature/oswietlenie-gi`. Aktualizacja opisu PR #1.
