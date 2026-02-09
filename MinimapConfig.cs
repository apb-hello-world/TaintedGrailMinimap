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
        BottomRight
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
        public static ConfigEntry<RotationMode> Rotation;
        public static ConfigEntry<KeyCode> ToggleKey;
        public static ConfigEntry<KeyCode> ZoomInKey;
        public static ConfigEntry<KeyCode> ZoomOutKey;

        public static void Init(ConfigFile config)
        {
            // Section: General
            Enabled = config.Bind("General", "Enabled", true, "Enable or disable the minimap");

            // Section: Position
            Position = config.Bind("Position", "Position", MinimapPosition.TopRight, "Corner of the screen for the minimap");
            OffsetX = config.Bind("Position", "OffsetX", 10, new ConfigDescription("Horizontal pixel offset from screen edge", new AcceptableValueRange<int>(-500, 500)));
            OffsetY = config.Bind("Position", "OffsetY", 10, new ConfigDescription("Vertical pixel offset from screen edge", new AcceptableValueRange<int>(-500, 500)));
            Size = config.Bind("Position", "Size", 200f, new ConfigDescription("Minimap diameter in pixels", new AcceptableValueRange<float>(100f, 400f)));

            // Section: Zoom
            ZoomLevel = config.Bind("Zoom", "ZoomLevel", 1f, new ConfigDescription("Minimap zoom level", new AcceptableValueRange<float>(0.5f, 3f)));

            // Section: Rotation
            Rotation = config.Bind("Rotation", "RotationMode", RotationMode.FixedNorth, "Map rotation behavior");

            // Section: Keybinds
            ToggleKey = config.Bind("Keybinds", "ToggleKey", KeyCode.M, "Key to toggle minimap visibility");
            ZoomInKey = config.Bind("Keybinds", "ZoomInKey", KeyCode.Equals, "Key to zoom in");
            ZoomOutKey = config.Bind("Keybinds", "ZoomOutKey", KeyCode.Minus, "Key to zoom out");
        }
    }
}
