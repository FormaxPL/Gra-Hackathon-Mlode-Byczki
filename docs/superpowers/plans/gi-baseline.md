# GI i mgła – pomiary

Pomiary w edytorze (okno Game 1209×674), nie w buildzie. Kamera w ujęciu `lit_room` (gi-shots.json), AI, menu i fabuła wyłączone.

## Przed zmianami (2026-11-21)

| Ujęcie | Średnia jasność (luma 0–1) |
|---|---|
| lit_room | 0.0269 |
| corridor_next_to_lit | 0.0055 |
| vending_corner | 0.1314 |
| stalker_under_lamp | 0.0265 |
| fog_under_lamp | 0.0154 |
| lit_room w awarii | 0.0057 |

FPS (lit_room, ~10 s): **185.9**

## Po włączeniu mgły AERO (bez GI)

| Ujęcie | Luma |
|---|---|
| lit_room | 0.0575 |
| corridor_next_to_lit | 0.0042 |
| vending_corner | 0.2059 |
| stalker_under_lamp | 0.0542 |
| fog_under_lamp | 0.0523 |
| lit_room w awarii | 0.0019 |
| lit_room bez AERO (projekt bez assetu) | 0.0253 |

FPS (lit_room, ~10 s): **128.7** (przed: 185.9). Uwaga: `before_stalker_under_lamp.png` odtworzone później z wyłączoną mgłą – w pierwszym przebiegu model potwora był ukryty.

## Po zmianach: GI (APV, odbicia 8×) + mgła AERO (gęstość 0.12) + odbłysk latarki

| Ujęcie | Przed | Po |
|---|---|---|
| lit_room | 0.0269 | 0.0682 |
| corridor_next_to_lit | 0.0055 | 0.0050 |
| vending_corner | 0.1314 | 0.2610 |
| stalker_under_lamp | 0.0259 | 0.0651 |
| fog_under_lamp | 0.0154 | 0.0440 |
| lit_room w awarii | 0.0057 | 0.0031 |

FPS (lit_room, ~10 s): przed **185.9**, GI + AERO **170.2**, GI bez AERO **174.5**.
Wypalanie: ok. 35–40 s na scenariusz, dane 13.3 MB (największy plik 3.1 MB).

Uwagi:
- Korytarz za ścianą nie dostaje odbić (musiałyby odbić się kilka razy przez drzwi). Ciemniejszy niż przed, bo płaski ambient zastąpiły sondy.
- Pierwsze strojenie mgły (0.25) było bez latarki. Z latarką obraz zalewała poświata, stąd 0.12 (`after_flashlight_wall.png` / `before_flashlight_wall.png`).
