# Architecture

How the pieces fit, and — more usefully — where the non-obvious decisions are
and why they went the way they did. If you are about to change something here,
the reasoning matters more than the structure.

---

## The loop

```
ConfigDB.LoadAll()          reads 28 JSON tables off StreamingAssets
        │
BattleSceneBuilder          generates the scene from the tables
        │
BattleRuntime.StartBattle(sceneId, hard)
        │
        ├── BasePlatform      the pedestal: 8 tiles, 6.4 world units wide
        ├── BaseHealth        HP + the bar
        ├── BrickDropper      deals 3 cards, handles drag/rotate/drop
        ├── MergeSystem       watches for touching same-group bricks
        ├── HeightBonus       tower height -> global attack multiplier
        └── wave scheduler    BrickMonster rows, per scene
```

Each frame, placed `BrickUnit`s fire at the nearest `EnemyUnit` on their own
interval; `EnemyUnit`s walk toward the pedestal and attack it on arrival; kills
pay gold into `BattleContext`; gold buys cards.

`BattleRuntime` is the hub. Anything that needs to find anything else goes
through it, which keeps the object graph shallow and makes reflection-based
probing practical.

## Data flow

`Skyscraper.Config.ConfigDB` is a static holder. `LoadAll()` reads from
`Application.streamingAssetsPath` synchronously (editor/desktop);
`LoadAllAsync()` exists for Android/WebGL where StreamingAssets is not a real
directory. See [DATA.md](DATA.md) for the tables.

Two things to know:

- **After a domain reload the static lists are `null` again.** Editor code and
  probes must call `LoadAll()` and guard.
- **`ConfigDB`, not `Tables`.** The type is in namespace `Skyscraper.Config`.

## Geometry: why the constants are what they are

`RefScale.cs` exists so that no size in this project is a matter of taste. It
holds pixel measurements taken off reference screenshots of the original at
1125×2436, and the conversions out of them:

```
CellPx     = 36                    a brick cell in the reference, in px
CellSize   = 0.8                   the same cell, in world units
WorldPerPx = CellSize / CellPx     the conversion
ViewWidth  = 1125 * WorldPerPx     = 25 world units of camera width
FromSource = ViewWidth / 8         = 3.125
```

Everything else in that file — card sizes, HP bar, ruler, every font size — is
the same kind of measurement. **Changing one is a claim about the original.**
When you change one, put the measurement in the commit message.

For procedural art, `Shapes.PPU = 100` and the source sprites are 64 px, so
`StretchTo`'s factor is `100/64 = 1.5625` and therefore:

```
world size = measured localScale / 1.5625
```

Note also: `Sprite.bounds` shrinks to the *tight* mesh and will under-report.
Use `sprite.rect.width / sprite.pixelsPerUnit` when you want the full cell.

## Brick shapes

`BrickShape` is a set of unit cells on an integer grid, plus an orientation.

The tables do not describe the geometry — `BrickHero.Shape` is just
`"Cube_1".."Cube_16"`. The *art* supplies what those point at, and two separate
atlases agree on all sixteen (`Cube_N` in the tower atlas, `break1..break16` in
the battle atlas). So the hero fixes the shape, and `BrickShape.ByCube` is that
correspondence transcribed off the art — with the sprite pixel dimensions
recorded in a comment so the transcription is checkable.

**The sixteen are seven distinct footprints up to rotation and reflection:**
`I3`, `S4`, `J4`, `T4`, `O4`, `O1`, `X5`. Nothing exceeds 3 cells in either
direction; nothing exceeds 5 cells total. No domino, no 4-in-a-row, nothing 4
wide.

Two independent checks that this is transcribed and not guessed:

1. Every `break_N` sprite is an exact multiple of 100 px per cell once the
   256 px atlas cap is undone (the capped ones are uniformly scaled by 0.853).
2. `BrickHero.MergeSkin` partitions the sixteen heroes into exactly these seven
   classes — a table column agreeing with an art measurement.

Duplicated footprints (`break7`/`break15` both 2×2; `break12`/`break1` both
`I3`) are kept as separate entries on purpose: `break_N` is also the name of the
*sprite*, so holding each cube's own authored layout means the sprite can be
pasted on with no correction beyond the rolled orientation.

### The orientation trap

`BrickShape.Oriented(quarters, flip)` mirrors about x, then applies this cell
map once per quarter:

```csharp
cells[i] = new Vector2Int(maxY - cells[i].y, cells[i].x);
```

**Its doc comment says "clockwise". It is counter-clockwise.** In centred
coordinates — which is what `CellCenter` returns and what all the anchor offsets
are expressed in — that map is `(X,Y) → (−Y, X)`, a counter-clockwise quarter.

So:

```
Oriented  = rotCCW^q ∘ flipx^f
Unorient  = flipx^f  ∘ rotCW^q
```

This got derived the wrong way round once already, and the failure is *silent*:
the wrong inverse still lands a hero somewhere on the brick, just not on the
right cell. It was settled by measurement, not reasoning — pulling all four of a
hero's hand-placed offsets back through each candidate, the correct convention
collapses them to within 0.11–0.55 cells and the wrong one scatters them to
0.43–1.1.

The comment on `Oriented` is still wrong and should be fixed. Do not fix the
*code* to match it.

Related identity, used by `Compose` and by `HeroAnchor`'s cost function:
`F·R^q = R^−q·F`, hence the negated quarter count when `Flip` is set.

### Roll deals all eight transforms

`BrickShape.Roll(row)` is `For(row).Oriented(Random.Range(0,4), Random.Range(0,2)==0)`
— uniform over all eight rigid transforms, including reflections. Reflections
are included because the original reflects footprints elsewhere (`MergeSkin`
groups S with Z and J with L onto one shared sprite).

This has a consequence that drives the next section: **roughly half the bricks
in play are in an orientation the original never hand-placed.**

## Hero placement on the brick

Each brick carries a `Hero` child. The original hand-placed that child's offset
per orientation — but only ships one variant per *distinct rotation of the
footprint* (2 for a bar, 1 for a square, 4 for an L/T), and never a mirrored
one. `Roll` deals all eight. So about half of live bricks have no authored
offset to copy, and `Figures.HeroAnchor` has to derive one.

Three approaches were tried. The first two are wrong in instructive ways:

1. **Match by silhouette** — "same footprint, so same offset". Wrong: a 3×1 bar
   at `(0,false)` and at `(2,true)` occupies the same three cells, but the
   transform between them is a flip in y. Reusing the offset raw ignores it.

2. **Average all authored offsets, pulled back to canonical.** Wrong, and the
   numeric probe *passed* — it took a zoomed play-mode capture to see it. The
   hand-placed offsets disagree by up to a full cell, and **the mean of points
   on an L is not on the L.** Heroes ended up straddling notches and hanging off
   ends.

3. **Nearest authored variant, rigid-mapped** — correct, and it is what ships.
   `Orient(Unorient(p, authored), drawn)` is exactly the rigid transform that
   carries the authored footprint onto the drawn one (both being `Oriented`
   images of the same canonical), so a hero standing on a cell lands on *that
   cell's image*. Geometrically guaranteed to be on the brick.

Nearest means fewest quarter-turns, mirror counted slightly worse than a turn
(`min(t, 4−t) + (mirror ? 0.5 : 0)`) — any authored variant is geometrically
safe, so this only picks the least visually disruptive one. Exact matches
short-circuit and reproduce the authored offset byte-for-byte.

The lesson worth carrying: **a numeric probe that checks "is the anchor inside
the bounding box" passes for a wrong answer.** Look at the picture.

## Merging

`MergeSystem` merges touching bricks that share a `MergeSkin` group *and* level.

The grouping key is `MergeSkin`, **not** hero ID: 狂斧, 投石手, 毒液 and 忍者
all carry `MergeSkin 3`, so four different heroes combine with each other. That
is precisely why the column exists separately from `ID`, and it is also the
independent confirmation of the footprint table (see above).

## Height bonus

`HeightBonus` maps tower height to a global additive attack multiplier — the
ruler up the left edge of the reference screenshots.

**This is not in the extracted data.** All 28 tables were swept; the only
`Height` columns anywhere are `Global.ChallAntiAirHeight` and
`Global.ChallGazeHeight`, both belonging to challenge modifiers. The bands here
are read off the reference screenshots and are the one place in the project
where a *system* rather than a constant is reconstructed rather than extracted.
It is called out in the source for the same reason it is called out here.

## Figure animation

Not sprite sheets. The original uses **Spine 3.8.75 skeletal animation** via
spine-unity, so there is no "frame of a hero" in the bundles at all — only
bones, meshes and atlas fragments. Details and evidence in
[ART.md](ART.md#the-figures-are-spine-not-sprite-sheets).

`FigureAnimator` is therefore a frame player that currently holds **one frame
per clip** — the baked setup pose — with the motion supplied procedurally:

| Clip | Procedural motion |
|---|---|
| `Appear` | scale 0.40 → 1.15 → 1.0, alpha ramp |
| `Idle` | 1.6 s bob + squash |
| `Move` | 0.5 s hop + lean |
| `Attack` | 0.28 s lunge toward target + squash |
| `Hit` | 0.18 s recoil + white flash |
| `Death` | 0.45 s spin, shrink, fade |

The clip names are the original's own, read out of the skeletons. Because
`Clip` already holds `Sprite[] Frames` and `Figures.LoadFrames` already looks in
`Resources/Figures/<rig>/<clip>/`, baking the real timelines is a **data-only**
change — drop numbered PNGs in and the procedural motion steps aside.

Neither heroes nor enemies are mirrored: heroes are drawn facing right, and
monsters are frontal three-quarter views with mixed asymmetry, so mirroring them
would look wrong rather than merely different.

## Scene generation

`Assets/Scenes/Battle.unity` is generated by `BattleSceneBuilder`
(**Skyscraper → Build Battle Scene**), and `BattleSceneAutoBuild` is
`[InitializeOnLoad]` so a fresh clone gets a working scene after import without
anyone having to know the menu item exists.

**Hand edits to the scene are discarded.** The upside is that the scene is
reviewable as code and cannot drift between machines; the cost is that "just
drag it in the inspector" is not available. Change the builder.

## Invariants you can break without noticing

Collected because each of these has actually gone wrong, and none of them
produce an error message:

- **Reading a value before recompilation finishes** gives you the old assembly
  and looks like your edit did nothing.
- **The orientation convention** — wrong inverse still puts the hero on the
  brick, just on the wrong cell.
- **`Sprite.bounds` vs `rect.width / pixelsPerUnit`** — the former is the tight
  mesh and silently under-reports.
- **IMGUI y-down vs world y-up** — compare only after converting.
- **`Object.Destroy` is deferred to end of frame** — a destroyed object is still
  non-null and still in lists for the rest of the current frame. `EnemyUnit`'s
  death path books the kill *immediately* and only then plays the animation, so
  a corpse cannot stall a wave.
- **Enemy scale**: the root takes `BrickEnemy.Scale` directly. Collider radius
  and art scale each absorbed the old 1.25 factor, so every product is
  unchanged — but the relative sizes between monsters now follow the table.
  Verified: `Monster_1` at `Scale 2.0` measures 1.57 world units ≈ 70.6
  reference px, against 72 px measured in the capture.
