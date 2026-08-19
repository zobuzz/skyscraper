using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// One of the three bricks currently on offer. The hero and the footprint
    /// are rolled together and stay bound until the card is played or rerolled,
    /// which is what makes the choice a choice: the player is picking a shape as
    /// much as a hero.
    public class HandCard
    {
        public BrickHeroRow Row;
        public BrickShape Shape;
        public int Cost => Row != null ? Row.Cost : 0;
        public override string ToString() =>
            Row == null ? "(empty)" : $"{Row.Name} {Shape} {Cost}g";
    }

    /// Offers a hand of three bricks and drops the chosen one where the player
    /// releases the drag. The brick falls under gravity and stacks -- placement
    /// is a physics problem, not a grid lookup.
    ///
    /// Two rules from the reference are enforced here rather than in the HUD,
    /// because they decide whether a placement happens at all:
    ///   * the release point must be above the white line (the top of the
    ///     stack), so pieces land on the tower instead of inside it;
    ///   * a full-height translucent column shows the fall path before release.
    public class BrickDropper : MonoBehaviour
    {
        [Header("Drop")]
        [Tooltip("How tall the legal release band is above the white line. The " +
                 "band travels up with the stack -- a fixed ceiling would cap " +
                 "the tower a few metres up and make the top height bands " +
                 "unreachable, which would leave the ruler decorative.")]
        /// 8 cells. Measured off the 1-1 capture, where the band runs from about
        /// 786 to 1145 reference px -- 359 px, near enough 10 cells, of which the
        /// bottom two sit below the white line in that shot. 4 came out at half
        /// the reference height on screen.
        public float DropBandHeight = 8f * BrickShape.CellSize;

        /// Top of the legal release band, i.e. the drag ceiling.
        public float DropHeight => _runtime != null
            ? _runtime.DropLineY + DropBandHeight
            : DropBandHeight;

        public KeyCode RerollKey = KeyCode.R;
        public KeyCode RotateKey = KeyCode.Q;

        [Header("Hand")]
        public int HandSize = 3;

        [Tooltip("Gold per reroll. Global has no battle-reroll column of its " +
                 "own; ChallRefreshCost (20) is the only refresh price in the " +
                 "table set, so it is used here. The reference screenshot shows " +
                 "25, which no extracted table produces.")]
        public int RerollCostFallback = 20;

        [Header("Pool")]
        [Tooltip("BrickHero.UnLock is the player level a hero unlocks at, not " +
                 "a flag: the column holds 1,2,3,5,8,9,10,15,20,25,9999.")]
        public int PlayerLevel = 1;

        /// 9999 marks heroes that never enter the normal pool (雷神, 行者);
        /// the original sources them elsewhere.
        public const int NeverUnlocks = 9999;

        BattleRuntime _runtime;
        BattleContext _ctx;
        Camera _cam;

        readonly List<HandCard> _hand = new List<HandCard>();
        int _selected;

        GameObject _preview;
        SpriteRenderer[] _previewArt = new SpriteRenderer[0];
        GameObject _column, _line, _zone, _zoneTop;
        SpriteRenderer _columnArt, _lineArt, _zoneArt, _zoneTopArt;

        bool _dragging;
        Vector2 _dragPoint;
        float _rejectedAt = -99f;

        /// Set by the HUD while the cursor is over a card or a button. Without
        /// it, clicking a card would also begin a drag underneath it.
        public bool PointerBlocked;

        readonly List<BrickHeroRow> _pool = new List<BrickHeroRow>();

        public IReadOnlyList<HandCard> Hand => _hand;
        public int SelectedIndex => _selected;
        public HandCard SelectedCard =>
            _selected >= 0 && _selected < _hand.Count ? _hand[_selected] : null;

        public bool Dragging => _dragging;
        /// True while the drag is somewhere a drop would be refused -- the HUD
        /// and the column both colour off this.
        public bool DragLegal { get; private set; }
        /// Seconds since a drop was refused, for the HUD's warning flash.
        public float SinceRejected => Time.time - _rejectedAt;

        // Kept so the probe and the HUD keep reading a single "held" brick.
        public BrickHeroRow Current => SelectedCard != null ? SelectedCard.Row : null;
        public BrickShape CurrentShape => SelectedCard != null ? SelectedCard.Shape : null;
        public int CurrentCost => SelectedCard != null ? SelectedCard.Cost : 0;

        public IReadOnlyList<BrickHeroRow> Pool => _pool;

        public int RerollCost =>
            ConfigDB.Global != null && ConfigDB.Global.ChallRefreshCost > 0
                ? ConfigDB.Global.ChallRefreshCost
                : RerollCostFallback;

        public void Begin(BattleRuntime runtime, BattleContext ctx)
        {
            _runtime = runtime;
            _ctx = ctx;
            _cam = Camera.main;

            _pool.Clear();
            foreach (var h in ConfigDB.Heroes)
                if (IsUnlocked(h, PlayerLevel)) _pool.Add(h);
            if (_pool.Count == 0) _pool.AddRange(ConfigDB.Heroes);

            _hand.Clear();
            for (int i = 0; i < Mathf.Max(1, HandSize); i++) _hand.Add(Draw());
            _selected = 0;

            BuildMarkers();
            RebuildPreview();
        }

        public static bool IsUnlocked(BrickHeroRow h, int playerLevel) =>
            h.UnLock < NeverUnlocks && h.UnLock <= playerLevel;

        // --- hand ----------------------------------------------------------
        /// Quality tier is picked from Global.HeroQualityWeight ("80|15|5"),
        /// then RandomTimes weights the draw inside that tier -- the same two
        /// stage roll the original config implies.
        public HandCard Draw()
        {
            var tierW = Parse.Ints(ConfigDB.Global != null ? ConfigDB.Global.HeroQualityWeight : "80|15|5");
            if (tierW.Count == 0) tierW = new List<int> { 80, 15, 5 };

            int total = 0;
            foreach (var w in tierW) total += w;
            int roll = Random.Range(0, Mathf.Max(1, total));
            int tier = 1;
            for (int i = 0; i < tierW.Count; i++)
            {
                if (roll < tierW[i]) { tier = i + 1; break; }
                roll -= tierW[i];
            }

            var candidates = _pool.FindAll(h => h.Quality == tier);
            if (candidates.Count == 0) candidates = _pool;

            int wsum = 0;
            foreach (var h in candidates) wsum += Mathf.Max(1, h.RandomTimes);
            int pick = Random.Range(0, Mathf.Max(1, wsum));
            var row = candidates[candidates.Count - 1];
            foreach (var h in candidates)
            {
                int w = Mathf.Max(1, h.RandomTimes);
                if (pick < w) { row = h; break; }
                pick -= w;
            }

            // The footprint is rolled separately from the hero -- see the note
            // on BrickShape for why the tables cannot supply it.
            return new HandCard { Row = row, Shape = BrickShape.Roll(row) };
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _hand.Count || index == _selected) return;
            _selected = index;
            _dragging = false;
            RebuildPreview();
        }

        /// Rerolls the whole hand, which is what the reference's single reroll
        /// button does: it replaces the offer, not one card.
        public bool TryReroll()
        {
            if (!_ctx.TrySpend(RerollCost)) { _rejectedAt = Time.time; return false; }
            for (int i = 0; i < _hand.Count; i++) _hand[i] = Draw();
            _selected = 0;
            _dragging = false;
            RebuildPreview();
            return true;
        }

        void Rotate()
        {
            var card = SelectedCard;
            if (card == null || card.Shape == null) return;
            card.Shape = card.Shape.Rotated();
            RebuildPreview();
        }

        // --- markers -------------------------------------------------------
        void BuildMarkers()
        {
            if (_column != null) return;

            _column = Prefabs.MakeDropColumn(transform);
            _columnArt = _column.GetComponent<SpriteRenderer>();

            // The legal band and its two edges, as the reference draws them.
            _zone = Prefabs.MakeBar("DropZone", new Color(0.35f, 0.9f, 0.4f, 0.07f), 0, transform);
            _zoneArt = _zone.GetComponent<SpriteRenderer>();
            _zoneTop = Prefabs.MakeBar("DropZoneTop", new Color(0.45f, 1f, 0.5f, 0.5f), 2, transform);
            _zoneTopArt = _zoneTop.GetComponent<SpriteRenderer>();

            // The white line itself sits on top of everything else down here:
            // it is the rule the player has to read at a glance.
            _line = Prefabs.MakeBar("DropLine", Color.white, 12, transform);
            _lineArt = _line.GetComponent<SpriteRenderer>();
        }

        void RebuildPreview()
        {
            if (_preview != null) Destroy(_preview);
            _previewArt = new SpriteRenderer[0];
            var card = SelectedCard;
            if (card == null || card.Shape == null) return;
            _preview = Prefabs.MakeGhost(card.Row, card.Shape, null);
            _previewArt = _preview.GetComponentsInChildren<SpriteRenderer>();
        }

        void SetMarkersVisible(bool on)
        {
            // The band is permanent, not a drag-time affordance: the capture of
            // 1-1's opening shows it filled across the whole battlefield with
            // dashed edges top and bottom, and the "drag a block above the white
            // line" prompt sits inside it. Only the column and the ghost follow
            // the cursor.
            if (_column != null) _column.SetActive(on && _dragging);
            if (_zone != null) _zone.SetActive(on);
            if (_zoneTop != null) _zoneTop.SetActive(on);
            if (_line != null) _line.SetActive(on);
            if (_preview != null) _preview.SetActive(on && _dragging);
        }

        // --- loop ----------------------------------------------------------
        void Update()
        {
            if (_ctx == null || _ctx.Phase != BattlePhase.Running || SelectedCard == null)
            {
                _dragging = false;
                SetMarkersVisible(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
            if (Input.GetKeyDown(RerollKey)) TryReroll();
            if (Input.GetKeyDown(RotateKey) || Input.GetMouseButtonDown(1)) Rotate();

            if (Input.GetMouseButtonDown(0) && !PointerBlocked) _dragging = true;
            if (_dragging) _dragPoint = DragPoint();
            SetMarkersVisible(true);

            float lineY = _runtime.DropLineY;
            DragLegal = _dragging
                        && Clears(_dragPoint.y, SelectedCard)
                        && _ctx.Gold >= SelectedCard.Cost;

            LayoutMarkers(lineY);

            if (_dragging && Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                if (DragLegal) Place(_dragPoint);
                else _rejectedAt = Time.time;
                SetMarkersVisible(true);
            }
        }

        /// Cursor position in world space, clamped so the footprint stays inside
        /// the play area and under the drag ceiling. The vertical clamp
        /// deliberately does NOT enforce the white line -- if it did, the rule
        /// could never be broken and the player would never see why.
        Vector2 DragPoint()
        {
            var w = MouseWorld();
            float y = Mathf.Clamp(w.y, _runtime.GroundY, DropHeight);
            return new Vector2(ClampDropX(w.x), y);
        }

        void LayoutMarkers(float lineY)
        {
            var card = SelectedCard;
            float left = _ctx.LeftBound, right = _ctx.RightBound;
            float width = right - left;
            float midX = (left + right) * 0.5f;

            // White line: the floor of the legal release band.
            _line.transform.position = new Vector3(midX, lineY, 0f);
            Prefabs.StretchTo(_line.transform, width, 0.05f);

            // The band between the line and the drag ceiling.
            float bandH = Mathf.Max(0.02f, DropHeight - lineY);
            _zone.transform.position = new Vector3(midX, lineY + bandH * 0.5f, 0f);
            Prefabs.StretchTo(_zone.transform, width, bandH);
            _zoneTop.transform.position = new Vector3(midX, DropHeight, 0f);
            Prefabs.StretchTo(_zoneTop.transform, width, 0.04f);

            if (_dragging && card != null)
            {
                // Column runs from the pedestal top to the top of the view, the
                // whole fall path rather than just the gap under the piece.
                float baseTop = _runtime.Base != null ? _runtime.Base.TopY : _runtime.GroundY;
                float top = _cam != null
                    ? _cam.transform.position.y + _cam.orthographicSize
                    : DropHeight + 2f;
                float h = Mathf.Max(0.1f, top - baseTop);

                _column.transform.position = new Vector3(_dragPoint.x, baseTop + h * 0.5f, 0f);
                Prefabs.StretchTo(_column.transform, card.Shape.Size.x, h);
                _columnArt.color = DragLegal
                    ? new Color(0.35f, 0.9f, 0.35f, 0.16f)
                    : new Color(0.95f, 0.3f, 0.3f, 0.16f);

                _preview.transform.position = new Vector3(_dragPoint.x, _dragPoint.y, 0f);
                float a = DragLegal ? 0.6f : 0.18f;
                foreach (var sr in _previewArt)
                {
                    if (sr == null) continue;
                    var c = sr.color; c.a = a; sr.color = c;
                }
            }

            var edge = DragLegal || !_dragging
                ? new Color(0.45f, 1f, 0.5f, 0.5f)
                : new Color(1f, 0.45f, 0.45f, 0.6f);
            _zoneTopArt.color = edge;
            _lineArt.color = _dragging && !DragLegal
                ? new Color(1f, 0.55f, 0.55f, 1f)
                : Color.white;
        }

        /// The line is a rule about the footprint, not about the cursor: what has
        /// to be above it is the piece's bottom edge. Testing the centre instead
        /// would let a 3-cell-tall piece spawn with its lower cells already
        /// inside the stack, and the physics engine would then shove the tower
        /// apart resolving the overlap.
        public bool Clears(float centreY, HandCard card)
        {
            if (card == null || card.Shape == null) return false;
            return centreY - card.Shape.Size.y * 0.5f >= _runtime.DropLineY;
        }

        /// Lowest centre height the held piece may be released at.
        public float MinCentreY => SelectedCard != null && SelectedCard.Shape != null
            ? _runtime.DropLineY + SelectedCard.Shape.Size.y * 0.5f
            : _runtime.DropLineY;

        /// Keep the whole footprint inside the play area, not just its centre:
        /// half of a 3-wide piece hanging past the edge would be unrecoverable.
        float ClampDropX(float x)
        {
            var card = SelectedCard;
            float half = card != null && card.Shape != null ? card.Shape.Size.x * 0.5f : 0f;
            float lo = _ctx.LeftBound + half, hi = _ctx.RightBound - half;
            return lo > hi ? (_ctx.LeftBound + _ctx.RightBound) * 0.5f : Mathf.Clamp(x, lo, hi);
        }

        Vector2 MouseWorld()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return Vector2.zero;
            var mp = Input.mousePosition;
            mp.z = Mathf.Abs(_cam.transform.position.z);
            var w = _cam.ScreenToWorldPoint(mp);
            return new Vector2(w.x, w.y);
        }

        /// Spends the card and puts the brick in the world. The played slot is
        /// refilled, so the hand is always three deep.
        public bool Place(Vector2 at)
        {
            var card = SelectedCard;
            if (card == null || card.Shape == null) return false;
            if (!Clears(at.y, card)) { _rejectedAt = Time.time; return false; }
            if (!_ctx.TrySpend(card.Cost)) { _rejectedAt = Time.time; return false; }

            var go = Prefabs.MakeBrick(card.Row, card.Shape, _runtime.BrickRoot);
            go.transform.position = new Vector3(ClampDropX(at.x), at.y, 0f);

            var unit = go.GetComponent<BrickUnit>();
            unit.Init(card.Row, card.Shape, 1, _ctx, _runtime);
            _runtime.RegisterBrick(unit);

            var merger = _runtime.GetComponent<MergeSystem>();
            if (merger != null) merger.Watch(unit);

            _hand[_selected] = Draw();
            RebuildPreview();
            return true;
        }
    }
}
