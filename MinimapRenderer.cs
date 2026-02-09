using UnityEngine;
using Unity.Mathematics;

namespace TaintedGrailMinimap
{
    public static class MinimapRenderer
    {
        private static Texture2D _cachedFogTex;

        /// <summary>
        /// Reimplemented from FogOfWar.GetMapBounds (private static, lines 462-492).
        /// Computes the map bounds rect from world-space Bounds and aspect ratio.
        /// Uses exact game math: scale 1.0500001f, aspect ratio adjustment on X axis.
        /// </summary>
        public static Rect GetMapBounds(Bounds bounds, float aspectRatio)
        {
            float2 size = new float2(bounds.size.x, bounds.size.z);
            float scale = 1.0500001f;
            float2 scaledSize = size * scale;
            scaledSize.x = scaledSize.y * aspectRatio;
            float2 center = new float2(bounds.center.x, bounds.center.z);
            float2 halfSize = scaledSize * 0.5f;
            float2 min = center - halfSize;
            float2 max = center + halfSize;
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// Reimplemented from FogOfWar.WorldPositionToNormalizedMapPosition (private static, lines 610-616).
        /// Converts a world position to normalized [0,1] coordinates on the map.
        /// </summary>
        public static float2 WorldToNormalizedMapPos(Vector3 worldPos, Rect mapBounds)
        {
            float u = math.unlerp(mapBounds.xMin, mapBounds.xMax, worldPos.x);
            float v = math.unlerp(mapBounds.yMin, mapBounds.yMax, worldPos.z);
            return new float2(u, v);
        }

        /// <summary>
        /// Composites the minimap texture by sampling the map at the player's UV with zoom and rotation.
        /// CPU-based approach using GetPixels/SetPixels since we can't use custom shaders.
        /// </summary>
        public static void CompositeMinimapTexture(
            Texture2D mapTexture,
            Texture2D circleMask,
            Texture2D output,
            float2 playerUV,
            float zoom,
            float rotationDegrees,
            RenderTexture fogMask = null)
        {
            int outSize = output.width;
            Color[] maskPixels = circleMask.GetPixels();
            Color[] outPixels = new Color[outSize * outSize];

            int mapW = mapTexture.width;
            int mapH = mapTexture.height;
            Color[] mapPixels = mapTexture.GetPixels();

            Color[] fogPixels = null;
            int fogW = 0, fogH = 0;
            if (fogMask != null)
            {
                fogW = fogMask.width;
                fogH = fogMask.height;
                if (_cachedFogTex == null || _cachedFogTex.width != fogW || _cachedFogTex.height != fogH)
                    _cachedFogTex = new Texture2D(fogW, fogH, TextureFormat.RGBA32, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = fogMask;
                _cachedFogTex.ReadPixels(new Rect(0, 0, fogW, fogH), 0, 0);
                _cachedFogTex.Apply();
                RenderTexture.active = prev;
                fogPixels = _cachedFogTex.GetPixels();
            }

            float halfSize = outSize * 0.5f;
            float invZoom = 1f / zoom;

            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cosR = Mathf.Cos(radians);
            float sinR = Mathf.Sin(radians);
            float viewScale = invZoom / outSize;

            for (int y = 0; y < outSize; y++)
            {
                for (int x = 0; x < outSize; x++)
                {
                    int idx = y * outSize + x;

                    if (maskPixels[idx].a < 0.01f)
                    {
                        outPixels[idx] = Color.clear;
                        continue;
                    }

                    float ox = (x - halfSize + 0.5f) * viewScale;
                    float oy = (y - halfSize + 0.5f) * viewScale;

                    float rx, ry;
                    if (rotationDegrees != 0f)
                    {
                        rx = ox * cosR - oy * sinR;
                        ry = ox * sinR + oy * cosR;
                    }
                    else
                    {
                        rx = ox;
                        ry = oy;
                    }

                    float srcU = playerUV.x + rx;
                    float srcV = playerUV.y + ry;

                    srcU = math.clamp(srcU, 0f, 1f);
                    srcV = math.clamp(srcV, 0f, 1f);

                    int mapX = math.clamp((int)(srcU * mapW), 0, mapW - 1);
                    int mapY = math.clamp((int)(srcV * mapH), 0, mapH - 1);
                    Color pixel = mapPixels[mapY * mapW + mapX];

                    if (fogPixels != null)
                    {
                        int fogX = math.clamp((int)(srcU * fogW), 0, fogW - 1);
                        int fogY = math.clamp((int)(srcV * fogH), 0, fogH - 1);
                        float fogAlpha = fogPixels[fogY * fogW + fogX].a;
                        pixel.r *= fogAlpha;
                        pixel.g *= fogAlpha;
                        pixel.b *= fogAlpha;
                    }

                    outPixels[idx] = pixel;
                }
            }

            output.SetPixels(outPixels);
            output.Apply();
        }
    }
}
