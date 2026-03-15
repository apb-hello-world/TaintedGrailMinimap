using UnityEngine;

namespace TaintedGrailMinimap
{
    /// <summary>
    /// Procedurally generates all textures and sprites used by the minimap UI.
    /// All methods are static and results are cached where appropriate.
    /// </summary>
    public static class MinimapTextureFactory
    {
        private static Texture2D _arrowTexture;
        private static Texture2D _markerDotTexture;
        private static Texture2D _customMarkerRingTexture;

        // Arrow chevron parameters
        private const int ArrowTextureSize = 48;
        private const float ArrowLineThickness = 3.5f;
        private const float ArrowOutlineWidth = 1.5f;
        private const float ArrowLengthFactor = 0.4f;
        private const float ArrowWingSpreadX = 0.55f;
        private const float ArrowWingSpreadY = 0.15f;

        // Marker/ring texture parameters
        private const int MarkerDotTextureSize = 16;
        private const int MarkerRingTextureSize = 16;
        private const float MarkerRingThickness = 2.5f;
        private const float BorderRingThickness = 2f;

        /// <summary>
        /// Creates a filled circle texture and wraps it as a Sprite.
        /// Used for the circle mask on the minimap root.
        /// </summary>
        public static Sprite CreateCircleSprite(int size)
        {
            var tex = CreateCircleTexture(size, 0f, size / 2f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Creates a ring (hollow circle) texture and wraps it as a Sprite.
        /// Used for the minimap border.
        /// </summary>
        public static Sprite CreateBorderSprite(int size)
        {
            float radius = size / 2f;
            var tex = CreateCircleTexture(size, radius - BorderRingThickness, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Returns a cached chevron arrow texture for the player direction indicator.
        /// </summary>
        public static Texture2D GetArrowTexture()
        {
            if (_arrowTexture != null) return _arrowTexture;

            int s = ArrowTextureSize;
            _arrowTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[s * s];
            float cx = s / 2f;
            float cy = s / 2f;
            float arrowLen = s * ArrowLengthFactor;
            Color outline = new Color(0, 0, 0, 0.9f);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x - cx + 0.5f;
                    float py = y - cy + 0.5f;
                    float d1 = DistToSegment(px, py, 0f, arrowLen, -arrowLen * ArrowWingSpreadX, -arrowLen * ArrowWingSpreadY);
                    float d2 = DistToSegment(px, py, 0f, arrowLen, arrowLen * ArrowWingSpreadX, -arrowLen * ArrowWingSpreadY);
                    float d = Mathf.Min(d1, d2);

                    if (d <= ArrowLineThickness)
                        pixels[y * s + x] = Color.white;
                    else if (d <= ArrowLineThickness + ArrowOutlineWidth)
                        pixels[y * s + x] = outline;
                    else
                        pixels[y * s + x] = Color.clear;
                }
            }
            _arrowTexture.SetPixels(pixels);
            _arrowTexture.Apply();
            return _arrowTexture;
        }

        /// <summary>
        /// Returns a cached filled circle texture for quest marker dots.
        /// </summary>
        public static Texture2D GetMarkerDotTexture()
        {
            if (_markerDotTexture != null) return _markerDotTexture;
            _markerDotTexture = CreateCircleTexture(MarkerDotTextureSize, 0f, MarkerDotTextureSize / 2f);
            return _markerDotTexture;
        }

        /// <summary>
        /// Returns a cached ring texture for the custom compass marker.
        /// </summary>
        public static Texture2D GetCustomMarkerRingTexture()
        {
            if (_customMarkerRingTexture != null) return _customMarkerRingTexture;
            float r = MarkerRingTextureSize / 2f;
            _customMarkerRingTexture = CreateCircleTexture(MarkerRingTextureSize, r - MarkerRingThickness, r);
            return _customMarkerRingTexture;
        }

        /// <summary>
        /// Core circle/ring texture generator. Creates a white circle (or ring) on a transparent background.
        /// Use innerRadius=0 for a filled circle, or a positive value for a ring.
        /// </summary>
        private static Texture2D CreateCircleTexture(int size, float innerRadius, float outerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float outerSq = outerRadius * outerRadius;
            float innerSq = innerRadius * innerRadius;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distSq = dx * dx + dy * dy;
                    pixels[y * size + x] = (distSq <= outerSq && distSq >= innerSq) ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Computes the minimum distance from point (px,py) to line segment (ax,ay)-(bx,by).
        /// Used by the arrow chevron generator.
        /// </summary>
        private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq);
            float projX = ax + t * dx;
            float projY = ay + t * dy;
            float ex = px - projX;
            float ey = py - projY;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }
    }
}
