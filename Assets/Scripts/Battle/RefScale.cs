using UnityEngine;

namespace Skyscraper.Battle
{
    /// Single source of truth for "how big should this be".
    ///
    /// Every number here was measured off the two reference captures
    /// (1125x2436 native) and is stored in those reference pixels, so a
    /// constant can always be checked against the screenshot it came from
    /// rather than being a tuned-by-eye magic number.
    ///
    /// Two conversions come out of it:
    ///   * UI  -- Px()/Font() scale reference pixels to the live screen width.
    ///     Width, not height: the reference is 19.5:9 and a 16:9 screen is
    ///     relatively shorter, so anchoring on height would shrink everything
    ///     on the wider display. Horizontal layout is what must match.
    ///   * World -- one grid cell is a fixed fraction of the screen (CellPx of
    ///     RefWidth), which pins the camera's framing instead of the camera
    ///     framing deciding how large a brick looks.
    public static class RefScale
    {
        public const float RefWidth = 1125f;
        public const float RefHeight = 2436f;

        // --- measured off the captures --------------------------------------
        /// One footprint cell. Read off the piece in mid-air at 15:38:00: its
        /// top row of fill runs about 129px, and the piece is three cells wide,
        /// so 43px. The card slot behind it and the drop column agree.
        ///
        /// 36 rather than 43 because a cell of art is not a cell of footprint:
        /// the tiles carry a bevel and a drop shadow that overhang the grid, and
        /// pinning the grid to the drawn edge would leave the tower visibly
        /// gapped. Do not re-derive this from the mid-air piece's bounding box
        /// -- the hero figure occludes its lower row, which reads as a stub of a
        /// cell and makes the piece look one wider than it is. The sprites are
        /// the authority on cell counts; see BrickShape.
        public const float CellPx = 36f;
        /// Pedestal: 281px wide, 39px tall -- 7.8 x 1.08 cells.
        public const int BaseTiles = 8;
        /// Screen bottom to the top face of the pedestal.
        public const float GroundFromBottomPx = 807f;

        // Bottom furniture, all measured bottom-up so the layout survives a
        // shorter screen.
        public const float CardWPx = 284f, CardHPx = 394f, CardGapPx = 61f;
        public const float CardBottomPx = 97f;
        public const float HpBarWPx = 979f, HpBarHPx = 24f, HpBarBottomPx = 710f;
        public const float RerollWPx = 277f, RerollHPx = 71f;
        public const float RerollRightPx = 68f, RerollBottomPx = 558f;
        public const float RulerXPx = 67f, RulerWPx = 28f;

        // Glyph heights, measured on the captures.
        public const float FontTitlePx = 46f;   // 第1/30波
        public const float FontCostPx = 43f;    // card price, gold counter
        public const float FontHpPx = 30f;      // 1000 on the base bar
        public const float FontBodyPx = 26f;
        public const float FontTinyPx = 21f;    // ruler rungs, 攻击+20% badge

        // --- UI conversion ---------------------------------------------------
        public static float S => Screen.width / RefWidth;
        public static float Px(float refPx) => refPx * S;
        public static int Font(float refPx) => Mathf.Max(8, Mathf.RoundToInt(refPx * S));

        // --- world conversion -------------------------------------------------
        /// World units per reference pixel, fixed by the cell size the physics
        /// runs at. Changing BrickShape.CellSize rescales the camera, not the
        /// on-screen size of a brick -- which is the point.
        public const float WorldPerPx = BrickShape.CellSize / CellPx;

        /// Visible world width that puts a cell at CellPx on screen: 25 units.
        public const float ViewWidth = RefWidth * WorldPerPx;

        /// Ground line's distance above the bottom edge of the view.
        public const float GroundFromBottom = GroundFromBottomPx * WorldPerPx;

        public static float OrthoSize(float aspect) =>
            ViewWidth / (2f * Mathf.Max(0.1f, aspect));

        // --- source-table units -----------------------------------------------
        /// Lengths in BrickMap/BrickEnemy are in the original project's units,
        /// not ours. Chapter 1's battlefield is MonsterLeft/Right = +-4, and the
        /// captures show enemies entering from both screen edges -- so 8 source
        /// units span the screen, putting the original cell at 8/31.25 = 0.256
        /// against our 0.8. Every length read out of those tables goes through
        /// FromSource; that it lands the bounds exactly on the view edges is the
        /// check that the factor is right, not a coincidence to lean on.
        public const float SourceViewWidth = 8f;
        public const float FromSource = ViewWidth / SourceViewWidth;   // 3.125

        /// BrickEnemy.Scale reads as a diameter in cells: Scale 1 is one cell
        /// across, Scale 2 is two. The table spreads 0.5 to 3.0 over 26 enemies,
        /// and the flyer at the right edge of the 1-1 capture measures ~45
        /// reference px against Scale 1.25, which is this rule to within a
        /// couple of pixels.
        ///
        /// Pinning the rule to Scale 1 rather than to one measured sprite is the
        /// correction that matters: 1-1's opening wave is Scale 2.0, so treating
        /// the 72px it measures as the Scale-1 size doubled every enemy in the
        /// game and put four cells' worth of monster next to a one-cell brick.
        ///
        /// The rule survives, but nothing multiplies by it any more: the
        /// original's own monster prefabs carry localScale == Scale (17 of 20
        /// match their table row exactly), and the figures are baked at the
        /// rigs' 0.01, so EnemyUnit sets Scale plain and the cell conversion
        /// happens once, in Figures.EnemyUnit. Monster_1 lands at 70.6 reference
        /// px against the 72 measured here, which is what closes the loop --
        /// the number above is now a check on the art rather than a scale factor
        /// applied to it.
        public const float EnemyMeasuredWidth = 0.64f;
    }
}
