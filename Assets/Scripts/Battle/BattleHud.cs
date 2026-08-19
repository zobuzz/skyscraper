using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Minimal IMGUI overlay. Deliberately not uGUI: no prefabs, no canvas
    /// wiring, nothing to re-link when the scene is regenerated.
    ///
    /// Five pieces mirror the reference layout: the stat block up top, the
    /// height/attack ruler down the left edge, the base HP bar, the reroll pill
    /// on its own row, and the hand of three cards along the bottom.
    ///
    /// Every size here goes through RefScale rather than being a literal pixel
    /// count. IMGUI has no canvas scaler, so hard-coded numbers are silently
    /// resolution-dependent -- the original constants were authored against a
    /// 1080-wide view and came out roughly 2.4x too small against the reference
    /// capture. Reference pixels keep the comparison checkable.
    public class BattleHud : MonoBehaviour
    {
        BattleRuntime _runtime;
        BrickDropper _dropper;
        GUIStyle _big, _small, _tiny, _cardName, _cardCost, _hpText;
        Texture2D _px;
        int _styleWidth = -1;

        static float Px(float refPx) => RefScale.Px(refPx);

        float CardW => Px(RefScale.CardWPx);
        float CardH => Px(RefScale.CardHPx);
        float CardGap => Px(RefScale.CardGapPx);
        float HandTop => Screen.height - Px(RefScale.CardBottomPx) - CardH;

        float HpBarH => Px(RefScale.HpBarHPx);
        float HpBarTop => Screen.height - Px(RefScale.HpBarBottomPx) - HpBarH;
        float HintTop => HpBarTop - Px(RefScale.FontBodyPx) - Px(10f);

        readonly List<Rect> _blockers = new List<Rect>();

        void Awake()
        {
            _runtime = GetComponent<BattleRuntime>();
            _dropper = GetComponent<BrickDropper>();
        }

        /// Rebuilt when the view is resized: font sizes are baked into the
        /// style, so a cached style would keep the old scale after a resolution
        /// change.
        void EnsureStyles()
        {
            if (_big != null && _styleWidth == Screen.width) return;
            _styleWidth = Screen.width;

            _big = new GUIStyle(GUI.skin.label)
            { fontSize = RefScale.Font(RefScale.FontTitlePx), fontStyle = FontStyle.Bold };
            _big.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = RefScale.Font(RefScale.FontBodyPx) };
            _small.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            _tiny = new GUIStyle(GUI.skin.label) { fontSize = RefScale.Font(RefScale.FontTinyPx) };
            _tiny.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
            _cardName = new GUIStyle(GUI.skin.label)
            {
                fontSize = RefScale.Font(RefScale.FontBodyPx), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _cardName.normal.textColor = Color.white;
            _cardCost = new GUIStyle(GUI.skin.label)
            {
                fontSize = RefScale.Font(RefScale.FontCostPx), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _cardCost.normal.textColor = new Color(1f, 0.87f, 0.35f);
            _hpText = new GUIStyle(GUI.skin.label)
            {
                fontSize = RefScale.Font(RefScale.FontHpPx), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hpText.normal.textColor = Color.white;

            if (_px != null) return;
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        /// The dropper must not start a drag under a card the player just
        /// clicked, so the blocking rects are published every frame.
        void Update()
        {
            if (_dropper == null) return;
            var m = Input.mousePosition;
            var p = new Vector2(m.x, Screen.height - m.y);   // GUI space
            bool over = false;
            foreach (var r in _blockers) if (r.Contains(p)) { over = true; break; }
            _dropper.PointerBlocked = over;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = old;
        }

        /// Paint a brick's tile image into `box`, turned the way the card was
        /// dealt, so the face on the card is the face that lands.
        ///
        /// `box` is the footprint's box after the turn, but the image is drawn
        /// before it, so the untransformed rect uses the canonical cube's cell
        /// counts -- a quarter turn swaps them back. Mirror is applied under the
        /// rotation, matching BrickShape.Oriented; GUI angles run clockwise
        /// because GUI y points down, which is why this angle is positive where
        /// the world-space one is negative.
        void DrawShapeArt(Rect box, Sprite sprite, BrickShape shape, Color tint)
        {
            var canon = shape.Canonical;
            float cell = Mathf.Min(box.width / shape.CellsWide, box.height / shape.CellsHigh);
            var mid = box.center;
            var flat = new Rect(mid.x - canon.CellsWide * cell * 0.5f,
                                mid.y - canon.CellsHigh * cell * 0.5f,
                                canon.CellsWide * cell, canon.CellsHigh * cell);

            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height,
                              tr.width / tex.width, tr.height / tex.height);

            var oldM = GUI.matrix;
            var oldC = GUI.color;
            GUIUtility.RotateAroundPivot(90f * shape.Rot, mid);
            if (shape.Flip) GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), mid);
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(flat, tex, uv, true);
            GUI.color = oldC;
            GUI.matrix = oldM;
        }

        void OnGUI()
        {
            if (_runtime == null || _runtime.Ctx == null || _runtime.Ctx.Map == null) return;
            EnsureStyles();
            _blockers.Clear();

            DrawStats();
            DrawRuler();
            DrawBaseHp();
            DrawReroll();
            DrawHand();
            DrawHint();
        }

        /// The base's HP bar. The reference runs it across the bottom rather
        /// than perching it on the pedestal, which is itself part of the
        /// argument that the pool belongs to the run and not to any one brick
        /// -- see BaseHealth.
        void DrawBaseHp()
        {
            var hp = _runtime.BaseHp;
            if (hp == null) return;

            float w = Px(RefScale.HpBarWPx);
            var r = new Rect((Screen.width - w) * 0.5f, HpBarTop, w, HpBarH);
            float edge = Mathf.Max(1f, Px(3f));

            Fill(new Rect(r.x - edge, r.y - edge, r.width + edge * 2f, r.height + edge * 2f),
                 new Color(0f, 0f, 0f, 0.55f));
            Fill(r, new Color(0.10f, 0.11f, 0.13f, 0.95f));

            float k = hp.Ratio;
            // Green while healthy, amber from a third down, red in the last
            // fifth: the colour is the only warning the player gets, since the
            // bar is far from where they are looking.
            var col = k > 0.5f ? Color.Lerp(new Color(0.95f, 0.75f, 0.20f), new Color(0.30f, 0.85f, 0.35f),
                                            Mathf.InverseLerp(0.5f, 1f, k))
                               : Color.Lerp(new Color(0.90f, 0.25f, 0.25f), new Color(0.95f, 0.75f, 0.20f),
                                            Mathf.InverseLerp(0f, 0.5f, k));
            float inset = Mathf.Max(1f, Px(2f));
            if (k > 0f)
                Fill(new Rect(r.x + inset, r.y + inset, (r.width - inset * 2f) * k, r.height - inset * 2f), col);

            GUI.Label(r, $"{hp.Hp:0} / {hp.MaxHp:0}", _hpText);

            // The wall's contribution, called out separately: without it there
            // is no visible reason to ever spend gold on a 城墙.
            if (hp.BrickBonus > 0.0)
            {
                var bonus = new GUIStyle(_tiny) { alignment = TextAnchor.MiddleRight };
                bonus.normal.textColor = new Color(0.6f, 1f, 0.7f);
                GUI.Label(new Rect(r.x, r.y, r.width - Px(12f), r.height),
                          $"方块 +{hp.BrickBonus:0}", bonus);
            }
        }

        void DrawStats()
        {
            var ctx = _runtime.Ctx;
            GUILayout.BeginArea(new Rect(Px(30f), Px(24f), Px(860f), Px(340f)));

            GUILayout.Label($"{ctx.Map.Title}  {ctx.Map.ChapterTitle}", _big);
            GUILayout.Label($"金币 {ctx.Gold}    波次 {ctx.WaveIndex + 1}/{ctx.TotalWaves}    " +
                            $"砖块 {_runtime.Bricks.Count}    敌人 {_runtime.AliveEnemies}    " +
                            $"掉落损失 {_runtime.BricksLostToGround}", _small);
            GUILayout.Label($"塔高 {_runtime.TowerMetres:0.0}M    " +
                            $"攻击加成 +{HeightBonus.AttackAdd(_runtime.TowerMetres) * 100f:0}%    " +
                            $"白线 y={_runtime.DropLineY:0.00}", _small);

            if (ctx.ActiveChallenges.Count > 0)
            {
                string names = "";
                foreach (var id in ctx.ActiveChallenges)
                {
                    var row = ConfigDB.ChallengeById != null && ConfigDB.ChallengeById.TryGetValue(id, out var c) ? c : null;
                    names += (row != null ? row.Name : id.ToString()) + " ";
                }
                GUILayout.Label("挑战: " + names.Trim(), _small);
            }

            if (ctx.Phase == BattlePhase.Won) GUILayout.Label("通关", _big);
            if (ctx.Phase == BattlePhase.Lost) GUILayout.Label("失败", _big);

            GUILayout.EndArea();
        }

        /// The height ruler: one equal segment per band, the reached bands lit,
        /// the rest dimmed, with the bonus printed on each rung.
        void DrawRuler()
        {
            var bands = HeightBonus.Bands;
            int segs = bands.Count - 1;
            if (segs <= 0) return;

            float rulerX = Px(RefScale.RulerXPx);
            float rulerW = Px(RefScale.RulerWPx);
            float labelX = rulerX + rulerW + Px(14f);
            float labelW = Px(330f);
            float labelH = Px(RefScale.FontTinyPx * 1.5f);

            float bottom = HintTop - Px(20f);
            // The ruler's length is the one thing that cannot follow the
            // reference: it spans 815px of a 2436-tall screen there, and a 16:9
            // view simply has less room between the stat block and the HUD. Its
            // width and labels still match; only the run is compressed.
            float top = Px(400f);
            float h = bottom - top;
            if (h < Px(140f)) return;

            float metres = _runtime.TowerMetres;
            int cur = HeightBonus.BandIndex(metres);

            float pad = Px(4f);
            Fill(new Rect(rulerX - pad, top - pad, rulerW + pad * 2f, h + pad * 2f),
                 new Color(0f, 0f, 0f, 0.45f));

            float segH = h / segs;
            for (int i = 0; i < segs; i++)
            {
                // Segment i spans band i's threshold up to band i+1's.
                var b = bands[i];
                float y = bottom - segH * (i + 1);
                var c = b.Tint;
                if (i > cur) c *= 0.35f;
                c.a = 1f;
                Fill(new Rect(rulerX, y, rulerW, segH - Px(2f)), c);

                // Rung label sits on the boundary this segment ends at, which
                // is the height you must reach to earn the next band.
                var next = bands[i + 1];
                var lbl = new Rect(labelX, y - labelH * 0.5f, labelW, labelH);
                bool earned = metres >= next.Metres;
                _tiny.normal.textColor = earned
                    ? new Color(1f, 0.95f, 0.6f)
                    : new Color(0.75f, 0.78f, 0.84f);
                GUI.Label(lbl, $"{next.Label}  {next.BonusLabel}", _tiny);
            }
            _tiny.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
            GUI.Label(new Rect(labelX, bottom - labelH * 0.5f, labelW, labelH), "0M", _tiny);

            // Current-height marker.
            float f = HeightBonus.RulerFraction(metres);
            float my = bottom - h * f;
            float mh = Mathf.Max(2f, Px(6f));
            Fill(new Rect(rulerX - Px(8f), my - mh * 0.5f, rulerW + Px(16f), mh), Color.white);
            GUI.Label(new Rect(labelX, my - labelH * 1.4f, labelW, labelH), $"▲ {metres:0.0}M", _tiny);
        }

        /// The reroll sits on its own row above the hand, right-aligned, where
        /// the reference puts it -- not tucked in beside the cards. Keeping it
        /// out of the strip is what lets three cards at reference width fit.
        void DrawReroll()
        {
            if (_dropper == null) return;
            var ctx = _runtime.Ctx;

            float w = Px(RefScale.RerollWPx), h = Px(RefScale.RerollHPx);
            var rr = new Rect(Screen.width - Px(RefScale.RerollRightPx) - w,
                              Screen.height - Px(RefScale.RerollBottomPx) - h, w, h);
            _blockers.Add(rr);

            bool can = ctx.Gold >= _dropper.RerollCost;
            Fill(new Rect(rr.x - Px(3f), rr.y - Px(3f), rr.width + Px(6f), rr.height + Px(6f)),
                 new Color(0f, 0f, 0f, 0.5f));
            Fill(rr, can ? new Color(0.95f, 0.62f, 0.22f, 0.95f)
                         : new Color(0.28f, 0.26f, 0.24f, 0.95f));

            var label = new GUIStyle(_cardCost);
            label.normal.textColor = can ? new Color(1f, 0.99f, 0.90f) : new Color(0.62f, 0.60f, 0.58f);
            GUI.Label(rr, $"⇄ {_dropper.RerollCost}", label);
            if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) _dropper.TryReroll();
        }

        /// The hand of three. Clicking a card selects it; the piece is only
        /// spent when the drag is released above the white line.
        void DrawHand()
        {
            if (_dropper == null || _dropper.Hand.Count == 0) return;
            var hand = _dropper.Hand;
            var ctx = _runtime.Ctx;

            float totalW = hand.Count * CardW + (hand.Count - 1) * CardGap;
            float x = (Screen.width - totalW) * 0.5f;
            float y = HandTop;

            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                var r = new Rect(x + i * (CardW + CardGap), y, CardW, CardH);
                _blockers.Add(r);
                DrawCard(r, card, i == _dropper.SelectedIndex, ctx.Gold >= card.Cost);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _dropper.Select(i);
            }
        }

        void DrawCard(Rect r, HandCard card, bool selected, bool affordable)
        {
            var body = selected ? new Color(0.16f, 0.34f, 0.24f, 0.95f)
                                : new Color(0.12f, 0.14f, 0.18f, 0.92f);
            if (!affordable) body = new Color(0.16f, 0.10f, 0.10f, 0.92f);
            Fill(r, body);

            var edge = selected ? new Color(0.45f, 1f, 0.55f) : new Color(0.35f, 0.38f, 0.45f);
            float t = Mathf.Max(1f, Px(6f));
            Fill(new Rect(r.x, r.y, r.width, t), edge);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), edge);
            Fill(new Rect(r.x, r.y, t, r.height), edge);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), edge);

            if (card == null || card.Row == null) return;

            // Card internals are fractions of the card, so they follow it at any
            // scale instead of drifting apart the way fixed offsets did.
            GUI.Label(new Rect(r.x, r.y + r.height * 0.02f, r.width, r.height * 0.10f),
                      card.Row.Name, _cardName);

            var shape = card.Shape;
            if (shape != null)
            {
                // The art panel, then the footprint drawn cell by cell inside
                // it: the shape is the thing the player is committing to, so it
                // gets the largest block on the card.
                var box = new Rect(r.x + r.width * 0.08f, r.y + r.height * 0.13f,
                                   r.width * 0.84f, r.height * 0.50f);
                Fill(box, new Color(0.86f, 0.89f, 0.93f, 0.20f));

                float cell = Mathf.Min(box.width / shape.CellsWide, box.height / shape.CellsHigh);
                float gw = cell * shape.CellsWide, gh = cell * shape.CellsHigh;
                float ox = box.x + (box.width - gw) * 0.5f;
                float oy = box.y + (box.height - gh) * 0.5f;

                var tint = BrickUnit.BrickColor(card.Row);
                if (!affordable) tint *= 0.5f;
                tint.a = 1f;

                var face = BrickArt.Body(shape);
                if (face != null)
                    DrawShapeArt(new Rect(ox, oy, gw, gh), face, shape,
                                 affordable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f));
                else
                {
                    float gap = Mathf.Max(1f, cell * 0.06f);
                    foreach (var c in shape.Cells)
                    {
                        // Grid y counts up, GUI y counts down.
                        var cr = new Rect(ox + c.x * cell,
                                          oy + (shape.CellsHigh - 1 - c.y) * cell,
                                          cell - gap, cell - gap);
                        Fill(cr, tint);
                    }
                }
            }

            GUI.Label(new Rect(r.x, r.y + r.height * 0.65f, r.width, r.height * 0.11f),
                      $"{(SkillType)card.Row.SkillType}", _tiny);
            GUI.Label(new Rect(r.x, r.y + r.height * 0.78f, r.width, r.height * 0.18f),
                      $"{card.Cost} 金", _cardCost);
        }

        void DrawHint()
        {
            var r = new Rect(0f, HintTop, Screen.width, Px(RefScale.FontBodyPx * 1.4f));

            string msg;
            Color col;
            if (_dropper != null && _dropper.SinceRejected < 1.2f)
            {
                msg = _runtime.Ctx.Gold < _dropper.CurrentCost
                    ? "金币不足"
                    : "必须在白线上方松手才能放置！";
                col = new Color(1f, 0.5f, 0.5f);
            }
            else if (_dropper != null && _dropper.Dragging)
            {
                msg = _dropper.DragLegal ? "松手放置" : "拖到白线上方";
                col = _dropper.DragLegal ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.75f, 0.5f);
            }
            else
            {
                msg = "拖动一个守卫方块到白线上方来开始游戏！   1/2/3 选牌   右键/Q 旋转   R 换一批";
                col = new Color(0.88f, 0.92f, 0.98f);
            }

            var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = col;
            GUI.Label(r, msg, style);
        }
    }
}
