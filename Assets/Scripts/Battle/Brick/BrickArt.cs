using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// The brick faces, loaded from Resources/Bricks.
    ///
    /// These are the original game's own tile images, extracted from its
    /// AssetBundles -- the one place in the project where that is true. Nothing
    /// here is required: every lookup can come back empty and the callers fall
    /// back to the primitives in Shapes, so a build with the folder deleted
    /// still runs and still reads correctly, just plainer.
    ///
    /// Two sprites per brick. break_N is the body, one per cube index, drawn in
    /// that cube's own authored orientation -- which is why BrickShape keeps the
    /// sixteen cubes separate instead of collapsing the mirror pairs. break_M_1
    /// ..break_M_4 are rims marking merge level, filed under the group's
    /// representative cube (BrickHero.MergeSkin) rather than per hero, so four
    /// heroes share one set of four and the rim has to be turned onto the body
    /// it is landing on.
    public static class BrickArt
    {
        public const string Folder = "Bricks/";

        /// Level 1 wears no rim; 2..5 wear break_M_1..break_M_4 and anything
        /// higher keeps the last one.
        public const int RimLevels = 4;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// Null rather than an exception when the folder is absent, because a
        /// missing face is a cosmetic downgrade and not a broken battle.
        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>(Folder + name);
            Cache[name] = s;
            return s;
        }

        public static Sprite Body(BrickShape shape) =>
            shape == null ? null : Load(shape.Canonical.Name);

        /// True when there is enough art to draw the piece as one image.
        public static bool Has(BrickShape shape) => Body(shape) != null;

        // ------------------------------------------------------------------
        /// Draw one brick: the body, then its merge rim if it has earned one.
        /// Returns false without touching `root` when the body sprite is
        /// missing, which is the caller's cue to fall back to primitives.
        public static bool Build(Transform root, BrickHeroRow row, BrickShape shape,
                                 int level, int sortingOrder, float alpha)
        {
            var body = Body(shape);
            if (body == null) return false;

            Place(root, "Face", body, shape.Canonical, shape.Rot, shape.Flip,
                  sortingOrder, alpha);
            UpdateRim(root, row, shape, level, sortingOrder + 1, alpha);
            return true;
        }

        /// Replace whatever rim the brick is wearing with the one its current
        /// level calls for. Separate from Build because a merge changes the
        /// level of a brick that is already standing.
        public static void UpdateRim(Transform root, BrickHeroRow row, BrickShape shape,
                                     int level, int sortingOrder, float alpha)
        {
            // Renamed before being destroyed, not just destroyed: Destroy is
            // deferred to the end of the frame, so the outgoing rim is still a
            // child named "Rim" when the replacement goes in, and two merges in
            // one frame would otherwise leave the older sprite on top. Sweeps
            // every match rather than the first, so a brick that already picked
            // up duplicates recovers instead of keeping them forever.
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c.name != "Rim") continue;
                c.name = "RimSpent";
                Object.Destroy(c.gameObject);
            }

            var rim = RimFor(row, shape, level);
            if (rim.Sprite == null) return;
            Place(root, "Rim", rim.Sprite, rim.Authored, rim.Rot, rim.Flip,
                  sortingOrder, alpha);
        }

        public struct RimArt
        {
            public Sprite Sprite;
            /// The cube the rim was drawn for, whose cell box sizes it.
            public BrickShape Authored;
            public int Rot;
            public bool Flip;
        }

        /// The rim for a brick at this level, already turned to sit on the
        /// hero's own footprint.
        ///
        /// The group's rim is authored for cube MergeSkin, so it needs the
        /// transform from that cube onto this one before the drawn orientation
        /// is applied on top -- see BrickShape.Compose for why the two rotations
        /// cannot just be added.
        public static RimArt Rim(BrickHeroRow row, int level)
        {
            var none = default(RimArt);
            if (row == null || level < 2) return none;

            var authored = BrickShape.ForCube(row.MergeSkin);
            if (authored == null) return none;

            int step = Mathf.Clamp(level - 1, 1, RimLevels);
            var sprite = Load($"break{row.MergeSkin}_{step}");
            if (sprite == null) return none;

            var shape = BrickShape.For(row);
            if (!shape.TransformFrom(authored, out int q, out bool f))
                return none;                      // group and hero disagree

            return new RimArt { Sprite = sprite, Authored = authored, Rot = q, Flip = f };
        }

        /// Rim for a shape already carrying its drawn orientation: composes the
        /// group transform under it.
        public static RimArt RimFor(BrickHeroRow row, BrickShape drawn, int level)
        {
            var r = Rim(row, level);
            if (r.Sprite == null || drawn == null) return r;
            drawn.Compose(r.Rot, r.Flip, out int rot, out bool flip);
            r.Rot = rot;
            r.Flip = flip;
            return r;
        }

        // ------------------------------------------------------------------
        /// Add one sprite child covering `authored`'s cell box, then turn it.
        ///
        /// The sprite is stretched to its own cube's box first and rotated
        /// afterwards, which is why a quarter turn needs no width/height swap
        /// here: Unity applies scale before rotation, so scaling to w x h and
        /// then turning 90 degrees lands h x w on screen, exactly what the
        /// turned footprint occupies. The pivot is the sprite's centre and the
        /// piece's origin is its bounding-box centre (BrickShape.CellCenter
        /// measures from there), so the child sits at the origin.
        static void Place(Transform root, string name, Sprite sprite,
                          BrickShape authored, int quarters, bool flip,
                          int sortingOrder, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 1f, 1f, alpha);

            var px = sprite.bounds.size;
            float sx = px.x > 0f ? authored.CellsWide * BrickShape.CellSize / px.x : 1f;
            float sy = px.y > 0f ? authored.CellsHigh * BrickShape.CellSize / px.y : 1f;

            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(flip ? -sx : sx, sy, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * (quarters & 3));
        }
    }
}
