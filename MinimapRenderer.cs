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

        /// <summary>
        /// Converts a world-space marker position to a screen-space offset on the minimap,
        /// applying zoom, rotation, scale, and edge clamping. Shared by quest markers and
        /// the custom compass marker.
        /// </summary>
        /// <param name="markerWorldPos">World position of the marker.</param>
        /// <param name="playerUV">Player's normalized UV position on the map.</param>
        /// <param name="mapBounds">Map bounds rect for coordinate conversion.</param>
        /// <param name="zoom">Current zoom level.</param>
        /// <param name="minimapSize">Minimap diameter in pixels.</param>
        /// <param name="rotAngle">Map rotation angle in degrees.</param>
        /// <param name="mapScale">Scale multiplier (1.0 for fixed, 1.45 for rotating).</param>
        /// <param name="edgeLimit">Max distance from center before clamping.</param>
        /// <param name="wasClamped">True if the marker was clamped to the edge.</param>
        /// <returns>Screen-space offset from minimap center.</returns>
        public static Vector2 WorldToMinimapOffset(
            Vector3 markerWorldPos, float2 playerUV, Rect mapBounds,
            float zoom, float minimapSize, float rotAngle, float mapScale,
            float edgeLimit, out bool wasClamped)
        {
            float2 markerUV = WorldToNormalizedMapPos(markerWorldPos, mapBounds);

            float scaledU = (markerUV.x - playerUV.x) * zoom * minimapSize;
            float scaledV = (markerUV.y - playerUV.y) * zoom * minimapSize;

            float rotU, rotV;
            if (rotAngle != 0f)
            {
                float radians = rotAngle * Mathf.Deg2Rad;
                float cosR = Mathf.Cos(radians);
                float sinR = Mathf.Sin(radians);
                rotU = scaledU * cosR - scaledV * sinR;
                rotV = scaledU * sinR + scaledV * cosR;
            }
            else
            {
                rotU = scaledU;
                rotV = scaledV;
            }

            rotU *= mapScale;
            rotV *= mapScale;

            float dist = Mathf.Sqrt(rotU * rotU + rotV * rotV);
            wasClamped = dist > edgeLimit;
            if (wasClamped && dist > 0.01f)
            {
                float clampFactor = edgeLimit / dist;
                rotU *= clampFactor;
                rotV *= clampFactor;
            }

            return new Vector2(rotU, rotV);
        }
    }
}
