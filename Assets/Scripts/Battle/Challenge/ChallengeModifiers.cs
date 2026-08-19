using System.Collections;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// The 7 ChallengeAttr rows, driven by the matching Global.Chall* fields.
    /// Each one attacks the tower as a structure rather than adding raw stats,
    /// which is what makes the physics stacking the actual difficulty knob.
    public class ChallengeModifiers : MonoBehaviour
    {
        public const int AntiAir = 1;   // 禁空领域: units above ChallAntiAirHeight die
        public const int Careful = 2;   // 谨慎决策: reroll price climbs
        public const int Swamp   = 3;
        public const int Giant   = 4;
        public const int Gravity = 5;
        public const int Wind    = 6;
        public const int Gaze    = 7;

        BattleRuntime _runtime;
        BattleContext _ctx;
        float _antiAirHeight;
        Coroutine _wind;

        void Awake() => _runtime = GetComponent<BattleRuntime>();

        public void Apply(BattleContext ctx)
        {
            _ctx = ctx;
            var g = ConfigDB.Global;
            if (g == null) return;

            // Reset anything a previous run left behind.
            Physics2D.gravity = new Vector2(0f, -9.81f);
            _antiAirHeight = 0f;
            if (_wind != null) { StopCoroutine(_wind); _wind = null; }
            _runtime.SetChallengeHpMultiplier(1f);

            foreach (var id in ctx.ActiveChallenges)
            {
                switch (id)
                {
                    case AntiAir:
                        _antiAirHeight = g.ChallAntiAirHeight > 0 ? g.ChallAntiAirHeight : 15f;
                        break;

                    case Giant:
                        // Fewer but far bigger enemies.
                        _runtime.SetChallengeHpMultiplier(Mathf.Max(1f, g.ChallGiantScale) * 2f);
                        break;

                    case Gravity:
                        Physics2D.gravity = new Vector2(0f, -9.81f * Mathf.Max(1f, g.ChallGravityScale));
                        break;

                    case Wind:
                        _wind = StartCoroutine(WindGusts(g.WindForce, Mathf.Max(1f, g.ChallWindCD)));
                        break;
                }
            }
        }

        void Update()
        {
            if (_antiAirHeight <= 0f || _ctx == null || _ctx.Phase != BattlePhase.Running) return;

            // Height cap: stacking past the ceiling destroys the top bricks, so
            // the tower cannot simply be built out of reach.
            var bricks = _runtime.Bricks;
            for (int i = bricks.Count - 1; i >= 0; i--)
            {
                var b = bricks[i];
                if (b == null || !b.IsAlive) continue;
                if (b.transform.position.y > _runtime.GroundY + _antiAirHeight)
                    b.Demolish();
            }
        }

        IEnumerator WindGusts(float force, float cooldown)
        {
            var wait = new WaitForSeconds(cooldown);
            while (true)
            {
                yield return wait;
                if (_ctx == null || _ctx.Phase != BattlePhase.Running) continue;

                float dir = Random.value < 0.5f ? -1f : 1f;
                foreach (var b in _runtime.Bricks)
                {
                    if (b == null || !b.IsAlive || b.Body == null) continue;
                    // Taller bricks catch more wind, so top-heavy towers topple.
                    float lever = Mathf.Max(0.2f, b.transform.position.y - _runtime.GroundY);
                    b.Body.AddForce(new Vector2(dir * force * lever * 0.02f, 0f), ForceMode2D.Impulse);
                }
            }
        }
    }
}
