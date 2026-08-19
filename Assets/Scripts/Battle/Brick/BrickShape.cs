using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// A tetromino-style footprint: a set of unit cells on an integer grid.
    ///
    /// The tables name the geometry but do not describe it: BrickHero.Shape is
    /// "Cube_1".."Cube_16", one per hero row. The art supplies what it points
    /// at. Two atlases draw those sixteen as polyominoes and agree on all
    /// sixteen -- Cube_N in the tower atlas, break1..break16 in the battle one
    /// -- so the hero fixes the shape and ByCube below is that correspondence
    /// transcribed. See the catalogue for the sprite measurements.
    ///
    /// Each of the sixteen is kept separate even where two are the same
    /// footprint turned around, because break_N is also the name of the sprite
    /// that draws it. Holding the cube's own authored layout means the sprite
    /// needs no correction before the rolled orientation is applied to it.
    ///
    /// Orientation is the part the tables cannot fix, so it is rolled per draw
    /// and the player never changes it: the card face in the idle capture
    /// already shows the piece in the orientation it falls in one frame later.
    /// Reflections are included alongside rotations because the original
    /// reflects footprints elsewhere -- BrickHero.MergeSkin groups heroes by
    /// shape up to mirror, putting S with Z and J with L on one shared sprite.
    public class BrickShape
    {
        /// Cell edge in world units. The pedestal uses the same value, so a
        /// piece sits flush on it the way it does in the reference.
        public const float CellSize = 0.8f;

        public readonly string Name;
        /// Normalised so the lowest-left occupied cell is (0,0).
        public readonly Vector2Int[] Cells;
        public readonly int CellsWide;
        public readonly int CellsHigh;

        /// Quarter turns clockwise applied after the flip, and whether the
        /// canonical form was mirrored about its vertical axis. Kept so a
        /// rotation can be composed from the catalogue entry every time rather
        /// than from the last rotated copy, which would drift.
        public readonly int Rot;
        public readonly bool Flip;

        readonly string _base;
        readonly BrickShape _canonical;
        readonly int _cube;
        readonly string _family;

        /// The catalogue entry this piece is an orientation of.
        public BrickShape Canonical => _canonical ?? this;

        /// Which break_N sprite draws this footprint, 1..16. An orientation
        /// reports its canonical entry's number, because the sprite is that
        /// same picture turned rather than a different picture.
        public int Cube => Canonical._cube;

        /// The footprint up to rotation and reflection -- "J4" for all four
        /// cubes that draw a J. BrickHero.MergeSkin partitions the sixteen
        /// heroes into exactly these classes, which is the independent check
        /// that the catalogue is transcribed off the art and not guessed.
        public string Family => Canonical._family;

        public int Area => Cells.Length;
        public Vector2 Size => new Vector2(CellsWide, CellsHigh) * CellSize;

        BrickShape(int cube, string family, params Vector2Int[] cells)
            : this("break" + cube, 0, false, cells)
        {
            _cube = cube;
            _family = family;
        }

        BrickShape(string name, int rot, bool flip, Vector2Int[] cells)
        {
            _base = name;
            Rot = rot & 3;
            Flip = flip;
            Name = Rot == 0 && !Flip
                ? name
                : $"{name}{(Flip ? "*" : "")}{(Rot == 0 ? "" : "@" + Rot * 90)}";

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
            }
            Cells = new Vector2Int[cells.Length];
            int maxX = 0, maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                var c = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
                Cells[i] = c;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }
            CellsWide = maxX + 1;
            CellsHigh = maxY + 1;
        }

        BrickShape(BrickShape canonical, int rot, bool flip, Vector2Int[] cells)
            : this(canonical._base, rot, flip, cells)
        {
            _canonical = canonical;
        }

        /// Local offset of cell i, measured from the piece's bounding-box
        /// centre -- which is also the transform origin, so the piece pivots
        /// where its mass roughly is.
        public Vector2 CellCenter(int i) => new Vector2(
            (Cells[i].x + 0.5f - CellsWide * 0.5f) * CellSize,
            (Cells[i].y + 0.5f - CellsHigh * 0.5f) * CellSize);

        /// One of the eight rigid transforms of the canonical footprint: mirror
        /// about the vertical axis first, then turn `quarters` times clockwise.
        /// Transforming the footprint rather than the transform keeps the piece
        /// axis-aligned when it spawns, so it lands flat instead of on a corner.
        public BrickShape Oriented(int quarters, bool flip)
        {
            var c = Canonical;
            quarters &= 3;
            if (quarters == 0 && !flip) return c;

            var cells = new Vector2Int[c.Cells.Length];
            int maxX = c.CellsWide - 1;
            for (int i = 0; i < cells.Length; i++)
            {
                var v = c.Cells[i];
                cells[i] = flip ? new Vector2Int(maxX - v.x, v.y) : v;
            }
            for (int q = 0; q < quarters; q++)
            {
                int maxY = 0;
                foreach (var v in cells) if (v.y > maxY) maxY = v.y;
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new Vector2Int(maxY - cells[i].y, cells[i].x);
            }
            return new BrickShape(c, quarters, flip, cells);
        }

        /// Turn what is on screen 90 degrees clockwise.
        public BrickShape Rotated() => Oriented(Rot + 1, Flip);

        /// Mirror what is on screen. Reflecting an already-turned piece equals
        /// reflecting the canonical one and turning the other way, hence the
        /// negated quarter count.
        public BrickShape Mirrored() => Oriented(-Rot, !Flip);

        /// Compose two orientations: applying (q,f) and then this one.
        ///
        /// Mirror-then-turn does not commute -- turning after a mirror runs the
        /// other way round -- so the quarter counts cannot simply be added.
        /// M*R^q = R^-q*M gives the negation below. Used to carry a sprite
        /// authored for one cube onto a different cube of the same family.
        public void Compose(int quarters, bool flip, out int rot, out bool mirror)
        {
            rot = (Rot + (Flip ? -quarters : quarters)) & 3;
            mirror = Flip ^ flip;
        }

        /// The transform taking `from`'s layout onto this one's, or false when
        /// the two are not the same footprint at all. Both are catalogue
        /// entries in practice: it answers "how is break3 turned to become
        /// break9", which is what a shared merge-rim sprite needs.
        public bool TransformFrom(BrickShape from, out int quarters, out bool flip)
        {
            for (int f = 0; f < 2; f++)
                for (int q = 0; q < 4; q++)
                {
                    var o = from.Oriented(q, f == 1);
                    if (o.CellsWide != CellsWide || o.CellsHigh != CellsHigh) continue;
                    if (!SameCells(o.Cells, Cells)) continue;
                    quarters = q;
                    flip = f == 1;
                    return true;
                }
            quarters = 0;
            flip = false;
            return false;
        }

        static bool SameCells(Vector2Int[] a, Vector2Int[] b)
        {
            if (a.Length != b.Length) return false;
            foreach (var v in a)
            {
                bool hit = false;
                foreach (var w in b) if (v == w) { hit = true; break; }
                if (!hit) return false;
            }
            return true;
        }

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        // --- catalogue ------------------------------------------------------
        // Read straight off the battle atlas, not invented and not inferred
        // from a screenshot. Every break1..break16 sprite is an exact multiple
        // of 100px per cell once the 256px atlas cap is undone (the capped ones
        // are all uniformly scaled by 0.853), which is what makes the cell
        // count unambiguous rather than a judgement call:
        //
        //   break1  300x100 ###          break9  200x300 #./#./##
        //   break2  300x200 .##/##.      break10 200x300 .#/.#/##
        //   break3  200x300 ##/#./#.     break11 300x200 ##./.##
        //   break4  200x300 ##/.#/.#     break12 300x100 ###
        //   break5  300x200 ##./.##      break13 300x200 ###/.#.
        //   break6  300x200 .#./###      break14 300x200 ###/.#.
        //   break7  200x200 ##/##        break15 200x200 ##/##
        //   break8  100x100 #            break16 300x300 .#./###/.#.
        //
        // Nothing exceeds three cells in either direction and nothing exceeds
        // five cells total. Up to rotation and reflection those sixteen are
        // seven distinct footprints -- so no domino, no four in a row, and
        // nothing four cells wide. Anything outside this list is not in the
        // game.
        //
        // Each entry below is one cube in the orientation its own sprite is
        // drawn in, so break_N can be pasted onto it with no correction: the
        // only transform the art then needs is the one the draw rolled. Cells
        // are listed top row first to read like the picture above; y counts up,
        // so the first line carries the largest y.

        /// Cube_1..Cube_16 -> footprint, indexed 1-based so the row's own id
        /// reads through unchanged. This is the table BrickHero.Shape was
        /// always pointing at: break_N corresponds 1:1 to hero row N, and each
        /// break_N carries exactly one footprint, so the hero fixes the shape.
        static readonly BrickShape[] ByCube =
        {
            null,                                                                    // 0 unused
            new BrickShape( 1, "I3", V(0, 0), V(1, 0), V(2, 0)),                      // ###
            new BrickShape( 2, "S4", V(1, 1), V(2, 1), V(0, 0), V(1, 0)),             // .## / ##.
            new BrickShape( 3, "J4", V(0, 2), V(1, 2), V(0, 1), V(0, 0)),             // ## / #. / #.
            new BrickShape( 4, "J4", V(0, 2), V(1, 2), V(1, 1), V(1, 0)),             // ## / .# / .#
            new BrickShape( 5, "S4", V(0, 1), V(1, 1), V(1, 0), V(2, 0)),             // ##. / .##
            new BrickShape( 6, "T4", V(1, 1), V(0, 0), V(1, 0), V(2, 0)),             // .#. / ###
            new BrickShape( 7, "O4", V(0, 1), V(1, 1), V(0, 0), V(1, 0)),             // ## / ##
            new BrickShape( 8, "O1", V(0, 0)),                                        // #
            new BrickShape( 9, "J4", V(0, 2), V(0, 1), V(0, 0), V(1, 0)),             // #. / #. / ##
            new BrickShape(10, "J4", V(1, 2), V(1, 1), V(0, 0), V(1, 0)),             // .# / .# / ##
            new BrickShape(11, "S4", V(0, 1), V(1, 1), V(1, 0), V(2, 0)),             // ##. / .##
            new BrickShape(12, "I3", V(0, 0), V(1, 0), V(2, 0)),                      // ###
            new BrickShape(13, "T4", V(0, 1), V(1, 1), V(2, 1), V(1, 0)),             // ### / .#.
            new BrickShape(14, "T4", V(0, 1), V(1, 1), V(2, 1), V(1, 0)),             // ### / .#.
            new BrickShape(15, "O4", V(0, 1), V(1, 1), V(0, 0), V(1, 0)),             // ## / ##
            new BrickShape(16, "X5", V(1, 2), V(0, 1), V(1, 1), V(2, 1), V(1, 0)),    // .#. / ### / .#.
        };

        /// The cube whose art stands in for a whole merge group: BrickHero
        /// .MergeSkin is itself a cube index, and the group's rim sprites are
        /// filed under it (break3_1..break3_4 serve cubes 3, 4, 9 and 10).
        public static BrickShape ForCube(int cube) =>
            cube >= 1 && cube < ByCube.Length ? ByCube[cube] : null;

        static readonly BrickShape[][] Single = BuildSingle();

        static BrickShape[][] BuildSingle()
        {
            var a = new BrickShape[ByCube.Length][];
            for (int i = 1; i < ByCube.Length; i++) a[i] = new[] { ByCube[i] };
            return a;
        }

        /// "Cube_7" -> 7, and 0 for anything unparseable. Only the tail after
        /// the underscore is read, so a renamed prefix does not break it.
        static int CubeIndex(BrickHeroRow row)
        {
            var s = row != null ? row.Shape : null;
            if (string.IsNullOrEmpty(s)) return 0;
            int u = s.LastIndexOf('_');
            return int.TryParse(u >= 0 ? s.Substring(u + 1) : s, out int n)
                   && n >= 1 && n < ByCube.Length ? n : 0;
        }

        /// The hero's footprint in its authored orientation. Falls back to the
        /// three-bar rather than throwing, so a table typo costs a wrong brick
        /// and not a dead battle.
        public static BrickShape For(BrickHeroRow row)
        {
            int n = CubeIndex(row);
            return ByCube[n == 0 ? 1 : n];
        }

        /// What a hero can produce. One entry now: the shape is the hero's, so
        /// there is nothing to choose between. Kept as a list because it is the
        /// probe's read-out of "what does this hero drop".
        public static IReadOnlyList<BrickShape> SetFor(BrickHeroRow row)
        {
            int n = CubeIndex(row);
            return n == 0 ? Single[1] : Single[n];
        }

        /// The hero's footprint in a random orientation. The original settles
        /// orientation when it deals the card -- the card face in the idle
        /// capture already shows the piece the way it falls a frame later -- so
        /// rolling it here and storing it on the card is enough; the HUD paints
        /// Cells directly.
        public static BrickShape Roll(BrickHeroRow row)
        {
            // Uniform over all eight transforms rather than over the distinct
            // silhouettes. A symmetric footprint collapses several of them onto
            // the same result, but every distinct result absorbs the same
            // number of transforms, so the odds stay even and there is nothing
            // to de-duplicate.
            return For(row).Oriented(Random.Range(0, 4), Random.Range(0, 2) == 0);
        }

        public override string ToString() => $"{Name}/{Family}({CellsWide}x{CellsHigh},{Area})";
    }
}
