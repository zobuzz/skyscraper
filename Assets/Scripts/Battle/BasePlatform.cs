using System.Collections.Generic;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// The fixed pedestal every run starts on.
    ///
    /// Without it the first brick lands on bare ground and the tower has no
    /// footing: the run has no defined build site, and the stack spreads
    /// sideways instead of going up. The pedestal is static and indestructible,
    /// so it is deliberately NOT registered with BattleRuntime.RegisterBrick --
    /// counting it as a brick would make the "all bricks destroyed" lose check
    /// unreachable.
    ///
    /// BrickMap.Desk is 1..25 (plus a single -1) and only selects an art
    /// variant in the source data -- no table anywhere stores the pedestal's
    /// dimensions. So the geometry below is this project's own choice: tiles
    /// are one BrickShape cell square, matching the single row of square blocks
    /// the reference art puts under the tower. The count comes off the capture:
    /// the pedestal measures 281px against a 36px cell, and exactly twice the
    /// width of the 4-cell piece being dragged over it -- so eight. That is
    /// wide enough to land two pieces side by side, which is what makes the
    /// second storey possible, and still narrow enough that a careless drop
    /// slides off. Desk only picks the tint.
    public class BasePlatform : MonoBehaviour
    {
        public const int DefaultTiles = RefScale.BaseTiles;

        [Tooltip("Number of cell-sized tiles across.")]
        public int Tiles = DefaultTiles;
        public float TileWidth = BrickShape.CellSize;
        public float Height = BrickShape.CellSize;

        /// Surface the first brick lands on -- the drop preview and any
        /// future auto-placement should aim here.
        public float TopY { get; private set; }
        public float HalfWidth { get; private set; }
        public float CenterX { get; private set; }

        readonly List<GameObject> _tiles = new List<GameObject>();

        public void Build(int desk, Transform parent, float groundY, float centerX = 0f)
        {
            Clear();

            Tiles = Mathf.Max(1, Tiles);
            CenterX = centerX;
            HalfWidth = Tiles * TileWidth * 0.5f;
            TopY = groundY + Height;

            transform.SetParent(parent, false);
            transform.position = new Vector3(centerX, groundY, 0f);

            var tint = DeskTint(desk);

            for (int i = 0; i < Tiles; i++)
            {
                float x = centerX - HalfWidth + TileWidth * (i + 0.5f);

                var go = new GameObject($"Desk_{desk}_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(x, groundY + Height * 0.5f, 0f);

                // Static body: the pedestal must not be pushed by the stack it
                // carries, and a static collider is also cheaper than a
                // kinematic one for something that never moves.
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(TileWidth, Height);
                col.sharedMaterial = new PhysicsMaterial2D("DeskMat")
                {
                    friction = 0.95f,
                    bounciness = 0f
                };

                var art = new GameObject("Art");
                art.transform.SetParent(go.transform, false);
                art.AddComponent<SpriteRenderer>();
                var rs = art.AddComponent<RuntimeSprite>();
                rs.Shape = RuntimeSprite.Kind.Box;
                // Alternate the shade so the tile seams are visible without art.
                rs.Tint = i % 2 == 0 ? tint : tint * 0.85f;
                rs.SortingOrder = 3;
                rs.Size = new Vector2(TileWidth, Height);
                rs.Apply();

                _tiles.Add(go);
            }
        }

        public void Clear()
        {
            foreach (var t in _tiles)
                if (t != null) Destroy(t);
            _tiles.Clear();
        }

        /// Desk is an art id in the source tables, so it only drives colour
        /// here. Spread the hue over the 25 known variants; -1 falls back.
        public static Color DeskTint(int desk)
        {
            if (desk < 1) return new Color(0.45f, 0.47f, 0.52f);
            float h = ((desk - 1) * 0.11f) % 1f;
            return Color.HSVToRGB(h, 0.28f, 0.62f);
        }

        public override string ToString() =>
            $"tiles={Tiles} width={Tiles * TileWidth:0.##} topY={TopY:0.##} centerX={CenterX:0.##}";
    }
}
