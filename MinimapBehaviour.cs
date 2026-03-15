using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.Text;
using Unity.Mathematics;
using Awaken.TG.Main.UI;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Cameras;
using Awaken.TG.Main.FastTravel;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Maps.Compasses;
using Awaken.TG.Main.Maps.Markers;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Assets;
using TMPro;

namespace TaintedGrailMinimap
{
    public class MinimapBehaviour : MonoBehaviour
    {
        private GameObject _minimapRoot;
        private GameObject _minimapContainer;
        private RawImage _minimapImage;
        private RectTransform _mapImageTransform;
        private Image _borderImage;
        private RectTransform _rootTransform;
        private bool _initialized;
        private bool _isVisible = true;

        private Texture _mapTexture;
        private float _mapAspect = 1f;
        private Rect _mapBoundsRect;
        private bool _mapReady;
        private int _sceneGeneration;
        private SpriteReference _spriteRef;

        private RawImage _arrowImage;
        private RectTransform _arrowTransform;
        private static Texture2D _arrowTexture;

        private const int MaxMarkers = 8;
        private RawImage[] _markerImages;
        private RectTransform[] _markerTransforms;
        private static Texture2D _markerDotTexture;
        private static Texture2D _customMarkerRingTexture;
        private readonly List<QuestMarker> _questMarkerBuffer = new List<QuestMarker>();

        private RawImage _customMarkerImage;
        private RectTransform _customMarkerTransform;

        private SceneReference _currentScene;
        private bool _hasCurrentScene;

        private Text _offsetLabel;
        private float _offsetLabelHideTime;

        private const int CardinalCount = 4;
        private static readonly string[] CardinalLetters = { "N", "E", "S", "W" };
        private static readonly float[] CardinalBaseAngles = { 90f, 0f, 270f, 180f };
        private TextMeshProUGUI[] _cardinalLabels;
        private RectTransform[] _cardinalTransforms;

        void Update()
        {
            if (Input.GetKeyDown(MinimapConfig.ToggleKey.Value))
                _isVisible = !_isVisible;

            if (Input.GetKeyDown(MinimapConfig.ZoomInKey.Value))
            {
                float newZoom = Mathf.Clamp(MinimapConfig.ZoomLevel.Value + 0.5f, 0.5f, 8f);
                MinimapConfig.ZoomLevel.Value = newZoom;
            }
            if (Input.GetKeyDown(MinimapConfig.ZoomOutKey.Value))
            {
                float newZoom = Mathf.Clamp(MinimapConfig.ZoomLevel.Value - 0.5f, 0.5f, 8f);
                MinimapConfig.ZoomLevel.Value = newZoom;
            }

            // Ctrl+Arrow nudge for repositioning
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                int step = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    ? MinimapConfig.NudgeLargeStep.Value
                    : MinimapConfig.NudgeSmallStep.Value;
                bool nudged = false;
                MinimapPosition pos = MinimapConfig.Position.Value;
                bool rightSide = pos == MinimapPosition.TopRight || pos == MinimapPosition.BottomRight || pos == MinimapPosition.MiddleRight;
                bool topSide = pos == MinimapPosition.TopLeft || pos == MinimapPosition.TopRight || pos == MinimapPosition.TopMiddle;
                int xDir = rightSide ? -1 : 1;
                int yDir = topSide ? -1 : 1;

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
                {
                    _rootTransform.sizeDelta = new Vector2(configSize, configSize);
                }
                ApplyPosition();
            }

            if (_minimapContainer != null)
                _minimapContainer.SetActive(_isVisible);
        }

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
            catch (System.NullReferenceException)
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
            _mapAspect = 1f; // Force 1:1 aspect ratio to fix stretching in non-square maps
            _mapReady = true;
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

        void LateUpdate()
        {
            if (!_initialized || !_mapReady || !_isVisible || _mapTexture == null)
                return;

            var hero = Hero.Current;
            if (hero == null) return;

            float2 playerUV = MinimapRenderer.WorldToNormalizedMapPos(hero.Coords, _mapBoundsRect);
            float zoom = MinimapConfig.ZoomLevel.Value;

            Rect uvRect = MinimapRenderer.ComputeUVRect(playerUV, zoom, _mapAspect);
            _minimapImage.texture = _mapTexture;
            _minimapImage.uvRect = uvRect;
            _minimapImage.color = new Color(1f, 1f, 1f, MinimapConfig.Opacity.Value);

            float rotAngle;
            if (MinimapConfig.Rotation.Value == RotationMode.RotateWithPlayer)
                rotAngle = World.Any<GameCamera>()?.MainCamera?.transform.eulerAngles.y ?? 0f;
            else
                rotAngle = 0f;

            _mapImageTransform.localRotation = Quaternion.Euler(0, 0, rotAngle);
            float mapScale = MinimapConfig.Rotation.Value == RotationMode.RotateWithPlayer ? 1.45f : 1f;
            _mapImageTransform.localScale = new Vector3(mapScale, mapScale, 1f);

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

            UpdateQuestMarkers(playerUV, zoom, rotAngle, mapScale);
            UpdateCustomMarker(playerUV, zoom, rotAngle, mapScale);
            UpdateCardinalLabels(rotAngle);
        }

        private void UpdateQuestMarkers(float2 playerUV, float zoom, float rotAngle, float mapScale)
        {
            int markerIdx = 0;
            float edgeLimit = MinimapConfig.Size.Value / 2f - 6f;
            float radians = rotAngle * Mathf.Deg2Rad;
            float cosR = Mathf.Cos(radians);
            float sinR = Mathf.Sin(radians);

            try
            {
                _questMarkerBuffer.Clear();
                World.All<QuestMarker>().FillList(_questMarkerBuffer);

                for (int i = 0; i < _questMarkerBuffer.Count && markerIdx < MaxMarkers; i++)
                {
                    QuestMarker qm = _questMarkerBuffer[i];
                    float2 markerUV = MinimapRenderer.WorldToNormalizedMapPos(qm.Position, _mapBoundsRect);

                    float offsetU = markerUV.x - playerUV.x;
                    float offsetV = markerUV.y - playerUV.y;

                    float scaledU = offsetU * zoom * MinimapConfig.Size.Value;
                    float scaledV = offsetV * zoom * MinimapConfig.Size.Value;

                    float rotU, rotV;
                    if (rotAngle != 0f)
                    {
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
                    bool clamped = dist > edgeLimit;
                    if (clamped && dist > 0.01f)
                    {
                        float clampFactor = edgeLimit / dist;
                        rotU *= clampFactor;
                        rotV *= clampFactor;
                    }

                    _markerTransforms[markerIdx].anchoredPosition = new Vector2(rotU, rotV);
                    _markerImages[markerIdx].gameObject.SetActive(true);
                    _markerImages[markerIdx].color = clamped
                        ? new Color(1f, 0.5f, 0f, 0.9f)
                        : new Color(1f, 0.85f, 0f, 1f);
                    markerIdx++;
                }
            }
            catch (Exception) { }

            for (int i = markerIdx; i < MaxMarkers; i++)
            {
                if (_markerImages[i] != null)
                    _markerImages[i].gameObject.SetActive(false);
            }
        }

        private void UpdateCardinalLabels(float rotAngle)
        {
            if (_cardinalTransforms == null) return;
            float radius = MinimapConfig.Size.Value / 2f + 10f;
            for (int i = 0; i < CardinalCount; i++)
            {
                float angle = (CardinalBaseAngles[i] + rotAngle) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                _cardinalTransforms[i].anchoredPosition = new Vector2(x, y);
            }
        }

        private void UpdateCustomMarker(float2 playerUV, float zoom, float rotAngle, float mapScale)
        {
            if (_customMarkerImage == null) return;

            Awaken.TG.Main.Maps.Compasses.Compass compass = World.Any<Awaken.TG.Main.Maps.Compasses.Compass>();
            Location loc = compass != null ? compass.CustomMarkerLocation : null;
            if (loc == null)
            {
                _customMarkerImage.gameObject.SetActive(false);
                return;
            }

            float2 markerUV = MinimapRenderer.WorldToNormalizedMapPos(loc.Coords, _mapBoundsRect);
            float offsetU = markerUV.x - playerUV.x;
            float offsetV = markerUV.y - playerUV.y;

            float scaledU = offsetU * zoom * MinimapConfig.Size.Value;
            float scaledV = offsetV * zoom * MinimapConfig.Size.Value;

            float radians = rotAngle * Mathf.Deg2Rad;
            float cosR = Mathf.Cos(radians);
            float sinR = Mathf.Sin(radians);

            float rotU = rotAngle != 0f ? scaledU * cosR - scaledV * sinR : scaledU;
            float rotV = rotAngle != 0f ? scaledU * sinR + scaledV * cosR : scaledV;
            rotU *= mapScale;
            rotV *= mapScale;

            float edgeLimit = MinimapConfig.Size.Value / 2f - 6f;
            float dist = Mathf.Sqrt(rotU * rotU + rotV * rotV);
            if (dist > edgeLimit && dist > 0.01f)
            {
                float clampFactor = edgeLimit / dist;
                rotU *= clampFactor;
                rotV *= clampFactor;
            }

            _customMarkerTransform.anchoredPosition = new Vector2(rotU, rotV);
            _customMarkerImage.gameObject.SetActive(true);
        }

        private void CreateUI(Canvas hudCanvas)
        {
            float size = MinimapConfig.Size.Value;

            // Outer container (not masked) - holds the minimap and markers
            _minimapContainer = new GameObject("MinimapContainer");
            _rootTransform = _minimapContainer.AddComponent<RectTransform>();
            _minimapContainer.transform.SetParent(hudCanvas.transform, false);
            ApplyPosition();
            _rootTransform.sizeDelta = new Vector2(size, size);

            // Masked inner root with circle mask
            _minimapRoot = new GameObject("MinimapMasked");
            var maskedRT = _minimapRoot.AddComponent<RectTransform>();
            _minimapRoot.transform.SetParent(_minimapContainer.transform, false);
            maskedRT.anchorMin = Vector2.zero;
            maskedRT.anchorMax = Vector2.one;
            maskedRT.sizeDelta = Vector2.zero;
            maskedRT.anchoredPosition = Vector2.zero;

            var maskImage = _minimapRoot.AddComponent<Image>();
            maskImage.sprite = CreateCircleSprite((int)size);
            maskImage.color = new Color(0.1f, 0.1f, 0.1f, MinimapConfig.BackgroundOpacity.Value);
            _minimapRoot.AddComponent<Mask>().showMaskGraphic = true;

            // Map image (inside mask)
            var imageObj = new GameObject("MinimapImage");
            imageObj.transform.SetParent(_minimapRoot.transform, false);
            _minimapImage = imageObj.AddComponent<RawImage>();
            _mapImageTransform = imageObj.GetComponent<RectTransform>();
            _mapImageTransform.anchorMin = Vector2.zero;
            _mapImageTransform.anchorMax = Vector2.one;
            _mapImageTransform.sizeDelta = Vector2.zero;
            _mapImageTransform.anchoredPosition = Vector2.zero;

            // Border (inside mask)
            var borderObj = new GameObject("MinimapBorder");
            borderObj.transform.SetParent(_minimapRoot.transform, false);
            _borderImage = borderObj.AddComponent<Image>();
            var borderRT = borderObj.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            borderRT.anchoredPosition = Vector2.zero;
            _borderImage.sprite = CreateBorderSprite((int)size);
            _borderImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Quest markers (outside mask so they aren't clipped)
            _markerImages = new RawImage[MaxMarkers];
            _markerTransforms = new RectTransform[MaxMarkers];
            Texture2D dotTex = GetMarkerDotTexture();
            for (int i = 0; i < MaxMarkers; i++)
            {
                var markerObj = new GameObject("QuestMarker_" + i);
                markerObj.transform.SetParent(_minimapContainer.transform, false);
                _markerImages[i] = markerObj.AddComponent<RawImage>();
                _markerTransforms[i] = markerObj.GetComponent<RectTransform>();
                _markerTransforms[i].anchorMin = new Vector2(0.5f, 0.5f);
                _markerTransforms[i].anchorMax = new Vector2(0.5f, 0.5f);
                _markerTransforms[i].pivot = new Vector2(0.5f, 0.5f);
                _markerTransforms[i].sizeDelta = new Vector2(8, 8);
                _markerImages[i].texture = dotTex;
                _markerImages[i].color = new Color(1f, 0.85f, 0f, 1f);
                markerObj.SetActive(false);
            }

            // Custom marker (compass marker)
            var customMarkerObj = new GameObject("CustomMarker");
            customMarkerObj.transform.SetParent(_minimapContainer.transform, false);
            _customMarkerImage = customMarkerObj.AddComponent<RawImage>();
            _customMarkerTransform = customMarkerObj.GetComponent<RectTransform>();
            _customMarkerTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _customMarkerTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _customMarkerTransform.pivot = new Vector2(0.5f, 0.5f);
            _customMarkerTransform.sizeDelta = new Vector2(10, 10);
            _customMarkerImage.texture = GetCustomMarkerRingTexture();
            _customMarkerImage.color = new Color(0.3f, 0.5f, 1f, 1f);
            customMarkerObj.SetActive(false);

            // Player arrow (inside mask)
            var arrowObj = new GameObject("PlayerArrow");
            arrowObj.transform.SetParent(_minimapRoot.transform, false);
            _arrowImage = arrowObj.AddComponent<RawImage>();
            _arrowTransform = arrowObj.GetComponent<RectTransform>();
            _arrowTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowTransform.pivot = new Vector2(0.5f, 0.5f);
            _arrowTransform.sizeDelta = new Vector2(20, 20);
            _arrowTransform.anchoredPosition = Vector2.zero;
            _arrowImage.texture = GetArrowTexture();
            _arrowImage.color = Color.white;

            // Cardinal direction labels (outside mask)
            _cardinalLabels = new TextMeshProUGUI[CardinalCount];
            _cardinalTransforms = new RectTransform[CardinalCount];
            var gameFont = MinimapConfig.UseGameFont.Value ? TMP_Settings.defaultFontAsset : null;
            for (int i = 0; i < CardinalCount; i++)
            {
                var labelObj = new GameObject("Cardinal_" + CardinalLetters[i]);
                labelObj.transform.SetParent(_minimapContainer.transform, false);
                _cardinalLabels[i] = labelObj.AddComponent<TextMeshProUGUI>();
                _cardinalLabels[i].text = CardinalLetters[i];
                _cardinalLabels[i].fontSize = 14f;
                _cardinalLabels[i].fontStyle = TMPro.FontStyles.Bold;
                _cardinalLabels[i].alignment = TextAlignmentOptions.Center;
                _cardinalLabels[i].color = i == 0 ? new Color(1f, 0.3f, 0.3f, 1f) : Color.white;
                _cardinalLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                _cardinalLabels[i].overflowMode = TextOverflowModes.Overflow;
                if (gameFont != null)
                    _cardinalLabels[i].font = gameFont;

                var shadow = labelObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(1, -1);

                _cardinalTransforms[i] = labelObj.GetComponent<RectTransform>();
                _cardinalTransforms[i].anchorMin = new Vector2(0.5f, 0.5f);
                _cardinalTransforms[i].anchorMax = new Vector2(0.5f, 0.5f);
                _cardinalTransforms[i].pivot = new Vector2(0.5f, 0.5f);
                _cardinalTransforms[i].sizeDelta = new Vector2(24, 20);
            }

            // Offset label (shows when nudging position)
            var offsetObj = new GameObject("OffsetLabel");
            offsetObj.transform.SetParent(_minimapContainer.transform, false);
            _offsetLabel = offsetObj.AddComponent<Text>();
            _offsetLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _offsetLabel.fontSize = 12;
            _offsetLabel.alignment = TextAnchor.MiddleCenter;
            _offsetLabel.color = Color.white;
            var offsetRT = offsetObj.GetComponent<RectTransform>();
            offsetRT.anchorMin = new Vector2(0.5f, 0f);
            offsetRT.anchorMax = new Vector2(0.5f, 0f);
            offsetRT.pivot = new Vector2(0.5f, 1f);
            offsetRT.anchoredPosition = new Vector2(0, -4);
            offsetRT.sizeDelta = new Vector2(120, 20);
            offsetObj.SetActive(false);
        }

        private void ShowOffsetLabel()
        {
            if (_offsetLabel == null) return;
            _offsetLabel.text = "X:" + MinimapConfig.OffsetX.Value + "  Y:" + MinimapConfig.OffsetY.Value;
            _offsetLabel.gameObject.SetActive(true);
            _offsetLabelHideTime = Time.time + 2f;
        }

        private void ApplyPosition()
        {
            float offsetX = MinimapConfig.OffsetX.Value;
            float offsetY = MinimapConfig.OffsetY.Value;
            switch (MinimapConfig.Position.Value)
            {
                case MinimapPosition.TopLeft:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(0, 1);
                    _rootTransform.pivot = new Vector2(0, 1);
                    _rootTransform.anchoredPosition = new Vector2(offsetX, -offsetY);
                    break;
                case MinimapPosition.TopRight:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(1, 1);
                    _rootTransform.pivot = new Vector2(1, 1);
                    _rootTransform.anchoredPosition = new Vector2(-offsetX, -offsetY);
                    break;
                case MinimapPosition.BottomLeft:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(0, 0);
                    _rootTransform.pivot = new Vector2(0, 0);
                    _rootTransform.anchoredPosition = new Vector2(offsetX, offsetY);
                    break;
                case MinimapPosition.BottomRight:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(1, 0);
                    _rootTransform.pivot = new Vector2(1, 0);
                    _rootTransform.anchoredPosition = new Vector2(-offsetX, offsetY);
                    break;
                case MinimapPosition.MiddleLeft:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(0, 0.5f);
                    _rootTransform.pivot = new Vector2(0, 0.5f);
                    _rootTransform.anchoredPosition = new Vector2(offsetX, offsetY);
                    break;
                case MinimapPosition.MiddleRight:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(1, 0.5f);
                    _rootTransform.pivot = new Vector2(1, 0.5f);
                    _rootTransform.anchoredPosition = new Vector2(-offsetX, offsetY);
                    break;
                case MinimapPosition.TopMiddle:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(0.5f, 1);
                    _rootTransform.pivot = new Vector2(0.5f, 1);
                    _rootTransform.anchoredPosition = new Vector2(offsetX, -offsetY);
                    break;
                case MinimapPosition.BottomMiddle:
                    _rootTransform.anchorMin = _rootTransform.anchorMax = new Vector2(0.5f, 0);
                    _rootTransform.pivot = new Vector2(0.5f, 0);
                    _rootTransform.anchoredPosition = new Vector2(offsetX, offsetY);
                    break;
            }
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f;
            float radiusSq = radius * radius;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    pixels[y * size + x] = (dx * dx + dy * dy) <= radiusSq ? Color.white : Color.clear;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateBorderSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f;
            float outerSq = radius * radius;
            float inner = radius - 2f;
            float innerSq = inner * inner;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float distSq = dx * dx + dy * dy;
                    pixels[y * size + x] = (distSq <= outerSq && distSq >= innerSq) ? Color.white : Color.clear;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Texture2D GetArrowTexture()
        {
            if (_arrowTexture != null) return _arrowTexture;
            int s = 48;
            _arrowTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[s * s];
            float cx = s / 2f;
            float cy = s / 2f;
            float thickness = 3.5f;
            float arrowLen = s * 0.4f;
            Color white = Color.white;
            Color outline = new Color(0, 0, 0, 0.9f);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x - cx + 0.5f;
                    float py = y - cy + 0.5f;
                    // Two line segments forming a chevron/arrow
                    float d1 = DistToSegment(px, py, 0f, arrowLen, -arrowLen * 0.55f, -arrowLen * 0.15f);
                    float d2 = DistToSegment(px, py, 0f, arrowLen, arrowLen * 0.55f, -arrowLen * 0.15f);
                    float d = Mathf.Min(d1, d2);
                    if (d <= thickness)
                        pixels[y * s + x] = white;
                    else if (d <= thickness + 1.5f)
                        pixels[y * s + x] = outline;
                    else
                        pixels[y * s + x] = Color.clear;
                }
            }
            _arrowTexture.SetPixels(pixels);
            _arrowTexture.Apply();
            return _arrowTexture;
        }

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

        private static Texture2D GetMarkerDotTexture()
        {
            if (_markerDotTexture != null) return _markerDotTexture;
            int s = 16;
            _markerDotTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s / 2f;
            float rSq = r * r;
            Color[] pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    pixels[y * s + x] = (dx * dx + dy * dy) <= rSq ? Color.white : Color.clear;
                }
            _markerDotTexture.SetPixels(pixels);
            _markerDotTexture.Apply();
            return _markerDotTexture;
        }

        private static Texture2D GetCustomMarkerRingTexture()
        {
            if (_customMarkerRingTexture != null) return _customMarkerRingTexture;
            int s = 16;
            _customMarkerRingTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s / 2f;
            float outerSq = r * r;
            float inner = r - 2.5f;
            float innerSq = inner * inner;
            Color[] pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    float distSq = dx * dx + dy * dy;
                    pixels[y * s + x] = (distSq <= outerSq && distSq >= innerSq) ? Color.white : Color.clear;
                }
            _customMarkerRingTexture.SetPixels(pixels);
            _customMarkerRingTexture.Apply();
            return _customMarkerRingTexture;
        }

        void OnDestroy()
        {
            CleanupForSceneChange();
        }
    }
}
