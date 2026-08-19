using UnityEngine;

namespace Skyscraper
{
    /// Procedural placeholder sprites.
    ///
    /// The original game's textures and Spine rigs are copyrighted, so nothing
    /// is imported from it -- the replica draws its own primitives at runtime.
    /// Everything here is generated once and cached.
    public static class Shapes
    {
        static Sprite _box, _circle, _diamond;

        public const float PPU = 100f;

        public static Sprite Box => _box != null ? _box : (_box = MakeBox(64, 64, 6));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(64));
        public static Sprite Diamond => _diamond != null ? _diamond : (_diamond = MakeDiamond(64));

        static Sprite Finish(Texture2D tex)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), PPU);
        }

        /// Rounded box with a lighter inner face, so stacked bricks read as
        /// separate blocks without any art.
        static Sprite MakeBox(int w, int h, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = InRounded(x, y, w, h, radius);
                bool edge = inside && !InRounded(x, y, w, h, radius, 3);
                tex.SetPixel(x, y, !inside ? Color.clear
                                  : edge ? new Color(1f, 1f, 1f, 1f)
                                         : new Color(1f, 1f, 1f, 0.82f));
            }
            return Finish(tex);
        }

        static bool InRounded(int x, int y, int w, int h, int r, int inset = 0)
        {
            float minX = inset, minY = inset, maxX = w - 1 - inset, maxY = h - 1 - inset;
            if (x < minX || x > maxX || y < minY || y > maxY) return false;

            float cx = Mathf.Clamp(x, minX + r, maxX - r);
            float cy = Mathf.Clamp(y, minY + r, maxY - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= (float)r * r || (x >= minX + r && x <= maxX - r) || (y >= minY + r && y <= maxY - r);
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f, r = c;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d);                       // 1px feather
                float inner = d < r - 3 ? 0.82f : 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * inner));
            }
            return Finish(tex);
        }

        static Sprite MakeDiamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                float a = Mathf.Clamp01(c - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return Finish(tex);
        }
    }
}
