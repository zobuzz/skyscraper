using UnityEngine;

namespace Skyscraper.Battle
{
    /// The clips the originals actually ship, by name.
    ///
    /// Taken from the rigs rather than invented: every hero has `idle`,
    /// `attack_1` and `appear` (城墙 has only appear + idle, which is right --
    /// a wall does not swing); every monster has `idle`, `move`, `attack` and
    /// `death`, with `hit` on the first three and `appear` on the two that make
    /// an entrance.
    public enum FigureClip { Appear, Idle, Move, Attack, Hit, Death }

    /// Plays a baked figure.
    ///
    /// Stage 1 has one frame per clip -- the setup pose -- so the motion here is
    /// procedural: a bob, a lunge, a topple. That is not a stand-in for the
    /// Spine timelines so much as the part of them that survives baking: what
    /// reads at this size is the whole body's rhythm, not which elbow bent.
    ///
    /// It is written as a frame player regardless, indexing an array at a fps.
    /// Baking the timelines out is then data-only: drop numbered frames into
    /// Resources/Figures/<rig>/<clip>/ and SetClip picks them up, with the
    /// procedural offsets going quiet on their own once a clip has real frames
    /// (see Motion below).
    ///
    /// Colour is owned here, not by the callers. Two things tint a figure -- the
    /// level lift on a brick, the HP darkening on an enemy -- and two things
    /// fade one -- the dissolve, the death clip. Letting both sides write
    /// sr.color meant whichever ran last won, and it flickered; Tint and Alpha
    /// are the seams instead.
    [DisallowMultipleComponent]
    public class FigureAnimator : MonoBehaviour
    {
        struct Clip
        {
            public Sprite[] Frames;
            public float Fps;
            public bool Loop;
            public float Length;        // seconds; 0 for a loop with no end
        }

        /// Brightness/quality tint. Multiplies the sprite's own colour.
        public Color Tint = Color.white;

        /// Dissolve, driven by whatever is removing the object.
        public float Alpha = 1f;

        SpriteRenderer _sr;
        float _height;                  // figure height in local units, for the bob
        Vector3 _home;                  // anchor's authored local position
        Clip[] _clips;

        FigureClip _clip = FigureClip.Idle;
        float _t;
        Vector2 _dir = Vector2.right;   // where the current action points
        bool _done;

        public FigureClip Current => _clip;
        public bool Finished => _done;

        public void Bind(SpriteRenderer sr, float height, float alpha)
        {
            _sr = sr;
            _height = Mathf.Max(0.01f, height);
            _home = transform.localPosition;
            Alpha = alpha;
            _clips = new Clip[6];
            var one = sr != null ? sr.sprite : null;
            for (int i = 0; i < _clips.Length; i++)
                _clips[i] = new Clip { Frames = one != null ? new[] { one } : null,
                                       Fps = 12f, Loop = i == (int)FigureClip.Idle
                                                       || i == (int)FigureClip.Move };
            Play(FigureClip.Idle);
        }

        /// Stage 2's entry point: real frames for one clip.
        public void SetClip(FigureClip clip, Sprite[] frames, float fps, bool loop)
        {
            if (_clips == null || frames == null || frames.Length == 0) return;
            _clips[(int)clip] = new Clip
            {
                Frames = frames, Fps = Mathf.Max(1f, fps), Loop = loop,
                Length = frames.Length / Mathf.Max(1f, fps),
            };
        }

        /// Load every clip this rig has, by the original's own names.
        public void LoadClips(string figurePath, bool enemy)
        {
            if (string.IsNullOrEmpty(figurePath)) return;
            Take(figurePath, FigureClip.Appear, "appear", 20f, false);
            Take(figurePath, FigureClip.Idle,   "idle",   20f, true);
            Take(figurePath, FigureClip.Death,  "death",  20f, false);
            Take(figurePath, FigureClip.Hit,    "hit",    20f, false);
            if (enemy)
            {
                Take(figurePath, FigureClip.Move,   "move",   20f, true);
                Take(figurePath, FigureClip.Attack, "attack", 20f, false);
            }
            else
            {
                Take(figurePath, FigureClip.Attack, "attack_1", 20f, false);
                Take(figurePath, FigureClip.Attack, "attack",   20f, false);
            }
        }

        void Take(string path, FigureClip clip, string name, float fps, bool loop)
        {
            var seq = Figures.LoadFrames(path, name);
            if (seq != null) SetClip(clip, seq, fps, loop);
        }

        /// `dir` points the action: +x for a hero shooting right, -x for an
        /// enemy walking left. Ignored by the clips that have no direction.
        public void Play(FigureClip clip, Vector2 dir = default)
        {
            if (_clips == null) return;
            if (dir.sqrMagnitude > 1e-6f) _dir = dir.normalized;
            // Re-triggering a loop mid-cycle would make a walking enemy stutter
            // once per frame that asks for Move again.
            if (clip == _clip && _clips[(int)clip].Loop) return;
            _clip = clip;
            _t = 0f;
            _done = false;
        }

        void Update()
        {
            if (_sr == null || _clips == null) return;
            _t += Time.deltaTime;
            var c = _clips[(int)_clip];

            if (c.Frames != null && c.Frames.Length > 0)
            {
                int n = c.Frames.Length;
                int i = (int)(_t * c.Fps);
                i = c.Loop ? ((i % n) + n) % n : Mathf.Min(n - 1, i);
                _sr.sprite = c.Frames[i];
            }

            // With real frames the animation is in the frames, so the
            // procedural offsets stand down; with one frame they are the whole
            // animation. Same code path either way.
            Motion(c.Frames == null || c.Frames.Length <= 1);

            if (!c.Loop)
            {
                float dur = c.Length > 0f ? c.Length : Duration(_clip);
                if (_t >= dur)
                {
                    _done = true;
                    // Death holds on its last frame -- whatever spawned the
                    // figure is about to remove it.
                    if (_clip != FigureClip.Death) Play(Rest);
                }
            }
        }

        /// What a one-shot clip falls back to. Move is the resting state while
        /// the caller keeps asking for it; Idle otherwise.
        FigureClip Rest = FigureClip.Idle;

        /// Callers that walk set this once instead of re-Playing every frame.
        public void SetRest(FigureClip clip) { Rest = clip; }

        static float Duration(FigureClip c)
        {
            switch (c)
            {
                case FigureClip.Appear: return 0.30f;
                case FigureClip.Attack: return 0.28f;
                case FigureClip.Hit:    return 0.18f;
                case FigureClip.Death:  return 0.45f;
                default:                return 0f;
            }
        }

        void Motion(bool on)
        {
            var pos = _home;
            float rot = 0f;
            var scale = Vector3.one;
            float a = 1f;
            var flash = Color.white;

            if (on)
            {
                switch (_clip)
                {
                    case FigureClip.Appear:
                    {
                        // Punch in: overshoot then settle. The original's appear
                        // is a drop-and-squash, and the overshoot is what makes
                        // a single frame read as landing rather than fading in.
                        float k = Mathf.Clamp01(_t / 0.30f);
                        float s = k < 0.55f
                            ? Mathf.Lerp(0.40f, 1.15f, k / 0.55f)
                            : Mathf.Lerp(1.15f, 1f, (k - 0.55f) / 0.45f);
                        scale = new Vector3(s, s, 1f);
                        a = Mathf.Clamp01(k * 2.5f);
                        break;
                    }
                    case FigureClip.Idle:
                    {
                        float p = Mathf.Sin(_t * (Mathf.PI * 2f / 1.6f));
                        pos.y += 0.02f * _height * p;
                        scale = new Vector3(1f - 0.02f * p, 1f + 0.02f * p, 1f);
                        break;
                    }
                    case FigureClip.Move:
                    {
                        float p = Mathf.Sin(_t * (Mathf.PI * 2f / 0.5f));
                        pos.y += 0.035f * _height * Mathf.Abs(p);
                        rot = 3f * p * (_dir.x < 0f ? -1f : 1f);
                        break;
                    }
                    case FigureClip.Attack:
                    {
                        // Lunge and recover. 0.12 local units is about an eighth
                        // of a cell -- enough to see, small enough that a brick
                        // in a stack does not appear to leave its cell.
                        float k = Mathf.Clamp01(_t / 0.28f);
                        float push = Mathf.Sin(k * Mathf.PI) * 0.12f;
                        pos += (Vector3)(_dir * push);
                        float s = 1f + 0.10f * Mathf.Sin(k * Mathf.PI);
                        scale = new Vector3(s, 2f - s, 1f);
                        break;
                    }
                    case FigureClip.Hit:
                    {
                        float k = Mathf.Clamp01(_t / 0.18f);
                        float p = Mathf.Sin(k * Mathf.PI);
                        pos -= (Vector3)(_dir * (0.06f * p));
                        flash = Color.Lerp(Color.white, new Color(3f, 3f, 3f), p);
                        break;
                    }
                    case FigureClip.Death:
                    {
                        float k = Mathf.Clamp01(_t / 0.45f);
                        rot = -80f * k * (_dir.x < 0f ? -1f : 1f);
                        float s = 1f - 0.30f * k;
                        scale = new Vector3(s, s, 1f);
                        a = 1f - k;
                        break;
                    }
                }
            }

            transform.localPosition = pos;
            transform.localRotation = rot == 0f ? Quaternion.identity
                                                : Quaternion.Euler(0f, 0f, rot);
            transform.localScale = scale;

            var col = Tint * flash;
            col.a = Alpha * a;
            _sr.color = col;
        }
    }
}
