# Poza Zasięgiem

Horror pierwszoosobowy zrobiony na hackathon przez drużynę **Młode Byczki**.

Trzech znajomych nagrywa film w opuszczonym magazynie. Podłoga się zapada, a Marek spada do labiryntu pod spodem. Do tego miejsca nie dociera sygnał, a w korytarzach poluje na niego istota, która porusza się rurami pod sufitem. Trzeba zebrać części radiostacji, skontaktować się z Agencją ECHO i uciec, zanim cię znajdzie.

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

## Jak uruchomić

1. Pobierz projekt:

   ```bash
   git clone https://github.com/FormaxPL/Gra-Hackathon-Mlode-Byczki.git
   ```

2. W Unity Hub wybierz **Add → Add project from disk** i wskaż pobrany folder.
3. Otwórz projekt w **Unity 6000.6.10f1**. Pierwsze otwarcie trwa kilka minut, bo Unity buduje folder `Library`.
4. Otwórz scenę `Assets/PozaZasiegiem/Scenes/TestLevel` i wciśnij **Play**.

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

## Struktura projektu

```
Assets/PozaZasiegiem/
├── Editor/LevelBuilder.cs   generator poziomu (menu „Poza Zasiegiem”)
├── Scripts/                 logika gry: gracz, AI potwora, fabuła, menu, dźwięk
├── Models/entity.fbx        model potwora
├── Shaders/                 shader snopów światła
├── Generated/               materiały tworzone przez generator
├── Scenes/TestLevel.unity   gotowa scena gry
└── README.md                szczegółowy opis mechanik i strojenia
```

Ważniejsze skrypty:

- `StalkerAI.cs`: zachowanie potwora (patrol, pościg, rury, zasadzki)
- `MonsterAnimationDriver.cs`: proceduralna animacja modelu potwora
- `StoryDirector.cs`: przebieg fabuły (intro, awaria prądu, ucieczka)
- `PlayerController.cs`: ruch gracza, kondycja, kucanie
- `MenuSystem.cs` i `GameSettings.cs`: menu, pauza i ustawienia (PL / EN)

## Generowanie poziomu od nowa

Scena `TestLevel` powstaje z kodu. Żeby ją zbudować ponownie, wybierz w górnym menu Unity:

- **Poza Zasiegiem → Zbuduj scene testowa (staly uklad)**: zawsze ta sama mapa
- **Poza Zasiegiem → Zbuduj scene testowa (losowy uklad)**: nowa, losowa mapa

> **Uwaga:** generator nadpisuje `TestLevel.unity`. Ręczne zmiany w tej scenie przepadną, więc jeśli coś w niej układacie, najpierw zróbcie kopię sceny.

Szczegóły mechanik, strojenie potwora i rozwiązywanie problemów z grafiką są w [Assets/PozaZasiegiem/README.md](Assets/PozaZasiegiem/README.md).

## Praca w drużynie

- Przed rozpoczęciem pracy pobierzcie zmiany: `git pull`.
- **Nie edytujcie tej samej sceny w kilka osób naraz.** Pliki `.unity` źle się scalają w Gicie. Umówcie się, kto w danej chwili pracuje na `TestLevel`, albo róbcie zmiany w osobnych scenach i prefabach.
- Commitujcie zawsze plik razem z jego `.meta`. Bez `.meta` Unity zgubi powiązania między assetami.
- Folderów `Library`, `Temp`, `Logs` i `UserSettings` nie dodajemy do repozytorium. Pomija je `.gitignore`.
