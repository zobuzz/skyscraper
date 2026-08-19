# Config data

28 JSON tables in `Assets/StreamingAssets/GameConfig/`, extracted from the
original game. `Skyscraper.Config.ConfigDB` loads them; `ConfigTypes.cs` holds
the row types; `CompoundParsers.cs` handles the packed string columns.

**Provenance note.** These are the original's tables verbatim, including hero
names, skill text and every balance number. They are here because nothing runs
without them, but they are not this project's to license. See the licence
section of the [README](../README.md).

---

## Loading

```csharp
yield return ConfigDB.LoadAny(err => …);   // always correct — use this
```

`LoadAny` picks a path from `ConfigDB.StreamingAssetsIsFile` and is the only
call sites should need. The two it dispatches to are still public:
`LoadAll()` (synchronous, `File.ReadAllText`) and `LoadAllAsync()` (coroutine,
`UnityWebRequest`).

### On Android, StreamingAssets is not a directory

This is the trap. It cost a blank-screen APK once already, so it is worth
stating plainly:

- In the Editor and on desktop, `Application.streamingAssetsPath` is a real
  folder and `File.ReadAllText` works.
- **On Android the files stay compressed inside the APK** and the path is a URL
  — `jar:file:///data/app/…/base.apk!/assets/…`. `File` cannot open that. It
  fails with `Could not find a part of the path`. WebGL is a URL for the same
  reason.

So `LoadAll()` **works perfectly in the Editor and fails on device**, which is
the worst possible failure shape: everything looks correct until the APK boots.
And the symptom is not an obvious crash — `BattleRuntime` catches the error and
disables itself, so the player renders the camera's clear colour and *nothing
else*. An empty screen, no visible error unless you read logcat.

If an APK boots to a flat colour, check logcat for `[Battle] config load failed`
before suspecting the scene, the camera or the shaders.

Note that `StreamingAssetsIsFile` tests the path for `"://"` rather than using
`#if UNITY_ANDROID`. The URL scheme is the property that actually matters —
platform is only a proxy for it.

### Domain reload

After a **domain reload every static list is `null` again** — editor code and
probes must reload and guard.

Accessors: `ConfigDB.Hero(id)`, `.Enemy(id)`, `.Map(id)`,
`.HeroLevel(heroId, lv)`, `.ScenWaves(scene, hard)`, plus the raw `List<>`s and
`*ById` dictionaries.

## The tables

Row counts measured from the files, not estimated.

### Used by the battle

| Table | Rows | Row type | What it drives |
|---|---:|---|---|
| `BrickHero.json` | 16 | `BrickHeroRow` | the sixteen heroes: `Shape` (→ footprint), `Cost`, `Interval`, `SkillType`, `MergeSkin`, `DmgRadius`, crit |
| `BrickHeroLevel.json` | 480 | `BrickHeroLevelRow` | per-hero per-level `Attack`, `Gold`, `Attrs` (16 heroes × 30 levels) |
| `BrickEnemy.json` | 26 | `BrickEnemyRow` | monster defs: `Model` (→ art), `Scale`, `IsBoss`, `MoveY`, `Interval` |
| `BrickMonster.json` | 6962 | `BrickMonsterRow` | wave composition per scene — the big one |
| `BrickMonsterB.json` | 3242 | `BrickMonsterRow` | hard-mode waves |
| `BrickMap.json` | 233 | `BrickMapRow` | levels: `Title`, backgrounds, `MapColor`, `Desk`, gold, challenges |
| `BrickMapB.json` | 183 | `BrickMapRow` | hard-mode maps |
| `BrickCard.json` | 30 | `BrickCardRow` | the relic/buff cards: `Quality`, `Weight`, `Attr` |
| `BrickCardLevel.json` | 20 | `BrickCardLevelRow` | card upgrade costs |
| `ChallengeAttr.json` | 7 | `ChallengeAttrRow` | the challenge modifiers |
| `Global.json` | 1 | `GlobalRow` | global tuning; also `ChallAntiAirHeight`, `ChallGazeHeight` |
| `StarBonus.json` | 101 | `StarBonusRow` | star-count reward thresholds |
| `Item.json` | 185 | `ItemRow` | items and currencies |

### Present, not yet wired

`BattlePass.json` (30), `BrickCardFormat.json` (5), `DailyShop.json` (64),
`ErrorCode.json` (21), `Guide.json` (6), `Rewards.json` (11), `SignIn.json` (1),
`SoundVol.json` (67), `Task.json` (10), `TaskReward.json` (2), and the five
localisation tables `Lang_{CN,EN,HK,HN,JPN}.json` (7 rows each).

These belong to the meta layer — battle pass, daily shop, sign-in, tasks — which
is not implemented. They are kept so that when it is, the values are the
original's.

## Packed string columns

Several columns pack structure into a string. `CompoundParsers.cs` handles them,
and its doc comments are the authority:

| Shape | Example | Parser |
|---|---|---|
| attribute mods | `"AttackRate,0.03,0.03"` → base 0.03, per-level 0.03 | `Parse.Attrs` |
| | `"AttackRateB,+15%"` → base 0.15, per-level 0 | |
| | multiple joined with `\|` | |
| percentages | `"+15%"` → 0.15, `"-50%"` → −0.5 | `Parse.Percent` |
| reward lists | `"1,100\|2,100"` → item 1 ×100, item 2 ×100 | `Parse.Rewards` |
| int lists | `"100\|60"` → `[100, 60]` | `Parse.Ints` |
| float lists | `"80\|15\|5"` | `Parse.Floats` |

`AttrId` covers the **full observed vocabulary** — 7 card attrs, 11 hero-level
attrs. Unknown strings fall through to `AttrId.None` rather than throwing, so a
future table version with a new column degrades instead of crashing.

## Quirks that are data, not bugs

- **`Monser_8`** — `BrickEnemy.Model` for that monster is misspelled in the
  original data. `Figures.EnemyPath` reproduces the misspelling on purpose.
  There is no `Monster_17`.
- **`BrickHero.Shape` is opaque.** It is `"Cube_1".."Cube_16"` and carries no
  geometry; the footprint comes from the art. See
  [ARCHITECTURE.md § brick shapes](ARCHITECTURE.md#brick-shapes).
- **`MergeSkin` ≠ `ID`.** Several heroes share a `MergeSkin`, which is what lets
  different heroes merge with each other.
- **The height→attack ruler is *not* in these tables.** All 28 were swept; the
  only `Height` columns are the two challenge ones in `Global`. `HeightBonus.cs`
  reconstructs the bands from reference screenshots and says so in its own doc
  comment. It is the one system in the project that is reconstructed rather than
  extracted.

## Validating

**Skyscraper → Validate Config Tables** (`BattleSceneBuilder.cs:94`)
cross-checks the JSON against the row types. Run it after touching either side.
