# km – prototyp budovatelské strategie ve stylu Knights and Merchants

Godot 4.7 (.NET / C#). Veškerá grafika se generuje procedurálně v kódu, projekt nemá žádné externí assety.

## Spuštění

Otevři projekt v Godotu 4.7 (mono) a spusť ho (F5). Případně z příkazové řádky:

```
dotnet build
Godot_v4.7.2-stable_mono_win64.exe --path .
```

## Ovládání

| Vstup | Akce |
|---|---|
| WASD / šipky, prostřední tlačítko + tažení | posun kamery |
| kolečko | zoom ke kurzoru |
| levé tlačítko | výběr budovy / položení stavby |
| pravé tlačítko / Esc | zrušit nástroj |
| R | silnice (táhnout myší) |
| 1–4 | sklad, dřevorubec, pila, kamenolom |
| X | bourání |
| mezerník | pauza |
| + / − | rychlost hry |
| P | zobrazit cesty jednotek |

## Co prototyp umí

- **Terén**: jeden mesh s výškami ve vrcholech a stínováním svahů.
  - Shader míchá 4 procedurální textury (tráva, hlína, skála, písek) s nepravidelnými okraji.
  - Přidává velkoplošnou barevnou variaci a putující stíny mraků.
  - Voda má hloubku, odlesky a pěnu u břehu.
  - Pod budovou se terén při kopání srovná.
- **Grafika**: budovy vrhají stín a mají taškové střechy, stromy se kývají ve větru. Celý obraz má jemné barevné doladění a vinětaci.
- **UI**: dřevěné panely ve stylu KaM (procedurální 9-patch textury, systémové patkové písmo).
  - Nahoře jsou suroviny, lidé a rychlost hry.
  - Dole je minimapa (kliknutím nebo tažením přesune kameru), stavební tlačítka s miniaturami budov a cenou a panel vybrané budovy.
- **Ekonomika**: sklad → dřevorubec (kmeny) → pila (1 kmen = 2 prkna), kamenolom (kámen).
  - Nosiči rozvážejí materiál podle centrálního dispečera s rezervacemi.
  - Stavitelé nejdřív připraví staveniště a pak staví z dovezeného materiálu.
  - Řemeslník se po dokončení budovy objeví u skladu a dojde do práce.
- **Hledání cest**: A* (`AStarGrid2D`) s 8 směry bez řezání rohů a s váhami terénu. Silnice jsou nejlevnější a chodí se po nich rychleji, les a skály zpomalují. Jednotky se navzájem neblokují, takže nevznikají zácpy jako v originále. Když se cesta za chůze zablokuje (nová budova), jednotka ji přepočítá.

## Struktura

```
Main.cs                     kořen hry, vstup, spawn objektů
scripts/Core/               Iso (projekce), GameMap (data mapy + A*), CameraRig, DevTools
scripts/World/              budovy, definice, suroviny, stromy/kameny, Economy (dispečer)
scripts/Units/              Unit (fronta akcí + kreslení), Serf, Laborer, Worker, akce
scripts/Rendering/          TerrainView, RoadLayer, BuildingPainter, ArtFactory, Overlay
scripts/Rendering/          … TerrainArt + PixelCanvas (procedurální textury), PostFx
scripts/UI/                 Hud, Minimap, BuildButton, IconPainter, UiArt (téma a textury UI)
```

Izometrii řeší jen `Iso.cs` (dlaždice 64×32). Kdo by chtěl pohled víc jako původní KaM (čtvercová mřížka při pohledu šikmo shora), stačí změnit `ToScreen`/`ToGrid`.

## Ladění z příkazové řádky

Parametry se píšou za `--`:

```
Godot..._console.exe --path . -- --seed=42 --demo --timescale=5 --paths --screenshot=shot.png --shot-after=20
```

`--demo` sám naplánuje dřevorubce, pilu, kamenolom a silnice k nim. `--screenshot` uloží snímek a hru ukončí.
