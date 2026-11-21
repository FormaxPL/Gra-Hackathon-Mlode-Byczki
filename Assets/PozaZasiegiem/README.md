# POZA ZASIĘGIEM – pakiet startowy v5 (Unity)

Działa w **Unity 6** i **Unity 2022.3 LTS**. Najlepiej z szablonem **3D (URP)**: cienie wszystkich świateł, bloom, noktowizja i wizjer kamery działają w pełni tylko w URP.
Nie wymaga żadnych dodatkowych pakietów ani assetów. Wszystko jest z prostych brył, a tekstury i dźwięki są generowane kodem.

## Aktualizacja do v4

1. Jeśli coś ręcznie zmienialiście w scenie `TestLevel`, skopiujcie ją poza folder `PozaZasiegiem`.
2. **Usuńcie stary folder `Assets/PozaZasiegiem`.**
3. Zaimportujcie `PozaZasiegiem.unitypackage` (albo rozpakujcie zip do projektu). Nowość: folder `Shaders` z shaderem snopów światła.
4. Kliknijcie **Poza Zasiegiem → Zbuduj scene testowa (staly uklad)** i Play. Na starcie pojawi się **menu główne**.

## Sterowanie

| Klawisz | Akcja |
|---|---|
| WASD + mysz | ruch |
| Shift | bieg (głośny! ograniczona kondycja, potem zadyszka) |
| Ctrl / C | kucanie (ciche, trudniej cię zobaczyć) |
| E | podnieś, czytaj notatkę, schowaj się / wyjdź; podczas czytania odkłada notatkę |
| F | latarka; **gdy kamera jest podniesiona: noktowizja** |
| **PPM** | podnieś / opuść kamerę (wizjer jak w Outlast) |
| N | noktowizja (gdy kamera podniesiona) |
| Kółko myszy | zoom kamery |
| Tab / Q (trzymaj) | wykrywacz ruchu (zasięg 10 m) |
| LPM | gaśnica, przegania potwora do rur |
| Spacja (w kryjówce) | wstrzymanie oddechu |
| Spacja (w przerywniku) | pomiń intro |
| **Esc** | **pauza: wróć, ustawienia, restart, menu główne, wyjście** |
| F1 | debug AI |
| R | restart po śmierci/wygranej |

## Co nowego w v5

**Fabuła od początku do końca:**
1. **Intro:** znajomi (Kuba i Ola) nagrywają film w opuszczonym magazynie fabryki paliwa syntetycznego. Podłoga się zapada, bohater (Marek) spada, a potem budzi się na podłodze labiryntu i wstaje. Intro można pominąć Spacją.
2. **Awaria prądu** po zebraniu 2. części radiostacji (około 75 s ciemności).
3. **Radiostacja → Agencja ECHO** (Służba Odzyskiwania Zaginionych Poza Zasięgiem). Agencja odcina zasilanie, żeby odblokować **bramę serwisową** na wschodniej ścianie mapy. Do bramy trzeba się przekraść w całkowitej ciemności. Brama ma pomarańczowy kogut i sygnał dźwiękowy.
4. **Tunel za bramą:** po kilku metrach zapalają się czerwone światła, wyje syrena i potwór rusza w **pościg do samego końca**. W pościgu kondycja się nie kończy, a gaśnica tylko na chwilę go zatrzymuje.
5. **Portal ECHO:** agenci odpędzają potwora reflektorem. Bohater przechodzi przez portal na nocny plac przed magazynem, gdzie czekają furgonetka Agencji i przyjaciele. Potem napisy końcowe i powrót do menu.
- **Śmierć w pościgu** cofa do punktu kontrolnego przy bramie (R).

**Awaria prądu (blackout):**
- Gasną lampy, automaty i znaki, a otoczenie robi się prawie czarne.
- Latarka świeci słabo i krótko, a przy tym **ściąga potwora**: wydaje „hałas”, a potwór widzi ją z daleka.
- Kamera z noktowizją potworowi nie przeszkadza i widać w niej o niebo lepiej.
- **Włączenie noktowizji samo gasi latarkę.** Po wyłączeniu noktowizji latarka wraca.

**Nowy model potwora:** `Models/entity.fbx` z animacją `MonsterAnimationDriver` od kolegi z drużyny. Generator sam:
- wyłącza import kamery i światła z FBX,
- skaluje model do 2,15 m i obraca go przodem (wg oczu),
- podpina kości:

  | Część | Kości |
  |---|---|
  | tułów | Bone |
  | głowa | Bone.002 |
  | uda | Bone.006, Bone.009 |
  | łydki | Bone.007, Bone.010 |
  | ręce | Bone.003, Bone.004 |

- przenosi świecące oczy na głowę modelu.

Animacja dostała automatyczne osie zamachu (nogi i ręce chodzą naprzemiennie przy każdej orientacji kości), uginanie kolan i tryb ataku do jumpscare'ów. Stan AI przełącza tryby: chód, trucht, sprint, czajenie przy szafce, wejście do rury i atak.
> **Uwaga:** jeśli macie już `MonsterAnimationDriver.cs` w innym folderze projektu, usuńcie tamtą kopię. Wersja w paczce ma to samo API, a dwie klasy o tej samej nazwie dadzą błąd kompilacji.

**Mgła AERO (Volumetric Fog and Mist):** generator szuka w projekcie renderera URP z AERO (Full Screen Pass z materiałem mgły). Gdy go znajdzie:
- kopiuje go jako `Generated/PZ_AeroRenderer`, a materiał mgły jako `Generated/M_PZ_AeroFog.mat`,
- dopisuje ten renderer do assetów URP, a kamera przełącza się na niego, gdy w ustawieniach są włączone „Mgła i snopy światła”,
- **wyłącza starą mgłę z cząsteczek**. Zostają snopy pod lampami (trochę słabsze) i kurz w świetle,
- dodaje w ustawieniach suwak **Gęstość mgły (AERO)**.

Raport po budowaniu i konsola pokazują, co znaleziono, oraz listę właściwości materiału. Wygląd mgły stroicie w `M_PZ_AeroFog.mat`. Jeśli AERO nie ma w projekcie, gra zostaje przy mgle z cząsteczek.

**Optymalizacja: karta liczy tylko to, co widać:**
- generator wypala **Occlusion Culling**, więc ściany zasłaniają to, co jest za nimi (przezroczyste snopy i szkło nie zasłaniają),
- `LightCuller` co 0,2 s wyłącza światła dalej niż mgła, a **cienie liczy tylko tym światłom, które są w kadrze i nie są za ścianą** (cień to najdroższa część światła),
- daleka płaszczyzna kamery to 48 m, bo i tak dalej jest mgła.

**Mniej dyszenia:** zadyszka po biegu jest cichsza i wolniejsza, mija szybciej, a obraz kołysze się dużo słabiej, też w biegu i ze strachu.

## Co nowego w v4

**Cienie wszędzie:**
- Latarka znowu wyraźnie rzuca cień. W v3 szerokie rozproszenie latarki nie miało cienia i zalewało cień snopu. Teraz oba światła latarki rzucają cień.
- Każda lampa sufitowa rzuca cień.
- Świecące obiekty mają własne światło z cieniem: automaty z napojami, **nowe lodówki z przeszklonymi drzwiami** (butelki rzucają cień na podłogę), monitory i telewizory CRT (migoczą), znaki EXIT, światła awaryjne, radiostacja, przedmioty i notatki na postumentach oraz czerwone oczy potwora.
- Generator przełącza renderer URP na **Forward+** (Unity 2022.2+), żeby nie było limitu 8 świateł na obiekt.

**Mgła i snopy światła (god rays):**
- Pod każdą lampą jest świecący stożek światła z kurzem. Latarka też ma widoczny snop.
- W każdym pomieszczeniu jest nisko wisząca mgła, w każdej strefie inna: mroźna w chłodni, para w kuchni, pył w magazynie, wilgotna mgiełka w Backrooms.
- Mgła jest oświetlana, więc w ciemności jej nie widać, a w snopie latarki i pod lampami „gęstnieje”.
- W świetle lamp unoszą się drobinki kurzu.

**Mocny bloom:** wolumen `GlowVolume` (włącz/wyłącz w ustawieniach).

**Menu główne i pauza:** rozpocznij grę, ustawienia, wyjdź z gry. Esc w trakcie gry otwiera pauzę.

**Ustawienia** (zapisywane w PlayerPrefs):
- język: **polski / English** (cała gra, notatki i komunikaty),
- rozdzielczość, tryb wyświetlania, jakość grafiki,
- antyaliasing: wyłączony / FXAA / SMAA / TAA / MSAA 4x,
- cienie: niskie (tylko latarka) / średnie (+ lampy) / wysokie (wszystko),
- mgła i snopy światła, bloom,
- VSync, limit FPS, jasność, pole widzenia, czułość myszy, głośność.

**Dźwięk noktowizji:** klik przełącznika i pisk przetwornicy, rosnący przy włączaniu i opadający przy wyłączaniu. Podniesienie kamery ma dźwięk chwytu.

**Więcej korytarzy, większa mapa:**
- Mapa ma teraz 9×9 pomieszczeń zamiast 8×8.
- Backrooms i część magazynu to wąskie korytarze 2,4 m: skrzyżowania, zakręty i ślepe zaułki.
- Pod sufitem jest 15 włazów.

**Animacje bohatera (immersja):**
- kołysanie głowy w rytm kroków, mocniejsze w biegu, z przechyłem na boki,
- pochylenie przy ruchu w bok,
- ręce pracują naprzemiennie w biegu,
- oddech w spoczynku, po biegu zadyszka (głośniejsza i szybsza, gdy kończy się kondycja),
- drżenie obrazu ze strachu, gdy potwór jest blisko,
- kroki brzmią inaczej na płytkach, wykładzinie i blasze w chłodni.

## Jeśli czegoś nie widać

- **Budowanie trwa dłużej** (do około minuty): na końcu wypala się Occlusion Culling.
- **Potwór wygląda źle lub stoi tyłem:** w Inspectorze `Stalker/Model (placeholder)/Model (entity.fbx)` obróćcie dziecko `entity`. Osie animacji liczą się przy starcie gry.
- **Mgła AERO za gęsta lub za rzadka:** suwak w ustawieniach albo `Generated/M_PZ_AeroFog.mat`.
- **Snopy światła różowe lub niewidoczne:** sprawdźcie, czy zaimportował się `Shaders/LightShaft.shader`, i zbudujcie scenę jeszcze raz.
- **Brak cieni:** URP Asset → Lighting → Additional Lights: **Per Pixel** + **Cast Shadows**. Generator próbuje to ustawić sam (raport po budowaniu).
- **Gra się tnie:** w ustawieniach dajcie Cienie → Średnie albo Niskie, wyłączcie mgłę i snopy, a zamiast TAA wybierzcie FXAA.
- **Za ciemno lub za jasno:** suwak Jasność w ustawieniach.
- **Brak post-processingu:** patrzcie w okno Game (nie Scene).

## Uczciwie o „wolumetrycznej” mgle

URP nie ma wbudowanej mgły wolumetrycznej, więc v4 ją imituje: oświetlane cząsteczki mgły, addytywne stożki światła i mgła odległości. Działa to na każdym komputerze i w obu pipeline'ach. Prawdziwą mgłę liczoną promieniami (raymarching) dałoby się zrobić tylko osobnym Renderer Feature, a na to w hackathonie raczej nie ma czasu.

## Strojenie potwora (Inspector → `Stalker` → StalkerAI)

- **Rurociąg:** `pipeSpeed`, `climbTime`, `retreatTime`.
- **Zasadzka:** `ambushChance`, `ambushDropRadius`, `ambushDuration`, `ambushCooldown`.
- F1 pokazuje stan AI.

## Podmiana placeholderów na assety

- **Potwór (Mixamo):** wstaw model jako dziecko `Stalker` i usuń `Model (placeholder)`. W StalkerAI podepnij **Model** i **Eyes**.
- **Meble:** muszą mieć Collider i leżeć pod `Level (NavMesh)`.
- **Nowe świecące obiekty:** dodaj do światła komponent `LightCategory` (kategoria 2), żeby ustawienia cieni je obejmowały.
- **Darmowe źródła:** Asset Store (Free), Kenney.nl, Poly Haven, ambientCG, Mixamo, Freesound. Zapisujcie licencje do creditsów.
