using UnityEngine;
using Unity.Mathematics;

namespace TaintedGrailMinimap
{
    public static class MinimapRenderer
    {
        /// <summary>
        /// Reimplemented from FogOfWar.GetMapBounds (private static).
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
        /// Reimplemented from FogOfWar.WorldPositionToNormalizedMapPosition (private static).
        /// Converts a world position to normalized [0,1] coordinates on the map.
        /// </summary>
        public static float2 WorldToNormalizedMapPos(Vector3 worldPos, Rect mapBounds)
        {
            float u = math.unlerp(mapBounds.xMin, mapBounds.xMax, worldPos.x);
            float v = math.unlerp(mapBounds.yMin, mapBounds.yMax, worldPos.z);
            return new float2(u, v);
        }

        /// <summary>
        /// Computes a UV rect for the minimap RawImage, centered on playerUV with zoom and aspect correction.
        /// </summary>
        public static Rect ComputeUVRect(float2 playerUV, float zoom, float mapAspect)
        {
            float invZoom = 1f / zoom;
            float uvW = invZoom;
            float uvH = invZoom;
            if (mapAspect > 1f)
            {
                uvW = invZoom * mapAspect;
            }
            else if (mapAspect < 1f)
            {
                uvH = invZoom / mapAspect;
            }
            float originU = playerUV.x - uvW * 0.5f;
            float originV = playerUV.y - uvH * 0.5f;
            return new Rect(originU, originV, uvW, uvH);
        }
    }
}
