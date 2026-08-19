using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Builds battle objects in code rather than from authored prefabs, so the
    /// project has no binary assets to keep in sync with the tables.
    ///
    /// Bricks are the exception: Resources/Bricks holds the original tile
    /// images, and BuildCellArt uses them when they are there. The primitive
    /// path below is kept rather than deleted, because it is what draws every
    /// brick if that folder ever goes missing.
    public static class Prefabs
    {
        /// One rigidbody, one box collider per cell. A compound body is what
        /// makes an L behave like an L: a single box around the bounding
        /// rectangle would let the piece rest on air where its notch is.
        public static GameObject MakeBrick(BrickHeroRow row, BrickShape shape, Transform parent)
        {
            var go = new GameObject($"Brick_{row.ID}_{row.Name}_{shape.Name}");
            go.transform.SetParent(parent, false);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // A little friction keeps the stack from sliding apart instantly.
            var mat = new PhysicsMaterial2D("BrickMat") { friction = 0.9f, bounciness = 0.02f };

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                // Colliders butt up exactly; the art is inset so the seams
                // read as separate cells, as the outlined cells do in the
                // reference art.
                var col = go.AddComponent<BoxCollider2D>();
                col.offset = shape.CellCenter(i);
                col.size = new Vector2(BrickShape.CellSize, BrickShape.CellSize);
                col.sharedMaterial = mat;
            }

            BuildCellArt(go.transform, row, shape, BrickUnit.BrickColor(row),
                         BrickUnit.ArtOrder, 1f);

            go.AddComponent<BrickUnit>();
            return go;
        }

        /// The translucent footprint that follows the cursor before the drop.
        public static GameObject MakeGhost(BrickHeroRow row, BrickShape shape, Transform parent)
        {
            var go = new GameObject("DropPreview");
            if (parent != null) go.transform.SetParent(parent, false);
            var c = BrickUnit.BrickColor(row);
            BuildCellArt(go.transform, row, shape, c, 20, 0.45f);
            return go;
        }

        /// The full-height translucent column that shows where the held piece
        /// will come down. Drawn as one stretched sprite; the caller sets its
        /// position, scale and colour every frame.
        public static GameObject MakeDropColumn(Transform parent)
        {
            var go = new GameObject("DropColumn");
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Shapes.Box;
            sr.color = new Color(0.35f, 0.9f, 0.35f, 0.16f);
            sr.sortingOrder = 1;            // behind the bricks, in front of the bg
            sr.drawMode = SpriteDrawMode.Simple;
            return go;
        }

        /// A flat bar: the white minimum-drop line, and the dashed edges of the
        /// legal zone above it.
        public static GameObject MakeBar(string name, Color color, int sortingOrder,
                                         Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Shapes.Box;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// Scale a unit Box sprite to cover an exact world-space size.
        public static void StretchTo(Transform t, float width, float height)
        {
            float k = Shapes.PPU / 64f;
            t.localScale = new Vector3(Mathf.Max(0.001f, width) * k,
                                       Mathf.Max(0.001f, height) * k, 1f);
        }

        /// The brick's face. Prefers the extracted tile image, which already
        /// draws the cell seams and the bevel; falls back to one flat square
        /// per cell so a 4-cell piece still reads as four occupied squares
        /// rather than one slab.
        ///
        /// The hero marker rides on top either way -- the tile images are the
        /// brick only, with the hero drawn over them in the original too.
        public static void BuildCellArt(Transform root, BrickHeroRow row, BrickShape shape,
                                        Color tint, int sortingOrder, float alpha,
                                        int level = 1)
        {
            if (BrickArt.Build(root, row, shape, level, sortingOrder, alpha))
            {
                BuildHeroMark(root, row, shape, sortingOrder + 2, alpha);
                return;
            }
            BuildPrimitiveArt(root, row, shape, tint, sortingOrder, alpha);
        }

        /// The one hero the brick carries. Prefers the baked figure, placed at
        /// the offset the original hand-placed for that orientation; falls back
        /// to an abstract pip at the middle of the footprint.
        static void BuildHeroMark(Transform root, BrickHeroRow row, BrickShape shape,
                                  int sortingOrder, float alpha)
        {
            if (Figures.BuildHero(root, row, shape, sortingOrder, alpha) != null) return;

            var at = Vector2.zero;
            for (int i = 0; i < shape.Cells.Length; i++) at += shape.CellCenter(i);
            at /= shape.Cells.Length;

            var pip = new GameObject("Hero");
            pip.transform.SetParent(root, false);
            pip.transform.localPosition = at;
            var sr = pip.AddComponent<SpriteRenderer>();
            sr.sprite = row != null && row.SkillType == 0 ? Shapes.Box : Shapes.Circle;
            var c = BrickUnit.RoleColor(row); c.a = alpha * 0.85f;
            sr.color = c;
            sr.sortingOrder = sortingOrder;
            float g = BrickShape.CellSize * 0.42f;
            pip.transform.localScale = new Vector3(g * Shapes.PPU / 64f,
                                                   g * Shapes.PPU / 64f, 1f);
        }

        static void BuildPrimitiveArt(Transform root, BrickHeroRow row, BrickShape shape,
                                      Color tint, int sortingOrder, float alpha)
        {
            const float inset = 0.94f;
            float tile = BrickShape.CellSize * inset;
            var face = tint; face.a = alpha;
            var glyph = BrickUnit.RoleColor(row) * 1.25f; glyph.a = alpha;

            // The figure is independent of the tile images, so it still shows
            // even on this path; only then is the pip needed.
            bool figure = Figures.BuildHero(root, row, shape, sortingOrder + 2, alpha) != null;

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                var at = shape.CellCenter(i);

                var cell = new GameObject($"Cell{i}");
                cell.transform.SetParent(root, false);
                cell.transform.localPosition = at;
                var sr = cell.AddComponent<SpriteRenderer>();
                sr.sprite = Shapes.Box;
                sr.color = face;
                sr.sortingOrder = sortingOrder;
                cell.transform.localScale = new Vector3(tile * Shapes.PPU / 64f,
                                                        tile * Shapes.PPU / 64f, 1f);

                if (figure) continue;

                // Fallback marker for a hero with no baked figure.
                var pip = new GameObject("Hero");
                pip.transform.SetParent(root, false);
                pip.transform.localPosition = at;
                var psr = pip.AddComponent<SpriteRenderer>();
                psr.sprite = row != null && row.SkillType == 0 ? Shapes.Box : Shapes.Circle;
                psr.color = glyph;
                psr.sortingOrder = sortingOrder + 1;
                float g = BrickShape.CellSize * 0.34f;
                pip.transform.localScale = new Vector3(g * Shapes.PPU / 64f,
                                                       g * Shapes.PPU / 64f, 1f);
            }
        }

        public static GameObject MakeEnemy(BrickEnemyRow row, Transform parent)
        {
            var go = new GameObject($"Enemy_{row.ID}_{row.Model}");
            go.transform.SetParent(parent, false);

            if (Figures.BuildEnemy(go.transform, row, 6) == null)
            {
                var art = new GameObject("Art");
                art.transform.SetParent(go.transform, false);
                var sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = row.IsBoss ? Shapes.Diamond : Shapes.Circle;
                sr.sortingOrder = 6;
                // Match the collider. EnemyUnit scales the root by row.Scale
                // plain now, so a cell across here is the same 0.8 * Scale the
                // 0.64-at-1.25 pair used to give.
                art.transform.localScale =
                    Vector3.one * (BrickShape.CellSize * Shapes.PPU / 64f);
            }

            go.AddComponent<Rigidbody2D>();
            var col = go.AddComponent<CircleCollider2D>();
            // A cell across, and EnemyUnit now scales the root by row.Scale
            // plain instead of by Scale * 1.25, so the product -- the world
            // collider -- is unchanged. See RefScale.EnemyMeasuredWidth.
            col.radius = BrickShape.CellSize * 0.5f;
            col.isTrigger = true;

            go.AddComponent<EnemyUnit>();
            return go;
        }

        public static GameObject MakeProjectile(SkillType skill, Transform parent)
        {
            var go = new GameObject("Shot_" + skill);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = skill == SkillType.Laser || skill == SkillType.Sniper
                ? Shapes.Box
                : Shapes.Circle;
            sr.color = SkillColor(skill);
            sr.sortingOrder = 8;

            float s = skill == SkillType.Thunder ? 0.35f : 0.2f;
            go.transform.localScale = new Vector3(s, s, 1f) * (Shapes.PPU / 64f);

            go.AddComponent<Projectile>();
            return go;
        }

        public static Color SkillColor(SkillType s)
        {
            switch (s)
            {
                case SkillType.Splash:  return new Color(1f, 0.55f, 0.25f);
                case SkillType.Lob:     return new Color(0.95f, 0.35f, 0.15f);
                case SkillType.Poison:  return new Color(0.45f, 0.85f, 0.35f);
                case SkillType.Freeze:  return new Color(0.45f, 0.8f, 1f);
                case SkillType.Laser:   return new Color(1f, 0.25f, 0.45f);
                case SkillType.Sniper:  return new Color(1f, 0.95f, 0.6f);
                case SkillType.Thunder: return new Color(0.7f, 0.6f, 1f);
                case SkillType.Ninja:   return new Color(0.85f, 0.85f, 0.9f);
                default:                return Color.white;
            }
        }
    }
}
