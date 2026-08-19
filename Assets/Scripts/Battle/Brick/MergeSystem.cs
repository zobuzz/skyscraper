using System.Collections.Generic;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Merges touching bricks that share a MergeSkin group and level.
    ///
    /// MergeSkin -- not hero id -- is the grouping key: 狂斧, 投石手, 毒液 and
    /// 忍者 all carry MergeSkin 3, so four different heroes combine with each
    /// other. That is why the field exists separately from ID.
    public class MergeSystem : MonoBehaviour
    {
        [Tooltip("Bricks must be at least this settled before merging, so a " +
                 "falling brick does not merge mid-air.")]
        public float MergeCheckInterval = 0.25f;

        [Tooltip("How far apart two footprints may be and still count as " +
                 "touching. Overlap is tested on the cell bounds, not on the " +
                 "centres: an L and an S can touch while their origins are " +
                 "two units apart.")]
        public float ContactSlack = 0.12f;

        readonly List<BrickUnit> _pending = new List<BrickUnit>();
        BattleRuntime _runtime;
        float _timer;

        void Awake() => _runtime = GetComponent<BattleRuntime>();

        public void Watch(BrickUnit unit)
        {
            if (unit != null && !_pending.Contains(unit)) _pending.Add(unit);
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = MergeCheckInterval;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var u = _pending[i];
                if (u == null || !u.IsAlive || u.Lost) { _pending.RemoveAt(i); continue; }
                if (!u.Settled) continue;

                if (TryMerge(u)) { _pending.RemoveAt(i); continue; }
                _pending.RemoveAt(i);   // settled with no partner: stop polling
            }
        }

        bool TryMerge(BrickUnit unit)
        {
            BrickUnit partner = null;
            float bestSqr = float.MaxValue;

            var mine = unit.Bounds;
            mine.Expand(ContactSlack);

            foreach (var other in _runtime.Bricks)
            {
                if (other == null || other == unit || !other.IsAlive || other.Lost) continue;
                if (other.MergeSkin != unit.MergeSkin) continue;
                if (other.Level != unit.Level) continue;
                if (!other.Settled) continue;
                if (!mine.Intersects(other.Bounds)) continue;

                float d = ((Vector2)other.transform.position - unit.Position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; partner = other; }
            }

            if (partner == null) return false;

            // Keep the survivor lower in the stack so the tower does not jump.
            var keep = partner.transform.position.y <= unit.transform.position.y ? partner : unit;
            var consume = keep == partner ? unit : partner;

            keep.MergeFrom(consume);
            _runtime.OnBrickDestroyed(consume);
            Destroy(consume.gameObject);

            Watch(keep);   // a merged brick may chain into another merge
            return true;
        }
    }
}
