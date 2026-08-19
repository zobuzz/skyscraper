using UnityEngine;

namespace Skyscraper
{
    /// Assigns a procedurally generated sprite at runtime.
    ///
    /// A scene cannot store a reference to a Texture2D built in memory -- the
    /// reference is null on reload. Anything the scene builder places has to
    /// resolve its own sprite in Awake instead.
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class RuntimeSprite : MonoBehaviour
    {
        public enum Kind { Box, Circle, Diamond }

        public Kind Shape = Kind.Box;
        public Color Tint = Color.white;
        public int SortingOrder = 0;
        [Tooltip("World-space size in units; the sprite is scaled to match.")]
        public Vector2 Size = Vector2.one;

        void Awake() => Apply();
        void OnEnable() => Apply();
#if UNITY_EDITOR
        void OnValidate() => Apply();
#endif

        public void Apply()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;

            switch (Shape)
            {
                case Kind.Circle:  sr.sprite = Shapes.Circle;  break;
                case Kind.Diamond: sr.sprite = Shapes.Diamond; break;
                default:           sr.sprite = Shapes.Box;     break;
            }
            sr.color = Tint;
            sr.sortingOrder = SortingOrder;

            // Sprites are generated at 64px with PPU 100, so one sprite unit is
            // 0.64 world units before scaling.
            const float spriteUnits = 64f / Shapes.PPU;
            transform.localScale = new Vector3(Size.x / spriteUnits, Size.y / spriteUnits, 1f);
        }
    }
}
