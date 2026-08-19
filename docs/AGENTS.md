# Notes for AI agents

You are probably here because someone pointed you at this repository and asked
you to change something. This file is the shortest path to being useful without
breaking things.

Read [ARCHITECTURE.md](ARCHITECTURE.md) next. It is longer but it is where the
reasoning lives.

---

## The one-paragraph orientation

Unity 2022.3.62f1, 2D, URP. A tower-defense/Tetris hybrid reimplementing the
Douyin mini-game 关关难不住. Data comes from JSON tables extracted from the
original (`Assets/StreamingAssets/GameConfig`), loaded by
`Skyscraper.Config.ConfigDB`. The battle lives in `Skyscraper.Battle`, driven by
`BattleRuntime`. The scene is *generated* by an editor script, not
hand-authored. Art is gitignored and the code degrades gracefully without it.

## Rules that will save you an hour

### 1. The scene is generated — do not hand-edit it

`Assets/Scenes/Battle.unity` is produced by
`Assets/Editor/BattleSceneBuilder.cs` (menu: **Skyscraper → Build Battle
Scene**). `BattleSceneAutoBuild.cs` is `[InitializeOnLoad]` and rebuilds it
after a fresh import.

So: **changes you make to the scene by hand will be silently discarded.** If
you need a different scene, change the builder. This is deliberate — it makes
the scene reviewable as code.

### 2. Namespaces do not follow the folders

Everything under `Scripts/Battle/**` is namespace `Skyscraper.Battle` — flat,
regardless of subfolder. Config types are `Skyscraper.Config`. Notably
`ConfigDB` and all the `*Row` types are `Skyscraper.Config`, *not*
`Skyscraper.Battle`, even when only the battle uses them.

Do not guess. `grep -rn "class TypeName" Assets/Scripts`.

### 3. Some members are fields, some are properties

This bites reflection code specifically. `BrickShape.CellsWide` and `CellsHigh`
are **fields**. `BrickHeroRow.ID` is a **field**. But `BattleRuntime.Base`,
`TopY`, `Ctx` are **properties**, and `BattleRuntime.BrickRoot` is a **field**.

If you write reflection, always try property *then* field, or you will get a
silent `null` followed by a `NullReferenceException` one line later:

```csharp
var FL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
Func<object, string, object> M = (o, n) => {
    if (o == null) return null;
    var pi = o.GetType().GetProperty(n, FL);
    if (pi != null) return pi.GetValue(o);
    var fi = o.GetType().GetField(n, FL);
    return fi != null ? fi.GetValue(o) : null;
};
```

### 4. Never trust a probe you ran before recompiling

This is the single most expensive trap in this repository. Unity does not
recompile the instant you save a file. If you edit a script and immediately read
a value back through reflection or a probe, **you are reading the old
assembly** and it will look like your change did nothing.

Always force and wait:

```csharp
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
return "requested";
```

then poll until `EditorApplication.isCompiling` is false, *then* probe. And
verify the new code actually landed by checking a member exists that only exists
after your edit — "compiled successfully" and "the new code is loaded" are
different claims.

### 5. Numbers in this codebase are measurements, not preferences

`RefScale.cs` is a table of pixel measurements taken off reference screenshots
of the original at 1125×2436, plus the conversions into world units. When you
change a size, you are asserting something about the original. Say where the
number came from in the commit message.

Do not "clean up" a constant that looks arbitrary. `FromSource = 3.125` and
`CellPx = 36` look arbitrary and are not.

### 6. Art is optional and its absence is not a bug

`Assets/Resources/Bricks/` and `Assets/Resources/Figures/` are gitignored.
`Figures.BuildHero`/`BuildEnemy` return `null` when the sprite is missing, and
`Prefabs.cs` (`:121`, `:150`, `:187`) falls back to procedural shapes. Flat
coloured polygons in play mode means the art is absent, not that something
broke. See [ART.md](ART.md).

### 7. Do not add new brick shapes

The sixteen footprints in `BrickShape.ByCube` were transcribed off the original's
atlases and cross-checked two ways. Nothing exceeds 3 cells in either direction
or 5 cells total. A "reasonable-looking" 4-long bar or a novel L is **not in the
game** and adding one is a regression. This has been gotten wrong before by
reading a low-resolution screenshot.

## Verifying your work

There is a `unity-probe` subagent defined in the *driving* workspace (not in
this repo) that automates this. Without it, the manual loop is:

1. Edit the script.
2. Force recompile, poll `isCompiling`.
3. Check `LogEntries.GetCountsByType` for errors — expect **0 errors**. There
   are 4 pre-existing harmless warnings: `The referenced script (Unknown) on
   this Behaviour is missing!`
4. Confirm your new member exists via reflection.
5. Enter play mode (`EditorApplication.isPlaying = true` — this is
   **asynchronous**, it takes effect on the *next* call, do not wait for it in
   the same snippet).
6. Sample. Prefer several samples over time to one point; it is the only way to
   see whether a value is trending.
7. Exit play mode, **then check the console again** — runtime exceptions are
   only visible after.
8. Delete every temporary script you created.

`BattleProbe` (`Scripts/Battle/BattleProbe.cs`) writes a sampled report to
`BattleProbe.txt` at the project root, which is a good starting point for
"what is the battle actually doing".

## Useful reflection entry points

`BattleRuntime` is the hub. `FindObjectOfType` it and read:

| Member | Kind | Meaning |
|---|---|---|
| `Ctx` | prop | `BattleContext` — gold, wave, player state |
| `Bricks` | prop | `IReadOnlyList<BrickUnit>` placed bricks |
| `AliveEnemies` | prop | count |
| `Base` / `BaseHp` | prop | pedestal + its health |
| `TowerTopY` / `TowerMetres` | prop | stack height |
| `HeightAttackMul` | prop | the height→attack bonus |
| `DropLineY` | prop | the white line you must drop above |
| `BrickRoot` / `EnemyRoot` | **field** | scene parents |
| `OnLog` | field | `Action<string>`; hook it to capture the battle log |

Write a private auto-property via its backing field:
`type.GetField("<Gold>k__BackingField", FL).SetValue(obj, 9999)`.

`ConfigDB.LoadAny()` must have completed before any table access; after a domain
reload the static lists are `null` again. Guard for it.

**Never call `ConfigDB.LoadAll()` from runtime code.** It is synchronous
`File.ReadAllText`, which works in the Editor and fails on Android, where
StreamingAssets is a `jar:file:` URL inside the APK. Use the coroutine
`ConfigDB.LoadAny(err => …)`, which dispatches on
`ConfigDB.StreamingAssetsIsFile`. This exact mistake shipped an APK that booted
to a blank screen with no visible error — see
[DATA.md](DATA.md#on-android-streamingassets-is-not-a-directory).

## Geometry, precomputed

Do not re-derive these.

| Constant | Value | Where |
|---|---|---|
| `BrickShape.CellSize` | `0.8` world units | cell edge |
| `Shapes.PPU` | `100` | procedural sprite pixels-per-unit |
| Procedural sprite source | 64 px | so `StretchTo` factor is `100/64 = 1.5625` |
| **world size** | `localScale ÷ 1.5625` | for procedural art |
| `RefScale.CellPx` | `36` px | cell in reference screenshots |
| `RefScale.WorldPerPx` | `0.8/36` | reference px → world |
| `RefScale.ViewWidth` | `25` world units | camera width |
| `RefScale.FromSource` | `3.125` | source-view → our view |
| Spine rig scale | `0.01` | all 35 rigs |

IMGUI's y axis points **down**; world and grid y point **up**. Convert before
comparing coordinates.

## Things that are correct but look wrong

- `BrickShape.Oriented`'s doc comment says "clockwise". The transform is
  **counter-clockwise** in centred coordinates. The code is right, the comment
  is wrong. Do not "fix" the code to match the comment. See
  [ARCHITECTURE.md § the orientation trap](ARCHITECTURE.md#the-orientation-trap).
- `RefScale.EnemyMeasuredWidth = 0.64f` is unused by design; it is kept as the
  documented measurement it came from.
- Two `BrickShape` entries can hold identical footprints (e.g. `break7` and
  `break15`, both 2×2). That is intentional — `break_N` is also the *sprite*
  name, so each cube keeps its own authored layout.
- `Figures.HeroAnchor` short-circuits on an exact orientation match and
  rigid-maps the nearest authored one otherwise. Averaging the authored offsets
  instead is wrong and was tried; see ARCHITECTURE.md.

## Do not

- Hand-edit `Battle.unity` (regenerated).
- Commit `Assets/Resources/Bricks` or `Assets/Resources/Figures` (copyrighted;
  gitignored).
- Commit `Library/`, `Temp/`, `obj/`, `*.csproj`, `*.sln` (all regenerated; the
  `.gitignore` covers them).
- Add brick shapes outside the sixteen.
- Trust a measurement taken before recompilation finished.
