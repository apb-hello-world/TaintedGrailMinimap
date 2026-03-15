using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TaintedGrailMinimap
{
    public enum MinimapPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        MiddleLeft,
        MiddleRight,
        TopMiddle,
        BottomMiddle
    }

    public enum RotationMode
    {
        FixedNorth,
        RotateWithPlayer
    }

    public static class MinimapConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<MinimapPosition> Position;
        public static ConfigEntry<int> OffsetX;
        public static ConfigEntry<int> OffsetY;
        public static ConfigEntry<float> Size;
        public static ConfigEntry<float> ZoomLevel;
        public static ConfigEntry<float> Opacity;
        public static ConfigEntry<float> BackgroundOpacity;
        public static ConfigEntry<bool> UseGameFont;
        public static ConfigEntry<RotationMode> Rotation;
        public static ConfigEntry<KeyCode> ToggleKey;
        public static ConfigEntry<KeyCode> ZoomInKey;
        public static ConfigEntry<KeyCode> ZoomOutKey;
        public static ConfigEntry<int> NudgeSmallStep;
        public static ConfigEntry<int> NudgeLargeStep;

        public static void Init(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Enable or disable the minimap");

            Position = config.Bind("Position", "Position", MinimapPosition.MiddleRight, "Screen position for the minimap");
            OffsetX = config.Bind("Position", "OffsetX", 20, new ConfigDescription("Horizontal pixel offset from screen edge", new AcceptableValueRange<int>(-500, 500)));
            OffsetY = config.Bind("Position", "OffsetY", 20, new ConfigDescription("Vertical pixel offset from screen edge", new AcceptableValueRange<int>(-500, 500)));
            Size = config.Bind("Position", "Size", 200f, new ConfigDescription("Minimap diameter in pixels", new AcceptableValueRange<float>(100f, 400f)));

            ZoomLevel = config.Bind("Zoom", "ZoomLevel", 8f, new ConfigDescription("Minimap zoom level", new AcceptableValueRange<float>(0.5f, 8f)));

            Opacity = config.Bind("Appearance", "Opacity", 1f, new ConfigDescription("Minimap opacity (0 = transparent, 1 = fully opaque)", new AcceptableValueRange<float>(0.1f, 1f)));
            BackgroundOpacity = config.Bind("Appearance", "BackgroundOpacity", 0.85f, new ConfigDescription("Dark background opacity behind the minimap (0 = transparent, 1 = solid)", new AcceptableValueRange<float>(0f, 1f)));
            UseGameFont = config.Bind("Appearance", "UseGameFont", true, "Use the in-game fantasy font for cardinal direction labels (N/S/E/W). When disabled, uses default font.");

            Rotation = config.Bind("Rotation", "RotationMode", RotationMode.FixedNorth, "Map rotation behavior");

            ToggleKey = config.Bind("Keybinds", "ToggleKey", KeyCode.N, "Key to toggle minimap visibility");
            ZoomInKey = config.Bind("Keybinds", "ZoomInKey", KeyCode.Equals, "Key to zoom in");
            ZoomOutKey = config.Bind("Keybinds", "ZoomOutKey", KeyCode.Minus, "Key to zoom out");

            NudgeSmallStep = config.Bind("Position", "NudgeSmallStep", 5, new ConfigDescription("Pixels to move with Ctrl+Arrow", new AcceptableValueRange<int>(1, 50)));
            NudgeLargeStep = config.Bind("Position", "NudgeLargeStep", 20, new ConfigDescription("Pixels to move with Ctrl+Shift+Arrow", new AcceptableValueRange<int>(5, 100)));
        }
    }
}
