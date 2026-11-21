# The Backrooms: Isolation

Horror pierwszoosobowy zrobiony na hackathon przez drużynę **Młode Byczki**.

Trzech znajomych nagrywa film w opuszczonym magazynie. Podłoga się zapada i bohater – jego imienia nie znamy – spada do labiryntu pod spodem. Jack i Mary zostają na górze. Do tego miejsca nie dociera sygnał, a w korytarzach poluje istota, która porusza się rurami pod sufitem. Trzeba zebrać części radiostacji, skontaktować się z Agencją ECHO i uciec, zanim cię znajdzie.

## Wymagania

- **Unity 6000.6.10f1** (Unity 6), instalowane przez Unity Hub
- Git
- Windows, macOS albo Linux z kartą graficzną obsługującą URP

Projekt nie potrzebuje żadnych płatnych ani dodatkowych assetów. Tekstury i dźwięki są generowane kodem.

### Opcjonalnie: mgła AERO

Ładniejszą, wolumetryczną mgłę daje asset **AERO – Volumetric Fog and Mist** z Unity Asset Store. Nie ma go w repozytorium, bo licencja Asset Store nie pozwala go udostępniać (folder `Assets/Mirza` jest w `.gitignore`). Żeby go użyć:

1. Dodaj AERO do swojego konta w Asset Store i zaimportuj go przez **Window → Package Manager → My Assets**.
2. Zbuduj scenę ponownie: **Poza Zasiegiem → Zbuduj scene testowa (staly uklad)**. Generator sam wykryje AERO i przełączy grę na tę mgłę.

Bez AERO gra działa normalnie, tylko z prostszą mgłą z cząsteczek.

## Zagraj bez Unity (Windows)

1. Wejdź w zakładkę [**Releases**](https://github.com/FormaxPL/Gra-Hackathon-Mlode-Byczki/releases) i pobierz plik `TheBackroomsIsolation-Windows.zip`.
2. Rozpakuj całe archiwum (nie uruchamiaj gry z wnętrza ZIP-a).
3. Uruchom `TheBackroomsIsolation.exe`. Windows SmartScreen może ostrzec przed nieznanym wydawcą – wybierz **Więcej informacji → Uruchom mimo to**.

Gra startuje na pełnym ekranie. Tryb okna i rozdzielczość zmienisz w **Ustawieniach**.

## Jak zbudować grę (.exe)

1. Otwórz projekt w Unity (patrz niżej).
2. **File → Build Profiles**, wybierz **Windows** (sceny `MainMenu` i `TestLevel_A/B/C` są już na liście).
3. Kliknij **Build** i wskaż pusty folder, np. `Builds/Windows` (folder `Builds/` jest w `.gitignore`).
4. Do ZIP-a spakuj wszystko z folderu builda **oprócz** `*_BackUpThisFolder_ButDontShipItWithYourGame`.

Pierwszy build trwa kilkanaście minut (kompilacja shaderów), kolejne są dużo szybsze.

Gotowy ZIP wrzucisz na GitHuba przez **Releases → Draft a new release**: nadaj tag (np. `v1.0`), przeciągnij ZIP do pola z plikami i kliknij **Publish release**. Albo z terminala:

```bash
gh release create v1.0 TheBackroomsIsolation-Windows.zip --title "The Backrooms: Isolation v1.0"
```

## Jak uruchomić w Unity

1. Pobierz projekt:

   ```bash
   git clone https://github.com/FormaxPL/Gra-Hackathon-Mlode-Byczki.git
   ```

2. W Unity Hub wybierz **Add → Add project from disk** i wskaż pobrany folder.
3. Otwórz projekt w **Unity 6000.6.10f1**. Pierwsze otwarcie trwa kilka minut, bo Unity buduje folder `Library`.
4. Otwórz scenę `Assets/PozaZasiegiem/Scenes/MainMenu` i wciśnij **Play**. „Rozpocznij grę” losuje jedną z 3 map – przy każdym uruchomieniu inny układ pomieszczeń.

Na starcie pojawi się menu główne, a po nim intro, które można pominąć Spacją. Efekty graficzne (bloom, noktowizja, wizjer kamery) widać w oknie **Game**, a nie w Scene.

## Sterowanie

| Klawisz | Akcja |
|---|---|
| WASD + mysz | ruch |
| Shift | bieg (głośny, zużywa kondycję) |
| Ctrl / C | kucanie (ciszej, trudniej cię zauważyć) |
| E | podnieś, czytaj notatkę, schowaj się / wyjdź |
| F | latarka, a z podniesioną kamerą noktowizja |
| PPM | podnieś / opuść kamerę |
| N | noktowizja (z podniesioną kamerą) |
| Kółko myszy | zoom kamery |
| Tab / Q (trzymaj) | wykrywacz ruchu |
| LPM | gaśnica, przegania potwora do rur |
| Spacja | wstrzymanie oddechu w kryjówce, pominięcie intro |
| Esc | pauza i ustawienia |
| R | restart po śmierci |
| F1 | podgląd stanu AI potwora (debug) |
| ` (tylda) | konsola komend (`trailer`, `ambient`, `menu`, `help`) |

## Trailer

Około 29-sekundowy trailer: nagranie z kamery w korytarzu, plansze z tekstem, właz w rurze ze świecącymi oczami, potem oczami bohatera – chodzi, rozgląda się, zapada cisza i potwór wyskakuje z wentylacji. Na końcu tytuł gry i powrót do menu. Gra się na mapie A, miejsce przy włazie wybiera się samo.

Trailer odpala się komendą, na trzy sposoby:

- **w grze:** klawisz ` (tylda), wpisz `trailer` i Enter,
- **w edytorze:** **Poza Zasiegiem → Odtworz trailer** (sam wchodzi w Play), albo z wiersza poleceń:
  `Unity.exe -projectPath "<ścieżka do projektu>" -executeMethod TrailerMenu.Play`,
- **w zbudowanej grze:** uruchom ją z argumentem `-trailer`, np. `Gra.exe -trailer`.

Do nagrania najlepiej użyć okna Game w pełnym ekranie (albo zbudowanej gry) i OBS-a.

## Ambient

Przez całą grę (menu, mapy, przerywniki, trailer) leci straszne tło: niski, dudniący dron, a co kilkanaście sekund z losowej strony dochodzi stukanie i jęk rur nad sufitem, szept zza ściany, skrzypienie albo kapanie. Wszystko jest generowane kodem (`AmbientScape.cs`, `ProceduralAudio.cs`). W konsoli: `ambient off` wyłącza, `ambient on` włącza, `ambient 0.5` ustawia głośność.

## Struktura projektu

```
Assets/PozaZasiegiem/
├── Editor/LevelBuilder.cs   generator poziomu (menu „Poza Zasiegiem”)
├── Scripts/                 logika gry: gracz, AI potwora, fabuła, menu, dźwięk
├── Models/entity.fbx        model potwora
├── Shaders/                 shader snopów światła
├── Generated/               materiały tworzone przez generator
├── Scenes/MainMenu.unity    menu główne (korytarz Backrooms z potworem)
├── Scenes/TestLevel_A/B/C   trzy gotowe mapy, losowane przy starcie
└── README.md                szczegółowy opis mechanik i strojenia
```

Ważniejsze skrypty:

- `StalkerAI.cs`: zachowanie potwora (patrol, pościg, rury, zasadzki)
- `MonsterAnimationDriver.cs`: proceduralna animacja modelu potwora
- `StoryDirector.cs`: przebieg fabuły (intro, awaria prądu, ucieczka)
- `PlayerController.cs`: ruch gracza, kondycja, kucanie
- `MenuSystem.cs` i `GameSettings.cs`: menu, pauza i ustawienia (PL / EN)

## Generowanie poziomu od nowa

Mapy i menu powstają z kodu. Żeby zbudować wszystko od nowa, wybierz w górnym menu Unity:

- **Poza Zasiegiem → Zbuduj wszystkie mapy i menu**: buduje 3 mapy (`TestLevel_A/B/C`), każdą od razu wypala (GI), potem menu główne i kolejność scen w Build Settings. Trwa ok. 6–10 minut.
- **Poza Zasiegiem → Zbuduj scene testowa (staly uklad)**: tylko mapa A (szybciej, do testów).
- **Poza Zasiegiem → Zbuduj tylko menu**: samo menu (wymaga zbudowanej mapy A – kopiuje z niej potwora).

Po przebudowie mapy wypal oświetlenie globalne (odbite światło od ścian i podłóg):

- **Poza Zasiegiem → Wypal oswietlenie (GI)**: wypala dwa warianty, z prądem i w czasie awarii. Trwa około 1–3 minut. Bez tego gra działa normalnie, tylko bez odbitego światła.

Nie zmieniaj skryptów w trakcie wypalania, bo przekompilowanie je przerwie.

> **Uwaga:** generator nadpisuje sceny `TestLevel_A/B/C` i `MainMenu`. Ręczne zmiany w tych scenach przepadną, więc jeśli coś w niej układacie, najpierw zróbcie kopię sceny.

Szczegóły mechanik, strojenie potwora i rozwiązywanie problemów z grafiką są w [Assets/PozaZasiegiem/README.md](Assets/PozaZasiegiem/README.md).

## Praca w drużynie

- Przed rozpoczęciem pracy pobierzcie zmiany: `git pull`.
- **Nie edytujcie tej samej sceny w kilka osób naraz.** Pliki `.unity` źle się scalają w Gicie. Umówcie się, kto w danej chwili pracuje na której scenie, albo róbcie zmiany w osobnych scenach i prefabach.
- Commitujcie zawsze plik razem z jego `.meta`. Bez `.meta` Unity zgubi powiązania między assetami.
- Folderów `Library`, `Temp`, `Logs` i `UserSettings` nie dodajemy do repozytorium. Pomija je `.gitignore`.
