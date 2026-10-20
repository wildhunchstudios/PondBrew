# UI fonts (UX-02)

| File | Family / weight | Source | Licence |
|---|---|---|---|
| `Baloo2-ExtraBold.ttf` | Baloo 2, wght 800 | `ofl/baloo2/Baloo2[wght].ttf` from https://github.com/google/fonts (main branch, fetched 2026-10-19) | SIL Open Font License 1.1, see `OFL-Baloo2.txt` |
| `Nunito-SemiBold.ttf` | Nunito, wght 600 | `ofl/nunito/Nunito[wght].ttf` from https://github.com/google/fonts (main branch, fetched 2026-10-19) | SIL Open Font License 1.1, see `OFL-Nunito.txt` |
| `Nunito-ExtraBold.ttf` | Nunito, wght 800 | same as above | SIL Open Font License 1.1, see `OFL-Nunito.txt` |

The static files were instanced from the variable fonts with fontTools 4.66.1 (`varLib.instancer`, original names kept) because Unity's legacy font importer uses only a variable font's default instance. No glyphs were edited. Keep the OFL texts beside the fonts and list both families in the game credits (R11.3).
