using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// The hero and monster artwork, and where it stands.
    ///
    /// The originals are Spine 3.8 skeletal rigs, not sequence frames, so there
    /// is no frame in the bundle to lift out: a picture of a character only
    /// exists once something poses the bones. Resources/Figures holds one baked
    /// setup pose per rig (16 heroes, 19 monsters). FigureAnimator moves them;
    /// this file is only the table and the placement.
    ///
    /// Three numbers make the placement exact, and all three come from the
    /// original prefabs rather than from taste:
    ///
    ///   * 1 prefab unit == 1 cell. The hand-placed `hero` nodes land on cell
    ///     centres, and `building` sits at +/-1 unit along the long axis of a
    ///     3-cell brick.
    ///   * SkeletonDataAsset.scale is 0.01 on all 35 rigs, and the `hero` node
    ///     inside each brick variant is a uniform 0.5. So a hero spans
    ///     px * 0.01 * 0.5 cells, and a monster px * 0.01 * Scale cells.
    ///   * The monster prefab roots carry localScale == BrickEnemy.Scale (17 of
    ///     20 match the first table row exactly; the three that differ are
    ///     models whose rows disagree among themselves). Monster_1 at Scale 2
    ///     then comes out 70.6 reference px wide against the 72 px measured in
    ///     the capture, which is what pins the whole chain down.
    public static class Figures
    {
        const string Folder = "Figures/";

        /// SkeletonDataAsset.scale, identical on all 35 rigs.
        const float SkeletonScale = 0.01f;

        /// The `hero` node's uniform scale inside every brick_* variant.
        const float HeroNodeScale = 0.5f;

        /// Skeleton units -> local units, inside the brick (a cell is CellSize).
        public const float HeroUnit = SkeletonScale * HeroNodeScale * BrickShape.CellSize;

        /// Skeleton units -> local units inside an enemy root that already
        /// carries localScale == row.Scale.
        public const float EnemyUnit = SkeletonScale * BrickShape.CellSize;

        /// One baked rig: pixel size, and the bbox centre in skeleton units.
        ///
        /// The bake pads and crops to the drawn bounds, so the image spans
        /// exactly the bbox and its centre pivot sits at (CX, CY) in skeleton
        /// space. Offsetting the sprite by that puts the skeleton root -- which
        /// is at the character's feet -- where the original put it.
        public struct Figure
        {
            public string Path;
            public float W, H, CX, CY;
        }

        static Figure F(string path, float w, float h, float cx, float cy) =>
            new Figure { Path = path, W = w, H = h, CX = cx, CY = cy };

        // From ggnbz/art/_figures/figures.json. The subfolder is baked into the
        // path so a hero and a monster can never collide on a bare name.
        static readonly Figure[] Table =
        {
            F("Hero/Arrow",      261,  286,  -13.1f,  57.2f),
            F("Hero/Axe",        268,  269,   47.3f,  47.7f),
            F("Hero/Boxer",      229,  172,   -5.4f,   0.0f),
            F("Hero/IceCube",    196,  173,   12.0f,   0.0f),
            F("Hero/Javelin",    307,  307,   16.5f,  24.4f),
            F("Hero/Laser",      152,  123,    0.0f,   0.0f),
            F("Hero/Magma",      261,  260,   44.2f,  42.7f),
            F("Hero/Miner",      278,  269,   52.9f,  48.0f),
            F("Hero/Ninja",      223,  188,   25.2f,   7.6f),
            F("Hero/Plot",       173,  173,    0.0f,   0.0f),
            F("Hero/Poison",     196,  186,   11.6f,   6.8f),
            F("Hero/Repair",     349,  272,   88.3f,  49.9f),
            F("Hero/Star",       297,  260,   62.4f,  18.8f),
            F("Hero/Thor",       198,  251,   12.5f,  30.0f),
            F("Hero/Trebuchet",  290,  261,   27.7f,  39.5f),
            F("Hero/WuKong",     269,  284,   15.0f,   7.5f),

            F("Enemy/Monser_8",   124,  109,   12.0f,  53.2f),   // the original's typo
            F("Enemy/Monster_1",   98,   97,   13.0f,  46.3f),
            F("Enemy/Monster_2",  103,   78,   -9.9f,  35.6f),
            F("Enemy/Monster_3",  185,  162,    5.4f,  78.2f),
            F("Enemy/Monster_4",  168,  311,    2.7f, 164.5f),
            F("Enemy/Monster_5",  267,  355,   13.9f, 186.4f),
            F("Enemy/Monster_6",  228,  452,   -2.1f, 251.6f),
            F("Enemy/Monster_7",  508,  560,  -17.5f, 261.3f),
            F("Enemy/Monster_9",  168,  161,   -1.4f,  92.7f),
            F("Enemy/Monster_10", 241,  195,   38.2f, 100.3f),
            F("Enemy/Monster_11", 157,  171,    9.0f,  93.0f),
            F("Enemy/Monster_12", 181,  182,   11.0f, 127.7f),
            F("Enemy/Monster_13", 159,  141,    1.1f,  85.7f),
            F("Enemy/Monster_14", 243,  212,    0.7f,  21.9f),
            F("Enemy/Monster_15", 479,  427,   -5.0f, 236.3f),
            F("Enemy/Monster_16", 1339, 1217, 124.9f, 322.2f),
            F("Enemy/Monster_18", 356,  167,   -7.9f,  14.0f),
            F("Enemy/Monster_19", 543,  284,   -9.0f,  43.2f),
            F("Enemy/Monster_20", 346,  628,  -17.0f, 280.6f),
        };

        public static bool Get(string path, out Figure f)
        {
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Path == path) { f = Table[i]; return true; }
            f = default;
            return false;
        }

        // ------------------------------------------------------------------
        // which figure

        /// BrickHero.ID -> rig. The names are the rigs' own, from the bundle.
        public static string HeroPath(int heroId)
        {
            switch (heroId)
            {
                case 4001: return "Hero/Star";        // 爆爆法师
                case 4002: return "Hero/Magma";       // 熔岩
                case 4003: return "Hero/Trebuchet";   // 投石手
                case 4004: return "Hero/Axe";         // 狂斧
                case 4005: return "Hero/Repair";      // 修理工
                case 4006: return "Hero/Javelin";     // 投矛手
                case 4007: return "Hero/Miner";       // 矿工
                case 4008: return "Hero/Plot";        // 城墙
                case 4009: return "Hero/Poison";      // 毒液
                case 4010: return "Hero/Ninja";       // 忍者
                case 4011: return "Hero/Boxer";       // 拳击手
                case 4012: return "Hero/IceCube";     // 冻冻投手
                case 4013: return "Hero/Laser";       // 镭射眼
                case 4014: return "Hero/Arrow";       // 神射手
                case 4015: return "Hero/Thor";        // 雷神
                case 4016: return "Hero/WuKong";      // 行者
                default:   return null;
            }
        }

        /// BrickEnemy.Model -> rig. Not the identity: the original's prefab
        /// names and rig names cross over in the middle of the range
        /// (monster_7 uses Monster_9 and vice versa, monster_10/11 likewise),
        /// monster_17 reuses Monster_12, and Monster_8's asset is misspelled.
        public static string EnemyPath(string model)
        {
            switch (model)
            {
                case "monster_1":  return "Enemy/Monster_1";
                case "monster_2":  return "Enemy/Monster_2";
                case "monster_3":  return "Enemy/Monster_3";
                case "monster_4":  return "Enemy/Monster_4";
                case "monster_5":  return "Enemy/Monster_5";
                case "monster_6":  return "Enemy/Monster_6";
                case "monster_7":  return "Enemy/Monster_9";
                case "monster_8":  return "Enemy/Monser_8";
                case "monster_9":  return "Enemy/Monster_7";
                case "monster_10": return "Enemy/Monster_11";
                case "monster_11": return "Enemy/Monster_10";
                case "monster_12": return "Enemy/Monster_12";
                case "monster_13": return "Enemy/Monster_13";
                case "monster_14": return "Enemy/Monster_14";
                case "monster_15": return "Enemy/Monster_15";
                case "monster_16": return "Enemy/Monster_16";
                case "monster_17": return "Enemy/Monster_12";
                case "monster_bat":          return "Enemy/Monster_18";
                case "monster_shark":        return "Enemy/Monster_19";
                case "monster_octopus_boss": return "Enemy/Monster_20";
                default: return null;
            }
        }

        // ------------------------------------------------------------------
        // loading

        public static Sprite Load(string path) =>
            string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(Folder + path);

        /// The frames of one clip, in order, or null if this rig has none baked.
        ///
        /// Stage 2 (baking the Spine timelines out) drops numbered frames into
        /// Resources/Figures/Hero/Star/idle/ beside the flat Star.png, and this
        /// picks them up with no other change: FigureAnimator already indexes an
        /// array, and until then every clip holds the one setup pose.
        public static Sprite[] LoadFrames(string path, string clip)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var seq = Resources.LoadAll<Sprite>(Folder + path + "/" + clip);
            if (seq == null || seq.Length == 0) return null;
            // idle_10 sorts before idle_2 alphabetically, which would play the
            // frames in a plausible-looking wrong order.
            System.Array.Sort(seq, (a, b) => Trailing(a.name).CompareTo(Trailing(b.name)));
            return seq;
        }

        static int Trailing(string name)
        {
            int i = name.Length;
            while (i > 0 && name[i - 1] >= '0' && name[i - 1] <= '9') i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int n) ? n : 0;
        }

        // ------------------------------------------------------------------
        // where the hero stands

        /// One authored placement: the orientation it was drawn in, and the
        /// offset from the footprint's bbox centre, in cells.
        public struct Anchor
        {
            public int Rot;
            public bool Flip;
            public float X, Y;
        }

        static Anchor A(int rot, bool flip, float x, float y) =>
            new Anchor { Rot = rot, Flip = flip, X = x, Y = y };

        // Measured off the prefabs by ggnbz/hero_anchor.py. The original ships
        // one child per orientation with its own hand-placed `hero` node rather
        // than rotating one placement, so these do not agree with each other
        // once pulled back to the canonical frame -- the T and Z groups differ
        // by up to a cell. That is the artist's choice, not an error, so the
        // orientations are kept apart and matched at runtime instead of
        // averaged: it reproduces the original exactly.
        //
        // (Rot, Flip) is the pair BrickShape.Oriented takes, recovered as
        // quarters = round(-(rootRotZ + bodyRotZ) / 90) and
        // flip = rootScale.x * bodyScale.x < 0.
        static readonly System.Collections.Generic.Dictionary<int, Anchor[]> Anchors =
            new System.Collections.Generic.Dictionary<int, Anchor[]>
        {
            { 4001, new[]{ A(2,true , 0.705f,-0.093f), A(3,true ,-0.121f, 0.952f) } },
            { 4002, new[]{ A(2,true , 0.848f,-0.584f), A(3,true , 0.365f, 0.868f) } },
            { 4003, new[]{ A(0,true ,-0.403f, 0.918f), A(1,true ,-0.909f,-0.578f),
                           A(2,true , 0.301f,-1.111f), A(3,true , 0.818f, 0.418f) } },
            { 4004, new[]{ A(0,true , 0.337f, 0.916f), A(1,true ,-1.029f, 0.411f),
                           A(2,true ,-0.515f,-1.094f), A(3,true , 0.780f,-0.599f) } },
            { 4005, new[]{ A(2,true ,-0.939f,-0.599f), A(3,true ,-0.500f,-0.073f) } },
            { 4006, new[]{ A(0,true , 0.870f,-0.594f), A(1,true , 0.349f,-0.101f),
                           A(2,true , 0.840f, 0.398f), A(3,true , 0.431f, 0.885f) } },
            { 4007, new[]{ A(3,true ,-0.460f,-0.576f) } },
            { 4008, new[]{ A(3,false, 0.026f,-0.073f) } },
            { 4009, new[]{ A(0,true ,-0.403f, 0.918f), A(1,true ,-0.909f,-0.578f),
                           A(2,true , 0.301f,-1.111f), A(3,true , 0.818f, 0.418f) } },
            { 4010, new[]{ A(0,true , 0.337f, 0.916f), A(1,true ,-1.029f, 0.411f),
                           A(2,true ,-0.515f,-1.094f), A(3,true , 0.780f,-0.599f) } },
            { 4011, new[]{ A(2,true , 0.268f,-0.584f), A(3,true , 0.282f,-0.063f) } },
            { 4012, new[]{ A(2,true , 0.705f,-0.093f), A(3,true ,-0.121f, 0.952f) } },
            { 4013, new[]{ A(0,false, 0.870f,-0.519f), A(1,false, 0.349f,-0.010f),
                           A(2,false, 0.840f, 0.511f), A(3,false, 0.502f, 0.885f) } },
            { 4014, new[]{ A(0,false, 0.870f,-0.519f), A(1,false, 0.349f,-0.010f),
                           A(2,false, 0.840f, 0.511f), A(3,false, 0.500f, 0.980f) } },
            { 4015, new[]{ A(3,true ,-0.357f,-0.480f) } },
            { 4016, new[]{ A(3,true , 0.000f, 0.249f) } },
        };

        /// Where the figure's feet go, in the drawn shape's local space.
        ///
        /// Lookup order: the exact orientation if the original authored it, then
        /// the nearest authored one carried onto the drawn orientation, then the
        /// footprint's own centre of mass, for a hero with no table row at all.
        ///
        /// The middle step covers the orientations the original never shipped.
        /// It ships one variant per distinct rotation of the footprint and never
        /// a mirrored one, but BrickShape.Roll deals all eight transforms, so
        /// roughly half the bricks in play are drawn in a pose that has no
        /// hand-placed offset to copy.
        ///
        /// Carrying *one* authored placement across is what keeps the hero on
        /// the brick. Orient(Unorient(p, a), d) is a rigid transform, and it is
        /// the same one that takes the authored footprint onto the drawn
        /// footprint -- both being Oriented images of the same canonical -- so a
        /// hero standing on a cell lands on that cell's image. Averaging the
        /// authored placements first, which is what this replaces, loses the
        /// guarantee: the hand-placed offsets disagree by up to a cell (see the
        /// table above), and the mean of points on an L is not on the L. It put
        /// several heroes in the notch or off the end.
        ///
        /// Nearest means fewest quarter-turns, with a mirror counted as slightly
        /// worse than a turn -- any authored variant is geometrically safe, so
        /// this is only choosing the least visually disruptive one.
        ///
        /// Matching by silhouette -- "the same footprint, so the same offset" --
        /// was the first attempt and is wrong: a 3x1 bar drawn at (0,false) and
        /// at (2,true) occupies the same three cells, but the transform between
        /// them is a flip in y, so reusing the offset raw ignores it.
        public static Vector2 HeroAnchor(BrickHeroRow row, BrickShape drawn)
        {
            if (row != null && drawn != null &&
                Anchors.TryGetValue(row.ID, out var list) && list.Length > 0)
            {
                int best = 0;
                float bestCost = float.MaxValue;
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].Rot == drawn.Rot && list[i].Flip == drawn.Flip)
                        return new Vector2(list[i].X, list[i].Y) * BrickShape.CellSize;

                    // The relative transform is R^(d-a) when the flips agree and
                    // R^(d+a).F when they do not, because F.R^q == R^-q.F.
                    bool mirror = list[i].Flip != drawn.Flip;
                    int t = (mirror ? drawn.Rot + list[i].Rot
                                    : drawn.Rot - list[i].Rot) & 3;
                    float cost = Mathf.Min(t, 4 - t) + (mirror ? 0.5f : 0f);
                    if (cost < bestCost) { bestCost = cost; best = i; }
                }

                var p = Unorient(new Vector2(list[best].X, list[best].Y),
                                 list[best].Rot, list[best].Flip);
                return Orient(p, drawn.Rot, drawn.Flip) * BrickShape.CellSize;
            }

            var at = Vector2.zero;
            if (drawn == null || drawn.Cells.Length == 0) return at;
            for (int i = 0; i < drawn.Cells.Length; i++) at += drawn.CellCenter(i);
            return at / drawn.Cells.Length;
        }

        /// BrickShape.Oriented in point space, and its inverse.
        ///
        /// Oriented maps cell (x,y) to (maxY - y, x) per quarter, after mirroring
        /// x. Measured from the bounding-box centre -- which is what CellCenter
        /// returns and what these offsets are in -- that is (X,Y) -> (-Y,X), a
        /// counter-clockwise quarter, so Oriented is rotCCW^q . flipx^f and the
        /// inverse is flipx^f . rotCW^q.
        ///
        /// The direction is worth stating because it is easy to get backwards and
        /// silent when you do: the wrong one still lands the hero somewhere on
        /// the brick. It was settled against the prefabs rather than reasoned
        /// out -- pulling all four of a hero's hand-placed offsets back through
        /// each candidate, the correct one collapses them onto roughly one point
        /// (0.11 to 0.55 cells apart) and the other scatters them (0.43 to 1.1).
        static Vector2 Orient(Vector2 p, int q, bool flip)
        {
            if (flip) p.x = -p.x;
            for (int i = q & 3; i > 0; i--) p = new Vector2(-p.y, p.x);
            return p;
        }

        static Vector2 Unorient(Vector2 p, int q, bool flip)
        {
            for (int i = q & 3; i > 0; i--) p = new Vector2(p.y, -p.x);
            if (flip) p.x = -p.x;
            return p;
        }

        // ------------------------------------------------------------------
        // building

        /// The hero figure on a brick. Two nodes: an anchor at the feet, which
        /// is what the animator moves, and the sprite offset off it by the
        /// baked bbox centre.
        ///
        /// The figure is never rotated and never mirrored, because the original
        /// does not: the `hero` node counter-rotates by exactly its variant's
        /// rotation and its scale is a positive 0.5, so the character stands
        /// upright whichever way the brick landed.
        public static FigureAnimator BuildHero(Transform root, BrickHeroRow row,
                                              BrickShape drawn, int sortingOrder,
                                              float alpha)
        {
            if (row == null) return null;
            string path = HeroPath(row.ID);
            if (!Get(path, out var fig)) return null;
            var sprite = Load(path);
            if (sprite == null) return null;

            var anchor = new GameObject("HeroAnchor");
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = HeroAnchor(row, drawn);

            var anim = Place(anchor.transform, "Hero", sprite, fig,
                             HeroUnit, sortingOrder, alpha);
            return anim;
        }

        /// The monster figure. The enemy root itself is the skeleton root, so
        /// the anchor sits at the origin: the original puts SkeletonAnimation
        /// at localPosition zero on the prefab root.
        ///
        /// Never mirrored either, and here that is a judgement rather than a
        /// measurement: the rigs are frontal three-quarter views with mixed
        /// asymmetry (the dragon faces right, the shark left, the bat is
        /// symmetric), so they read correctly walking in either direction and
        /// flipping them would only make half of them face away.
        public static FigureAnimator BuildEnemy(Transform root, BrickEnemyRow row,
                                                int sortingOrder)
        {
            if (row == null) return null;
            string path = EnemyPath(row.Model);
            if (!Get(path, out var fig)) return null;
            var sprite = Load(path);
            if (sprite == null) return null;

            var anchor = new GameObject("Art");
            anchor.transform.SetParent(root, false);
            return Place(anchor.transform, "Fig", sprite, fig,
                         EnemyUnit, sortingOrder, 1f);
        }

        /// Sprite child + animator on the anchor.
        ///
        /// The scale is derived from the sprite's own rect and PPU rather than
        /// assuming the import settings, so a re-import at another PPU still
        /// comes out the right size. Deliberately not sprite.bounds: a
        /// tight-meshed sprite's bounds shrink to the drawn pixels, which would
        /// silently scale each figure by its own amount of padding.
        static FigureAnimator Place(Transform anchor, string name, Sprite sprite,
                                    Figure fig, float unit, int sortingOrder,
                                    float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(anchor, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 1f, 1f, alpha);

            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            float perPx = fig.W > 0f ? (sprite.rect.width / ppu) / fig.W : 0.01f;
            float k = perPx > 0f ? unit / perPx : 1f;
            go.transform.localScale = new Vector3(k, k, 1f);
            go.transform.localPosition = new Vector3(fig.CX * unit, fig.CY * unit, 0f);

            var anim = anchor.gameObject.AddComponent<FigureAnimator>();
            anim.Bind(sr, fig.H * unit, alpha);
            return anim;
        }
    }
}
