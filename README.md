# km – prototyp budovatelské strategie ve stylu Knights and Merchants

Godot 4.7 (.NET / C#). Veškerá grafika se generuje procedurálně v kódu, projekt nemá žádné externí assety.

## Spuštění

**Hotová hra:** `build/windows/KM.exe`, nebo zabalená v `build/KM-windows-x64.zip`. Složka `data_km_windows_x86_64` musí ležet vedle exe, je v ní .NET runtime.

**Z editoru:** otevři projekt v Godotu 4.7 (mono) a spusť ho (F5).

**Nový build exe:**

```
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --export-release "Windows Desktop" build/windows/KM.exe
```

Export potřebuje exportní šablony 4.7.2 mono. Stačí ty pro Windows x64 v `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`, případně je nainstaluj přes editor (Editor → Manage Export Templates).

## Menu a nastavení

- **Hlavní menu** běží nad živou vesnicí v pozadí. Nabízí novou hru (semínko mapy, volitelně odkrytou mapu), nastavení a konec.
- **Esc ve hře** otevře herní menu: pokračovat, nastavení, hlavní menu, konec.
- **Nastavení** se ukládá do `user://settings.cfg`, tedy `%APPDATA%\Godot\app_userdata\KM\`:
  - režim zobrazení (okno / celá obrazovka bez okrajů / exkluzivní) a rozlišení okna,
  - VSync, limit FPS, vyhlazování hran (MSAA), barevné doladění,
  - velikost rozhraní, posun kamery u okraje obrazovky a rychlost posunu kamery.

## Ovládání

| Vstup | Akce |
|---|---|
| WASD / šipky, okraj obrazovky, prostřední tlačítko + tažení | posun kamery |
| kolečko | zoom ke kurzoru |
| levé tlačítko | výběr budovy / položení stavby |
| pravé tlačítko | zrušit nástroj |
| Esc | zrušit nástroj, jinak herní menu |
| R | silnice (táhnout myší) |
| 1–4 | sklad, dřevorubec, pila, kamenolom |
| X | bourání |
| mezerník | pauza |
| + / − | rychlost hry |
| P | zobrazit cesty jednotek |

## Co prototyp umí

- **Pohled jako v KaM**: čtvercová mřížka viděná šikmo shora. Výška terénu posouvá body nahoru, takže je vidět reliéf se stínováním svahů.
- **Terén**: shader míchá 4 procedurální textury (tráva, hlína, skála, písek) s nepravidelnými okraji.
  - Přidává velkoplošnou variaci barvy a putující stíny mraků.
  - Voda má hloubku, odlesky a pěnu u břehu.
  - Pod budovou se terén srovná.
- **Mlha (fog of war)**: neprozkoumaná krajina je černá s měkkým okrajem a odkrývají ji lidé a budovy. Stavět jde jen na prozkoumaném území.
- **Budovy**: fasáda čelem k divákovi, vchod dole, různé materiály.
  - Kamenný sklad s taškovou střechou.
  - Roubená dřevorubecká chalupa s doškovou střechou.
  - Hrázděná pila s komínem, který při práci kouří.
  - Kamenný kamenolom s břidlicovou střechou.
  - Všechny mají dveře, okna s okenicemi, vývěsní štít, stín a zásoby před domem. Během stavby je vidět lešení a krov.
- **UI**: dřevěné panely ve stylu KaM se systémovým patkovým písmem.
  - Nahoře jsou suroviny, lidé a rychlost hry.
  - Dole je minimapa s mlhou (kliknutím nebo tažením přesune kameru), stavební tlačítka s miniaturami a cenou a panel vybrané budovy.
- **Ekonomika**: sklad → dřevorubec (kmeny) → pila (1 kmen = 2 prkna), kamenolom (kámen).
  - Nosiči rozvážejí materiál podle centrálního dispečera s rezervacemi.
  - Stavitelé nejdřív připraví staveniště a pak staví z dovezeného materiálu.
- **Hledání cest**: A* (`AStarGrid2D`) s 8 směry bez řezání rohů a s váhami terénu. Silnice jsou nejlevnější a rychlejší, les a skály zpomalují. Jednotky se navzájem neblokují a při zablokování cestu přepočítají.

## Struktura

```
App.cs                      kořen aplikace: hlavní menu ↔ hra, nastavení, snímky/ikona z příkazové řádky
Main.cs                     jedna hra (nebo pozadí menu): mapa, objekty, vstup
scripts/Core/               Iso (projekce), GameMap (mapa, A*, mlha), CameraRig, Settings, DevTools
scripts/World/              budovy, definice, suroviny, stromy/kameny, Economy (dispečer)
scripts/Units/              Unit (fronta akcí + kreslení), Serf, Laborer, Worker, akce
scripts/Rendering/          TerrainView, FogLayer, RoadLayer, BuildingPainter, ArtFactory, TerrainArt, PostFx…
scripts/UI/                 Hud, Minimap, MainMenu, PauseMenu, SettingsPanel, UiArt (téma), IconPainter…
```

## Ladění z příkazové řádky

Parametry se píšou za `--`. Fungují v editorové verzi i v exportovaném exe:

```
Godot..._console.exe --path . -- --seed=42 --demo --timescale=5 --paths --screenshot=shot.png --shot-after=20
```

| Přepínač | Co dělá |
|---|---|
| `--seed=N`, `--demo`, `--play` | rovnou spustí hru bez menu |
| `--demo` | naplánuje dřevorubce, pilu, kamenolom a silnice |
| `--reveal` | odkrytá mapa |
| `--timescale=N`, `--zoom=N` | rychlost simulace a zoom |
| `--paths`, `--select` | zobrazí cesty, vybere poslední budovu |
| `--pause`, `--settings` | otevře herní menu nebo rovnou nastavení |
| `--screenshot=a.png --shot-after=N` | uloží snímek a ukončí se |
| `--make-icon=icon.png` | vygeneruje ikonu aplikace (`icon.png` + `icon.ico`) |
