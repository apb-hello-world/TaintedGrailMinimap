using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using Awaken.TG.Main.UI;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Cameras;
using Awaken.TG.Main.FastTravel;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Maps.Markers;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Assets;
using BepInEx.Logging;
using TMPro;

namespace TaintedGrailMinimap
{
    public class MinimapBehaviour : MonoBehaviour
    {
        // Logger (set by Plugin on creation)
        internal static ManualLogSource Log;

        // Layout constants
        private const float RotationMapScale = 1.45f;
        private const float MarkerEdgePadding = 6f;
        private const float OffsetLabelDuration = 2f;
        private const float CardinalLabelOffset = 10f;
        private const float CardinalFontSize = 14f;
        private const int OffsetFontSize = 12;

        // UI element sizes
        private const int ArrowDisplaySize = 20;
        private const int MarkerDotDisplaySize = 8;
        private const int CustomMarkerDisplaySize = 10;
        private static readonly Vector2 CardinalLabelSize = new Vector2(24, 20);
        private static readonly Vector2 OffsetLabelSize = new Vector2(120, 20);

        // Cardinal direction data
        private const int CardinalCount = 4;
        private static readonly string[] CardinalLetters = { "N", "E", "S", "W" };
        private static readonly float[] CardinalBaseAngles = { 90f, 0f, 270f, 180f };

        // Marker colors
        private static readonly Color MarkerOnScreenColor = new Color(1f, 0.85f, 0f, 1f);
        private static readonly Color MarkerClampedColor = new Color(1f, 0.5f, 0f, 0.9f);
        private static readonly Color CustomMarkerColor = new Color(0.3f, 0.5f, 1f, 1f);
        private static readonly Color NorthLabelColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color BorderColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        private static readonly Color BackgroundTint = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color ShadowColor = new Color(0, 0, 0, 0.8f);

        // Position lookup table: anchor, pivot, offsetX sign, offsetY sign
        private struct PositionLayout
        {
            public Vector2 Anchor;
            public Vector2 Pivot;
            public float SignX;
            public float SignY;
        }

        private static readonly Dictionary<MinimapPosition, PositionLayout> PositionLayouts =
            new Dictionary<MinimapPosition, PositionLayout>
        {
            { MinimapPosition.TopLeft,      new PositionLayout { Anchor = new Vector2(0, 1),    Pivot = new Vector2(0, 1),    SignX =  1, SignY = -1 } },
            { MinimapPosition.TopRight,     new PositionLayout { Anchor = new Vector2(1, 1),    Pivot = new Vector2(1, 1),    SignX = -1, SignY = -1 } },
            { MinimapPosition.BottomLeft,   new PositionLayout { Anchor = new Vector2(0, 0),    Pivot = new Vector2(0, 0),    SignX =  1, SignY =  1 } },
            { MinimapPosition.BottomRight,  new PositionLayout { Anchor = new Vector2(1, 0),    Pivot = new Vector2(1, 0),    SignX = -1, SignY =  1 } },
            { MinimapPosition.MiddleLeft,   new PositionLayout { Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0, 0.5f), SignX =  1, SignY =  1 } },
            { MinimapPosition.MiddleRight,  new PositionLayout { Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(1, 0.5f), SignX = -1, SignY =  1 } },
            { MinimapPosition.TopMiddle,    new PositionLayout { Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1), SignX =  1, SignY = -1 } },
            { MinimapPosition.BottomMiddle, new PositionLayout { Anchor = new Vector2(0.5f, 0), Pivot = new Vector2(0.5f, 0), SignX =  1, SignY =  1 } },
        };

        // UI hierarchy
        private GameObject _minimapRoot;
        private GameObject _minimapContainer;
        private RawImage _minimapImage;
        private RectTransform _mapImageTransform;
        private Image _borderImage;
        private RectTransform _rootTransform;
        private bool _initialized;
        private bool _isVisible = true;

        // Map state
        private Texture _mapTexture;
        private float _mapAspect = 1f;
        private Rect _mapBoundsRect;
        private bool _mapReady;
        private int _sceneGeneration;
        private SpriteReference _spriteRef;

        // Player arrow
        private RawImage _arrowImage;
        private RectTransform _arrowTransform;

        // Quest markers
        private const int MaxMarkers = 8;
        private RawImage[] _markerImages;
        private RectTransform[] _markerTransforms;
        private readonly List<QuestMarker> _questMarkerBuffer = new List<QuestMarker>();

        // Custom compass marker
        private RawImage _customMarkerImage;
        private RectTransform _customMarkerTransform;

        // Scene tracking
        private SceneReference _currentScene;
        private bool _hasCurrentScene;

        // Offset nudge label
        private Text _offsetLabel;
        private float _offsetLabelHideTime;

        // Cardinal direction labels
        private TextMeshProUGUI[] _cardinalLabels;
        private RectTransform[] _cardinalTransforms;

        // -------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------

        void Update()
        {
            HandleInput();

            if (_offsetLabel != null && Time.time > _offsetLabelHideTime)
                _offsetLabel.gameObject.SetActive(false);

            if (!MinimapConfig.Enabled.Value)
            {
                if (_minimapContainer != null) _minimapContainer.SetActive(false);
                return;
            }
            if (!_initialized)
            {
                TryInitialize();
                return;
            }

            var sceneService = World.Services.Get<SceneService>();
            if (sceneService != null && _hasCurrentScene && !sceneService.ActiveSceneRef.Equals(_currentScene))
            {
                CleanupForSceneChange();
                return;
            }

            if (_rootTransform != null)
            {
                float configSize = MinimapConfig.Size.Value;
                if (Mathf.Abs(_rootTransform.sizeDelta.x - configSize) > 0.1f)
                    _rootTransform.sizeDelta = new Vector2(configSize, configSize);
                ApplyPosition();
            }

            if (_minimapContainer != null)
                _minimapContainer.SetActive(_isVisible);
        }

        void LateUpdate()
        {
            if (!_initialized || !_mapReady || !_isVisible || _mapTexture == null)
                return;

            var hero = Hero.Current;
            if (hero == null) return;

            float2 playerUV = MinimapRenderer.WorldToNormalizedMapPos(hero.Coords, _mapBoundsRect);
            float zoom = MinimapConfig.ZoomLevel.Value;

            // Update map image UV viewport
            Rect uvRect = MinimapRenderer.ComputeUVRect(playerUV, zoom, _mapAspect);
            _minimapImage.texture = _mapTexture;
            _minimapImage.uvRect = uvRect;
            _minimapImage.color = new Color(1f, 1f, 1f, MinimapConfig.Opacity.Value);

            // Map rotation
            float rotAngle = MinimapConfig.Rotation.Value == RotationMode.RotateWithPlayer
                ? World.Any<GameCamera>()?.MainCamera?.transform.eulerAngles.y ?? 0f
                : 0f;

            _mapImageTransform.localRotation = Quaternion.Euler(0, 0, rotAngle);
            float mapScale = MinimapConfig.Rotation.Value == RotationMode.RotateWithPlayer ? RotationMapScale : 1f;
            _mapImageTransform.localScale = new Vector3(mapScale, mapScale, 1f);

            // Player arrow rotation
            if (_arrowTransform != null)
            {
                if (MinimapConfig.Rotation.Value == RotationMode.FixedNorth)
                {
                    float cameraY = World.Any<GameCamera>()?.MainCamera?.transform.eulerAngles.y ?? 0f;
                    _arrowTransform.localRotation = Quaternion.Euler(0, 0, -cameraY);
                }
                else
                {
                    _arrowTransform.localRotation = Quaternion.identity;
                }
            }

            UpdateQuestMarkers(playerUV, zoom, _mapAspect, rotAngle, mapScale);
            UpdateCustomMarker(playerUV, zoom, _mapAspect, rotAngle, mapScale);
            UpdateCardinalLabels(rotAngle);
        }

        void OnDestroy()
        {
            CleanupForSceneChange();
        }

        // -------------------------------------------------------------------
        // Input handling
        // -------------------------------------------------------------------

        private void HandleInput()
        {
            if (Input.GetKeyDown(MinimapConfig.ToggleKey.Value))
                _isVisible = !_isVisible;

            if (Input.GetKeyDown(MinimapConfig.ZoomInKey.Value))
                MinimapConfig.ZoomLevel.Value = Mathf.Clamp(MinimapConfig.ZoomLevel.Value + 0.5f, 0.5f, 8f);

            if (Input.GetKeyDown(MinimapConfig.ZoomOutKey.Value))
                MinimapConfig.ZoomLevel.Value = Mathf.Clamp(MinimapConfig.ZoomLevel.Value - 0.5f, 0.5f, 8f);

            HandleNudgeInput();
        }

        private void HandleNudgeInput()
        {
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                return;

            int step = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                ? MinimapConfig.NudgeLargeStep.Value
                : MinimapConfig.NudgeSmallStep.Value;

            MinimapPosition pos = MinimapConfig.Position.Value;
            bool rightSide = pos == MinimapPosition.TopRight || pos == MinimapPosition.BottomRight || pos == MinimapPosition.MiddleRight;
            bool topSide = pos == MinimapPosition.TopLeft || pos == MinimapPosition.TopRight || pos == MinimapPosition.TopMiddle;
            int xDir = rightSide ? -1 : 1;
            int yDir = topSide ? -1 : 1;

            bool nudged = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MinimapConfig.OffsetX.Value = Mathf.Clamp(MinimapConfig.OffsetX.Value - xDir * step, -500, 500);
                nudged = true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MinimapConfig.OffsetX.Value = Mathf.Clamp(MinimapConfig.OffsetX.Value + xDir * step, -500, 500);
                nudged = true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MinimapConfig.OffsetY.Value = Mathf.Clamp(MinimapConfig.OffsetY.Value - yDir * step, -500, 500);
                nudged = true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MinimapConfig.OffsetY.Value = Mathf.Clamp(MinimapConfig.OffsetY.Value + yDir * step, -500, 500);
                nudged = true;
            }

            if (nudged) ShowOffsetLabel();
        }

        // -------------------------------------------------------------------
        // Initialization and scene management
        // -------------------------------------------------------------------

        private void TryInitialize()
        {
            var canvasService = World.Services.Get<CanvasService>();
            if (canvasService == null || canvasService.HUDCanvas == null || Hero.Current == null)
                return;

            var commonRefs = CommonReferences.Get;
            if (commonRefs == null) return;

            var sceneService = World.Services.Get<SceneService>();
            if (sceneService == null) return;

            SceneReference activeScene = sceneService.ActiveSceneRef;
            MapSceneData mapSceneData = null;
            try
            {
                if (!commonRefs.MapData.byScene.TryGetValue(in activeScene, out mapSceneData))
                    return;
            }
            catch (NullReferenceException)
            {
                return;
            }

            CreateUI(canvasService.HUDCanvas);
            _initialized = true;
            _currentScene = activeScene;
            _hasCurrentScene = true;
            LoadMapSprite(mapSceneData);
        }

        private void LoadMapSprite(MapSceneData mapSceneData)
        {
            _sceneGeneration++;
            int expectedGeneration = _sceneGeneration;

            _spriteRef = mapSceneData.Sprite.Get();
            if (_spriteRef == null || !_spriteRef.IsSet)
            {
                _spriteRef = null;
                return;
            }

            _mapBoundsRect = MinimapRenderer.GetMapBounds(mapSceneData.Bounds, mapSceneData.AspectRatio);

            _spriteRef.arSpriteReference.LoadAsset<Sprite>().OnComplete(
                (ARAsyncOperationHandle<Sprite> handle) => OnMapSpriteLoaded(handle, expectedGeneration)
            );
        }

        private void OnMapSpriteLoaded(ARAsyncOperationHandle<Sprite> handle, int expectedGeneration)
        {
            if (expectedGeneration != _sceneGeneration) return;

            Sprite result = handle.Result;
            if (result == null)
            {
                ReleaseSprite();
                return;
            }

            _mapTexture = result.texture;
            _mapAspect = (result.texture.width > 0 && result.texture.height > 0)
                ? (float)result.texture.width / result.texture.height
                : 1f;
            _mapReady = true;

            Log?.LogInfo($"Map loaded — texture: {result.texture.width}x{result.texture.height}, " +
                         $"textureAR: {_mapAspect:F3}, boundsAR: {_mapBoundsRect.width / _mapBoundsRect.height:F3}");
        }

        private void ReleaseSprite()
        {
            if (_spriteRef != null)
            {
                _spriteRef.Release();
                _spriteRef = null;
            }
        }

        private void CleanupForSceneChange()
        {
            ReleaseSprite();
            _mapTexture = null;
            _mapReady = false;

            if (_minimapContainer != null)
            {
                Destroy(_minimapContainer);
                _minimapContainer = null;
                _minimapRoot = null;
            }

            _initialized = false;
            _hasCurrentScene = false;
        }

        // -------------------------------------------------------------------
        // Marker updates
        // -------------------------------------------------------------------

        private void UpdateQuestMarkers(float2 playerUV, float zoom, float mapAspect, float rotAngle, float mapScale)
        {
            float edgeLimit = MinimapConfig.Size.Value / 2f - MarkerEdgePadding;
            int markerIdx = 0;

            try
            {
                _questMarkerBuffer.Clear();
                World.All<QuestMarker>().FillList(_questMarkerBuffer);

                for (int i = 0; i < _questMarkerBuffer.Count && markerIdx < MaxMarkers; i++)
                {
                    bool clamped;
                    Vector2 offset = MinimapRenderer.WorldToMinimapOffset(
                        _questMarkerBuffer[i].Position, playerUV, _mapBoundsRect,
                        zoom, MinimapConfig.Size.Value, mapAspect, rotAngle, mapScale,
                        edgeLimit, out clamped);

                    _markerTransforms[markerIdx].anchoredPosition = offset;
                    _markerImages[markerIdx].gameObject.SetActive(true);
                    _markerImages[markerIdx].color = clamped ? MarkerClampedColor : MarkerOnScreenColor;
                    markerIdx++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Minimap] Quest marker update failed: " + e.Message);
            }

            for (int i = markerIdx; i < MaxMarkers; i++)
            {
                if (_markerImages[i] != null)
                    _markerImages[i].gameObject.SetActive(false);
            }
        }

        private void UpdateCustomMarker(float2 playerUV, float zoom, float mapAspect, float rotAngle, float mapScale)
        {
            if (_customMarkerImage == null) return;

            var compass = World.Any<Awaken.TG.Main.Maps.Compasses.Compass>();
            Location loc = compass != null ? compass.CustomMarkerLocation : null;
            if (loc == null)
            {
                _customMarkerImage.gameObject.SetActive(false);
                return;
            }

            float edgeLimit = MinimapConfig.Size.Value / 2f - MarkerEdgePadding;
            bool clamped;
            Vector2 offset = MinimapRenderer.WorldToMinimapOffset(
                loc.Coords, playerUV, _mapBoundsRect,
                zoom, MinimapConfig.Size.Value, mapAspect, rotAngle, mapScale,
                edgeLimit, out clamped);

            _customMarkerTransform.anchoredPosition = offset;
            _customMarkerImage.gameObject.SetActive(true);
        }

        private void UpdateCardinalLabels(float rotAngle)
        {
            if (_cardinalTransforms == null) return;
            float radius = MinimapConfig.Size.Value / 2f + CardinalLabelOffset;
            for (int i = 0; i < CardinalCount; i++)
            {
                float angle = (CardinalBaseAngles[i] + rotAngle) * Mathf.Deg2Rad;
                _cardinalTransforms[i].anchoredPosition = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius);
            }
        }

        // -------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------

        private void CreateUI(Canvas hudCanvas)
        {
            float size = MinimapConfig.Size.Value;

            CreateContainer(hudCanvas, size);
            CreateMaskedRoot(size);
            CreateMapImage();
            CreateBorder(size);
            CreateQuestMarkerElements();
            CreateCustomMarkerElement();
            CreatePlayerArrow();
            CreateCardinalLabels();
            CreateOffsetLabel();
        }

        private void CreateContainer(Canvas hudCanvas, float size)
        {
            _minimapContainer = new GameObject("MinimapContainer");
            _rootTransform = _minimapContainer.AddComponent<RectTransform>();
            _minimapContainer.transform.SetParent(hudCanvas.transform, false);
            ApplyPosition();
            _rootTransform.sizeDelta = new Vector2(size, size);
        }

        private void CreateMaskedRoot(float size)
        {
            _minimapRoot = new GameObject("MinimapMasked");
            var rt = _minimapRoot.AddComponent<RectTransform>();
            _minimapRoot.transform.SetParent(_minimapContainer.transform, false);
            SetFillParent(rt);

            var maskImage = _minimapRoot.AddComponent<Image>();
            maskImage.sprite = MinimapTextureFactory.CreateCircleSprite((int)size);
            maskImage.color = new Color(BackgroundTint.r, BackgroundTint.g, BackgroundTint.b, MinimapConfig.BackgroundOpacity.Value);
            _minimapRoot.AddComponent<Mask>().showMaskGraphic = true;
        }

        private void CreateMapImage()
        {
            var obj = new GameObject("MinimapImage");
            obj.transform.SetParent(_minimapRoot.transform, false);
            _minimapImage = obj.AddComponent<RawImage>();
            _mapImageTransform = obj.GetComponent<RectTransform>();
            SetFillParent(_mapImageTransform);
        }

        private void CreateBorder(float size)
        {
            var obj = new GameObject("MinimapBorder");
            obj.transform.SetParent(_minimapRoot.transform, false);
            _borderImage = obj.AddComponent<Image>();
            SetFillParent(obj.GetComponent<RectTransform>());
            _borderImage.sprite = MinimapTextureFactory.CreateBorderSprite((int)size);
            _borderImage.color = BorderColor;
        }

        private void CreateQuestMarkerElements()
        {
            _markerImages = new RawImage[MaxMarkers];
            _markerTransforms = new RectTransform[MaxMarkers];
            Texture2D dotTex = MinimapTextureFactory.GetMarkerDotTexture();

            for (int i = 0; i < MaxMarkers; i++)
            {
                var obj = new GameObject("QuestMarker_" + i);
                obj.transform.SetParent(_minimapContainer.transform, false);
                _markerImages[i] = obj.AddComponent<RawImage>();
                _markerTransforms[i] = obj.GetComponent<RectTransform>();
                SetCentered(_markerTransforms[i], MarkerDotDisplaySize);
                _markerImages[i].texture = dotTex;
                _markerImages[i].color = MarkerOnScreenColor;
                obj.SetActive(false);
            }
        }

        private void CreateCustomMarkerElement()
        {
            var obj = new GameObject("CustomMarker");
            obj.transform.SetParent(_minimapContainer.transform, false);
            _customMarkerImage = obj.AddComponent<RawImage>();
            _customMarkerTransform = obj.GetComponent<RectTransform>();
            SetCentered(_customMarkerTransform, CustomMarkerDisplaySize);
            _customMarkerImage.texture = MinimapTextureFactory.GetCustomMarkerRingTexture();
            _customMarkerImage.color = CustomMarkerColor;
            obj.SetActive(false);
        }

        private void CreatePlayerArrow()
        {
            var obj = new GameObject("PlayerArrow");
            obj.transform.SetParent(_minimapRoot.transform, false);
            _arrowImage = obj.AddComponent<RawImage>();
            _arrowTransform = obj.GetComponent<RectTransform>();
            SetCentered(_arrowTransform, ArrowDisplaySize);
            _arrowTransform.anchoredPosition = Vector2.zero;
            _arrowImage.texture = MinimapTextureFactory.GetArrowTexture();
            _arrowImage.color = Color.white;
        }

        private void CreateCardinalLabels()
        {
            _cardinalLabels = new TextMeshProUGUI[CardinalCount];
            _cardinalTransforms = new RectTransform[CardinalCount];
            var gameFont = MinimapConfig.UseGameFont.Value ? TMP_Settings.defaultFontAsset : null;

            for (int i = 0; i < CardinalCount; i++)
            {
                var obj = new GameObject("Cardinal_" + CardinalLetters[i]);
                obj.transform.SetParent(_minimapContainer.transform, false);

                var label = obj.AddComponent<TextMeshProUGUI>();
                label.text = CardinalLetters[i];
                label.fontSize = CardinalFontSize;
                label.fontStyle = TMPro.FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = i == 0 ? NorthLabelColor : Color.white;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                if (gameFont != null)
                    label.font = gameFont;
                _cardinalLabels[i] = label;

                var shadow = obj.AddComponent<Shadow>();
                shadow.effectColor = ShadowColor;
                shadow.effectDistance = new Vector2(1, -1);

                var rt = obj.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = CardinalLabelSize;
                _cardinalTransforms[i] = rt;
            }
        }

        private void CreateOffsetLabel()
        {
            var obj = new GameObject("OffsetLabel");
            obj.transform.SetParent(_minimapContainer.transform, false);
            _offsetLabel = obj.AddComponent<Text>();
            _offsetLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _offsetLabel.fontSize = OffsetFontSize;
            _offsetLabel.alignment = TextAnchor.MiddleCenter;
            _offsetLabel.color = Color.white;

            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -4);
            rt.sizeDelta = OffsetLabelSize;
            obj.SetActive(false);
        }

        private void ShowOffsetLabel()
        {
            if (_offsetLabel == null) return;
            _offsetLabel.text = "X:" + MinimapConfig.OffsetX.Value + "  Y:" + MinimapConfig.OffsetY.Value;
            _offsetLabel.gameObject.SetActive(true);
            _offsetLabelHideTime = Time.time + OffsetLabelDuration;
        }

        // -------------------------------------------------------------------
        // Layout
        // -------------------------------------------------------------------

        private void ApplyPosition()
        {
            if (!PositionLayouts.TryGetValue(MinimapConfig.Position.Value, out var layout))
                return;

            float offsetX = MinimapConfig.OffsetX.Value;
            float offsetY = MinimapConfig.OffsetY.Value;
            _rootTransform.anchorMin = _rootTransform.anchorMax = layout.Anchor;
            _rootTransform.pivot = layout.Pivot;
            _rootTransform.anchoredPosition = new Vector2(layout.SignX * offsetX, layout.SignY * offsetY);
        }

        // -------------------------------------------------------------------
        // RectTransform helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Configures a RectTransform to fill its parent (anchors 0-1, zero size delta).
        /// </summary>
        private static void SetFillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Configures a RectTransform to be centered in its parent with a square size.
        /// </summary>
        private static void SetCentered(RectTransform rt, int size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
        }
    }
}
