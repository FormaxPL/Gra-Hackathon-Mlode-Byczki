# Oświetlenie globalne i mgła AERO: projekt

Data: 2026-11-21
Status: do akceptacji

## Po co

Gra „Poza Zasięgiem” (hackathon, Unity 6000.6.10f1, URP) ma wyglądać dojrzalej. Dziś światło nie odbija się od ścian: cienie wypełnia płaskie, jednolite tło, a mgła to cząsteczki. Autor chce:

1. **prawdziwej mgły wolumetrycznej** z darmowego assetu AERO – Volumetric Fog and Mist (Mirza Beig), który ma już zaimportowany,
2. **oświetlenia globalnego**: światło lamp odbija się od ścian i podłóg, rozjaśnia sąsiednie korytarze i nadaje kolor otoczeniu.

Autor wybrał wariant **A: wypalane sondy (Adaptive Probe Volumes) z dwoma wariantami oświetlenia: „prąd” i „awaria”**.

To pierwsza z trzech prac. Kolejne (ludzie z prawdziwymi modelami, lepszy model stalkera) będą miały osobne projekty.

## Stan obecny (zmierzony w edytorze)

- 181 świateł, wszystkie `Realtime`: 162 reflektory z miękkimi cieniami, 18 punktowych bez cieni, 1 reflektor bez cieni.
- Brak oświetlenia globalnego: `LightProbeSystem = LegacyLightProbes`, 0 rendererów z flagą `ContributeGI`, scena nie ma assetu Lighting Settings.
- URP (`PC_RPAsset`) obsługuje APV, scenariusze i ich mieszanie (`ProbeReferenceVolume.lightingScenario`, `BlendLightingScenario`), ale w assecie są wyłączone.
- Mapa (`Level (NavMesh)`) ma około 115 × 7 × 54 m, a scena około 4950 rendererów. Daleko od labiryntu stoją plany przerywników (intro w magazynie, finał na placu).
- Światła:
  - kategoria w `LightCategory` (0 latarka, 1 lampy sufitowe, 2 świecące obiekty) i flaga `powered`,
  - awaria (`Blackout.SetPower`) gasi światła `powered`, przyciemnia ambient do 6% i podmienia materiały świecące na zgaszone,
  - `LightCuller` wyłącza dalekie światła (`SetActive(false)`) i cienie poza kadrem,
  - światła fabularne (`scriptedOff`, np. czerwone w tunelu przed pościgiem) włącza skrypt.
- Integracja AERO już istnieje (`LevelBuilder.SetupAero`, `AeroFog.cs`):
  - generator szuka renderera URP z Full Screen Pass z materiałem mgły,
  - kopiuje go do `Generated/PZ_AeroRenderer` i `Generated/M_PZ_AeroFog.mat`,
  - dopisuje go do list rendererów assetów URP,
  - wyłącza mgłę z cząsteczek,
  - kamera przełącza renderer według ustawienia „Mgła i snopy światła”.

  Scena w repozytorium była zbudowana bez AERO.
- AERO od wersji 1.2 obsługuje APV, czyli oświetla mgłę światłem z sond.

## Cele

1. Mgła AERO działa w grze i jest oświetlana przez lampy, latarkę i odbite światło z sond.
2. Światło odbite od otoczenia jest wypalone do sond APV w dwóch scenariuszach:
   - **„Prad”**: wszystkie zasilane lampy i świecące obiekty włączone,
   - **„Awaria”**: zasilane źródła wyłączone, zostają tylko źródła odporne na awarię.
3. Przy awarii odbite światło znika razem z lampami: płynne przejście scenariuszy w rytm istniejącego migotania. Po powrocie prądu wraca.
4. Latarka daje tani pozór odbicia: słabe światło w miejscu, gdzie trafia snop.
5. Wypalanie uruchamia się jedną pozycją menu **Poza Zasiegiem → Wypal oswietlenie (GI)**. Generator przygotowuje scenę do wypalania, ale sam nie wypala, bo to trwa długo.
6. Projekt bez AERO (np. u kolegi, który go nie zaimportował) działa bez błędów i bez czarnego ekranu: zostaje mgła z cząsteczek.

## Poza zakresem

- Lightmapy (wypalanie do tekstur): odrzucone, bo przy ~5000 obiektach to długie wypalanie i dwa komplety dużych tekstur.
- Odbicia lustrzane (reflection probes) i SSAO: osobna praca, jeśli autor zechce.
- Dynamiczne GI od latarki, migających świetlówek, kogutów i świateł fabularnych: zostają `Realtime` bez wypalania. Latarka dostaje tylko pozorne odbicie (cel 4).
- Przełącznik oświetlenia globalnego w menu ustawień.
- Nowe modele postaci i stalkera: osobne projekty.

## Czego nie wolno zepsuć

- Cienie lamp nadal liczone na żywo. Ustawienia cieni (Niskie / Średnie / Wysokie) i `LightCuller` działają jak dotąd.
- Awaria wygląda co najmniej tak ciemno jak dziś. Odbite światło nie może „świecić” w ciemności.
- Wydajność nie spada zauważalnie przez GI. Koszt AERO jest mierzony i raportowany, a gdyby gra spadła poniżej ~60 FPS na komputerze autora, obniżamy jakość mgły (kroki raymarchingu / materiał „Mobile”).
- Fabuła, intro, pościg i finał działają bez zmian.
- AERO **nie trafia do repozytorium**, bo licencja Asset Store zabrania publicznej dystrybucji. `Assets/Mirza` zostaje w `.gitignore`.

## Podejścia (rozważone)

| | Opis | Za | Przeciw |
|---|---|---|---|
| **A (wybrane)** | APV z dwoma scenariuszami, lampy `Mixed` (Baked Indirect), mieszanie scenariuszy przy awarii | prawdziwe odbicia, tanie w grze, AERO czyta APV | wypalanie przy każdej przebudowie mapy, dane w repo |
| B | udawane GI: SSAO, słabe światła „odbicia”, odbłysk latarki | nic do wypalania, wszystko dynamiczne | wygląda sztucznie, AERO bez odbitego światła, więcej świateł przy 181 już istniejących |
| C | klasyczne lightmapy | najwyższa jakość na statycznych ścianach | bardzo długie wypalanie, ogromne tekstury ×2 (awaria), UV2 dla generowanych siatek |

## Projekt

### 1. Ustawienia URP i sceny (generator, przy każdym budowaniu)

- `PC_RPAsset` i `Mobile_RPAsset`: `Light Probe System = Adaptive Probe Volumes`, włączone *Lighting Scenarios* i *Scenario Blending*, SH L1.
- Scena dostaje własny asset Lighting Settings w `Generated/`:
  - Baked GI włączone, Realtime GI wyłączone,
  - Mixed Lighting = **Baked Indirect**,
  - Lightmapper = GPU, z CPU jako zapasem.
- Statyczna geometria poziomu i planów przerywników dostaje `ContributeGI` oraz **`Receive GI = Light Probes`**. Dzięki temu nic nie wypala się do lightmap, tylko do sond.
  - Wyjątki: obiekty przezroczyste (szkło, PCV, snopy), cząsteczki, wszystko ruchome (`HangingSway`, `IdleSway`, `SpinLight`, drzwi, zasłony, przedmioty do podniesienia), gracz i potwór.
- Jeden obiekt `Adaptive Probe Volume` w trybie globalnym obejmuje całą scenę. Minimalny odstęp sond ~1 m (gęściej przy ścianach), maksymalny według domyślnych ustawień.
- Baking Set w `Generated/` ze scenariuszami **„Prad”** i **„Awaria”**.

### 2. Które światła trafiają do wypalania

Światło jest **`Mixed`** (odbicia wypalone, bezpośrednie i cienie na żywo), gdy spełnia oba warunki:

- ma `LightCategory` z kategorią 1 albo 2,
- nie jest fabularne, migające, obrotowe ani przyczepione do czegoś ruchomego: bez `scriptedOff` w żadnym momencie gry, bez `FlickerLight`, bez `SpinLight`, nie pod graczem, potworem, przedmiotem czy notatką.

Pozostałe światła zostają `Realtime`: latarka, IR, oczy potwora, kogut bramy, czerwone światła tunelu, światła przerywników, migające świetlówki.

**Wykonawca sprawdza**, czy światła fabularne mają rozpoznawalny znacznik. Jeśli nie, dodaje jawną flagę `excludeFromBake` w `LightCategory` ustawianą przez generator tam, gdzie tworzy światło fabularne, zamiast zgadywać po nazwach.

### 3. Wypalanie: menu „Wypal oswietlenie (GI)”

1. Zapisz scenę.
2. Scenariusz „Prad”: włącz wszystkie światła `Mixed`, wypal APV.
3. Scenariusz „Awaria”: wyłącz światła `Mixed` z `powered = true` (z wyjątkiem pod `BlackoutImmune`), wypal APV.
4. Przywróć stan świateł i zapisz scenę.
5. Pokaż raport: czas wypalania i rozmiar danych.

Materiały świecące (TV, automaty) nie wysyłają światła do wypalania (zostaje `None`). Ich wygląd daje istniejące światło `LightCategory` 2.

Pliki danych APV (`*.bytes`) i Baking Set trafiają do repozytorium. Jeśli pojedynczy plik przekroczy 50 MB (ostrzeżenie GitHuba; twardy limit 100 MB), wykonawca zatrzymuje się i zgłasza to autorowi z propozycją (Git LFS albo rzadsze sondy).

Generator po przebudowie mapy usuwa stare dane wypalania (nie pasują do nowej mapy). W raporcie pisze, że trzeba uruchomić **Wypal oswietlenie (GI)**. Bez wypalonych danych gra działa jak dziś (płaski ambient).

### 4. Awaria w grze

- `Blackout.SetPower(false)`: równolegle z migotaniem (3 krótkie przełączenia) ustaw scenariusz „Awaria”. Na końcu migotania zrób krótkie zmieszanie (~0,3 s) do pełnego „Awaria”.
- `Blackout.SetPower(true)`: zmieszanie z powrotem do „Prad” (~0,5 s).
- Przyciemnianie `RenderSettings.ambientLight` zostaje jako zapas dla obiektów poza zasięgiem sond i dla sceny bez wypalonych danych.
- Kod APV ma działać też wtedy, gdy danych nie ma albo APV jest wyłączone: sprawdzenie przed wywołaniem, bez wyjątków.
- Przy starcie sceny (także po restarcie z punktu kontrolnego, klawisz R) `Blackout.Start` ustawia scenariusz „Prad”. Wypalony stan nie może „zostać” po przeładowaniu w trakcie awarii (moja decyzja).

### 5. Mgła AERO

- Uruchomić istniejący `SetupAero` na projekcie z AERO i sprawdzić, że:
  - znajduje renderer z AERO (`Settings/PC_Renderer`) i kopiuje go razem z feature oświetlenia mgły (`FullscreenCustomLightingFeature`),
  - kamera przełącza się na `PZ_AeroRenderer`,
  - suwak gęstości działa.
- Strojenie (wykonawca, z klatkami do porównania):
  - gęstość i kolor dobrane do każdej strefy (chłodnia, kuchnia, magazyn, Backrooms) w granicach możliwości jednego materiału,
  - mgła w awarii prawie niewidoczna, bo jest oświetlana tylko latarką i noktowizją,
  - jakość raymarchingu wybrana po pomiarze FPS.
- **Zabezpieczenie dla drużyny bez AERO:**
  - `AeroFog.Available` zwraca `true` tylko wtedy, gdy materiał mgły ma działający shader (`shader != null`, `isSupported`, nie shader błędu), a renderer nie ma brakujących feature'ów.
  - W przeciwnym razie kamera zostaje na domyślnym rendererze, a generator przy następnym budowaniu włącza mgłę z cząsteczek.
  - Kopia renderera z brakującym skryptem AERO w liście assetów URP nie może powodować błędów ani czarnego ekranu.
  - **Wykonawca sprawdza to w praktyce:** tymczasowo przenosi `Assets/Mirza` poza projekt, otwiera grę, zapisuje wynik i przywraca folder.

### 6. Pozorne odbicie latarki

- Nowy komponent `FlashlightBounce` na latarce: co klatkę raycast wzdłuż snopu (zasięg jak latarka, warstwy statycznej geometrii).
- W miejscu trafienia (cofnięte ~0,3 m wzdłuż normalnej) stoi punktowe światło bez cieni, `Realtime`, kategoria 0:
  - zasięg ~3 m,
  - jasność maleje z odległością trafienia,
  - kolor = kolor latarki × przybliżony kolor powierzchni (`_BaseColor` trafionego materiału, rozjaśniony).
- Wyłączone, gdy latarka zgaszona. W awarii słabsze proporcjonalnie do `Blackout.flashlightPower`.

## Jak sprawdzimy, że działa

1. Wypalanie obu scenariuszy kończy się bez błędów. Raport podaje czas i rozmiar danych.
2. Klatki „przed / po” z tych samych miejsc:
   - oświetlony pokój,
   - korytarz obok oświetlonego pokoju (ma być rozjaśniony odbiciem),
   - kąt z czerwonym automatem (podłoga łapie kolor),
   - to samo miejsce w awarii (odbicia znikają, ciemno jak dziś lub ciemniej),
   - potwór pod lampą,
   - mgła AERO pod lampą i w snopie latarki.
3. Przejście „Prad” → „Awaria” → „Prad” w trybie Play bez wyjątków.
4. Test bez AERO (punkt 5): brak błędów, gra startuje, widać mgłę z cząsteczek.
5. Pomiar FPS (średnia z ~10 s w tym samym miejscu): bez zmian / tylko GI / GI + AERO. Wyniki trafiają do raportu.
6. Pełne przejście fabuły nie jest wymagane. Wystarcza start gry, wymuszenie awarii i wejście do tunelu (pościg ze światłami fabularnymi).

## Decyzje wykonawcy (zostawione do ustalenia w trakcie)

- Dokładne parametry APV (odstępy sond, rozmiar cegieł, dylatacja, virtual offset) dobrane po pierwszym wypaleniu, tak żeby nie było przecieków światła przez cienkie ściany.
- Jasność odbić (bounce intensity / indirect multiplier na światłach) dobrana wizualnie.
- Odcień i gęstość mgły per strefa w granicach jednego materiału AERO.
- Dokładne API wypalania (`AdaptiveProbeVolumes.BakeAsync` / `Lightmapping.Bake`) według tego, co oferuje ta wersja Unity.

## Moje decyzje (do sprawdzenia przez autora)

- Wypalanie jest osobną pozycją menu, a nie częścią generatora, bo trwa minuty, a generator ma zostać szybki.
- Materiały świecące nie biorą udziału w wypalaniu. Ich światło daje istniejące źródło kategorii 2, więc nie dublujemy.
- `Assets/Mirza` zostaje poza repozytorium, a projekt musi działać bez niego.
- Plan pracy idzie od zabezpieczenia AERO (najmniejsze ryzyko, od razu widać efekt), potem GI, na końcu odbicie latarki.
