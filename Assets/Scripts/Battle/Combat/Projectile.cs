using UnityEngine;

namespace Skyscraper.Battle
{
    /// SkillType values observed across the 16 heroes. 0 is the wall (never
    /// fires); the rest are firing patterns whose parameters come from
    /// BrickHero (Interval / BurstTime / DmgRadius / DmgCount).
    public enum SkillType
    {
        Wall = 0,
        Splash = 1,       // 爆爆法师 / 狂斧 / 拳击手 / 行者 -- radius damage
        Lob = 2,          // 熔岩 / 投石手 -- arcing shot, lava leaves a burn
        Repair = 3,       // 修理工 -- heals the lowest brick
        Miner = 4,        // 矿工 -- generates gold instead of damage
        Ninja = 5,        // 忍者 -- rapid multi-strike
        Poison = 6,       // 毒液 -- damage over time
        Freeze = 7,       // 冻冻投手 -- slow
        Laser = 8,        // 镭射眼 -- instant beam
        Sniper = 9,       // 神射手 -- single high-damage bolt
        Thunder = 10,     // 雷神 -- large radius strike
    }

    /// Travels to its target, then hands off to BattleRuntime for the effect.
    /// Homing rather than ballistic: enemies move, and a miss on a slow lob
    /// would make the low-Interval heroes feel broken.
    public class Projectile : MonoBehaviour
    {
        EnemyUnit _target;
        Vector2 _lastKnown;
        DamageRoll _roll;
        float _radius;
        int _extraHits;
        SkillType _skill;
        float _speed;
        BattleRuntime _runtime;
        bool _spent;

        public void Launch(BattleRuntime runtime, EnemyUnit target, DamageRoll roll,
                           float radius, int extraHits, SkillType skill)
        {
            _runtime = runtime;
            _target = target;
            _roll = roll;
            _radius = radius;
            _extraHits = extraHits;
            _skill = skill;
            _lastKnown = target != null ? target.Position : (Vector2)transform.position;

            // Lobs travel visibly slower; laser and sniper read as instant.
            switch (skill)
            {
                case SkillType.Laser:
                case SkillType.Sniper:  _speed = 40f; break;
                case SkillType.Lob:     _speed = 7f;  break;
                case SkillType.Thunder: _speed = 25f; break;
                default:                _speed = 12f; break;
            }
        }

        void Update()
        {
            if (_spent) return;

            if (_target != null && _target.IsAlive) _lastKnown = _target.Position;

            var pos = (Vector2)transform.position;
            var delta = _lastKnown - pos;
            float step = _speed * Time.deltaTime;

            if (delta.sqrMagnitude <= step * step)
            {
                Impact();
                return;
            }

            var dir = delta.normalized;
            transform.position = pos + dir * step;
            transform.right = dir;
        }

        void Impact()
        {
            _spent = true;
            _runtime.ApplyImpact(_lastKnown, _target, _roll, _radius, _extraHits, _skill);
            Destroy(gameObject);
        }
    }
}
