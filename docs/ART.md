# Art

Two things this document does:

1. Explains why the figure animation is what it is (the answer is Spine, not
   sprite sheets), and
2. tells you how to populate the two gitignored `Resources` folders, since a
   fresh clone has neither.

---

## What is missing from a fresh clone

```
Assets/Resources/Bricks/            88 files  (~470 KB)   brick base art
Assets/Resources/Figures/Hero/      16 PNGs               hero figures
Assets/Resources/Figures/Enemy/     19 PNGs               monster figures
```

Both are gitignored. They were extracted from 关关难不住's AssetBundles and are
its publisher's copyright, not this project's — so they are not redistributed
here. If you want them, extract them from your own copy of the game using the
pipeline below.

**Their absence is not a bug.** `Figures.BuildHero` and `Figures.BuildEnemy`
return `null` when the sprite cannot be loaded, and `Prefabs.cs` falls back to
procedural coloured polygons (`:121`, `:150`, `:187`). The project compiles,
plays and is fully testable without any art — it just looks like flat shapes.

## Expected filenames

The loaders are strict about these, so if you extract your own, match them.

`Assets/Resources/Figures/Hero/` — one PNG per hero, named by figure, not ID.
`Figures.HeroPath(int heroId)` owns the mapping:

```
Arrow  Axe  Boxer  IceCube  Javelin  Laser  Magma  Miner
Ninja  Plot  Poison  Repair  Star  Thor  Trebuchet  WuKong
```

`Assets/Resources/Figures/Enemy/` — named by the `Model` column of
`BrickEnemy.json`, via `Figures.EnemyPath(string model)`:

```
Monster_1 .. Monster_7   Monser_8   Monster_9 .. Monster_16
Monster_18  Monster_19  Monster_20
```

`Monser_8` is spelled that way **in the original data**. It is not a typo to
fix — `EnemyPath` reproduces it deliberately, and renaming the file will break
that one monster. There is no `Monster_17`.

`Assets/Resources/Bricks/` — `break1..break16` plus merge-rim variants
`break<N>_1..break<N>_4`. Rim sprites are filed under the group's representative
cube (`break3_1..break3_4` serve cubes 3, 4, 9 and 10), matching
`BrickHero.MergeSkin`.

## The figures are Spine, not sprite sheets

Worth stating plainly because it determines what "replace the art" even means.

The original's hero and monster animation is **Spine 3.8.75 skeletal animation**
played through spine-unity. Five independent lines of evidence:

1. The `MonoScript` census in the bundles names `SkeletonAnimation`,
   `SkeletonDataAsset` and `SpineAtlasAsset`, all in assembly `spine-unity`.
2. Every skeleton is a binary `.skel` TextAsset whose header reads version
   `3.8.75`.
3. **Zero `AnimationClip` objects and zero `Animator` objects** in either
   bundle. If this were sprite-sheet or Unity-native animation, that count could
   not be zero.
4. The `hero` node carries `MeshFilter` + `MeshRenderer` + a `SkeletonAnimation`
   MonoBehaviour — not a `SpriteRenderer`.
5. `materials/spinefill` is present in the bundle manifest.

Supporting data: all 35 rigs have `SkeletonDataAsset.scale = 0.01`. Atlases are
libgdx `.atlas` + page PNG, with no `pma` flag (straight alpha).

**The consequence:** there is no single flat image of a hero anywhere in the
game files. To get one you must solve the skeleton's setup pose and composite
the atlas fragments through the mesh deformations. That is what the extraction
step below does.

### The original's own clip names

Read out of the skeletons, not invented:

| | Clips |
|---|---|
| Heroes | `idle`, `attack_1`, `appear`; several also have `attack`. Hero 4008 (`Plot`, 城墙) has only `appear` + `idle` — a wall does not swing. |
| Monsters | `idle`, `move`, `attack`, `death`; `hit` on Monster_1/2/3; `appear` on Monster_3/16. Irregular names: `death3` (M12), `deathB6` (M10), `RUn` (M18), `attack_2` (M3). |

`FigureAnimator`'s `FigureClip` enum mirrors these.

## Extraction pipeline

Requires your own copy of the game's AssetBundles. They are unencrypted
UnityFS.

```
pip install UnityPy==1.25.0 Pillow numpy
```

Then, broadly:

1. **Brick art** — plain `Texture2D`/`Sprite` objects. Read them out with
   UnityPy and write PNGs. `o.read_typetree()` works on this build, which makes
   sprite rects and offsets readable directly.

2. **Figures** — cannot be read out as images, per the above. You need to parse
   the binary Spine 3.8 skeleton, resolve the setup pose, and rasterise it
   through the atlas. This project's driving workspace has
   `spine_read.py` + `spine_bake.py` for exactly this; the format is documented
   in Spine's own `SkeletonBinary` reference.

3. Drop the PNGs into the paths and names above. Unity will import them; the
   `.meta` files are generated locally and are also gitignored along with the
   art.

## Stage 2

Current state (stage 1) is one baked setup-pose PNG per rig, with the six clips
driven by procedural transform motion — see
[ARCHITECTURE.md § figure animation](ARCHITECTURE.md#figure-animation).

Stage 2 is baking the real Spine timelines into frame sequences. It is
deliberately a **data-only** change:

- `FigureAnimator.Clip` already holds `Sprite[] Frames`, `Fps`, `Loop`.
- `Figures.LoadFrames` already probes
  `Resources/Figures/<rig>/<clip>/` via `Resources.LoadAll<Sprite>`.

So: sample each Spine animation at a fixed fps, bake each sample the same way
the setup pose is baked, write them as numbered PNGs into e.g.
`Resources/Figures/Hero/Star/idle/`, and the sequence takes over automatically.
The procedural motion steps aside when real frames are present. **No code
change required.**

What remains unwritten is the animation-timeline half of the Spine parser; the
skeleton/setup-pose half already exists.

## Unwired art

Extracted but not yet used by anything:

- `break_frame_1..7` — most likely the card frames.
- `UI_yd_5`.
- The `building` and `join` `SpriteRenderer` layers that the original brick
  carries alongside `_` and `merge`.

Also outstanding: `Desk` (26 values) and `MapColor` (0–8) have table columns but
no identified art source.

## Known pipeline defect

The export script's `group_of()` uses only the first path segment, so all 696
files land in one `art/atlas/` folder instead of splitting by source atlas
(brick / brick1 / brick2 / tower / icon / map / …). Harmless but annoying when
you are looking for something by hand.
