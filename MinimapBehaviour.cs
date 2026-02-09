using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using Awaken.TG.Main.UI;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Cameras;
using Awaken.TG.Main.FastTravel;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Assets;
using Awaken.TG.Graphics.MapServices;
using Awaken.TG.Main.Maps.Markers;

namespace TaintedGrailMinimap
{
    public class MinimapBehaviour : MonoBehaviour
    {
        private GameObject _minimapRoot;
        private RawImage _minimapImage;
        private Image _borderImage;
        private RectTransform _rootTransform;
        private bool _initialized;
        private bool _isVisible = true;
        private static Texture2D _circleMask;

        private Texture2D _mapTexture;
        private Rect _mapBoundsRect;
        private bool _mapReady;
        private int _sceneGeneration;
        private SpriteReference _spriteRef;
        private Texture2D _outputTexture;

        private FogOfWar _fogOfWar;
        private RenderTexture _fogMaskTexture;
        private float _lastFogRefreshTime;
        private const float FogRefreshInterval = 0.5f;

        private RawImage _arrowImage;
        private RectTransform _arrowTransform;
        private static Texture2D _arrowTexture;

        private const int MaxMarkers = 8;
        private RawImage[] _markerImages;
        private RectTransform[] _markerTransforms;
        private static Texture2D _markerDotTexture;
        private readonly System.Collections.Generic.List<QuestMarker> _questMarkerBuffer = new System.Collections.Generic.List<QuestMarker>();

        private SceneReference _currentScene;

        void Update()
        {
            // Keybind handling
            if (Input.GetKeyDown(MinimapConfig.ToggleKey.Value))
                _isVisible = !_isVisible;

            if (Input.GetKeyDown(MinimapConfig.ZoomInKey.Value))
            {
                float newZoom = Mathf.Clamp(MinimapConfig.ZoomLevel.Value + 0.25f, 0.5f, 3f);
                MinimapConfig.ZoomLevel.Value = newZoom;
            }
            if (Input.GetKeyDown(MinimapConfig.ZoomOutKey.Value))
            {
                float newZoom = Mathf.Clamp(MinimapConfig.ZoomLevel.Value - 0.25f, 0.5f, 3f);
                MinimapConfig.ZoomLevel.Value = newZoom;
            }

            if (!MinimapConfig.Enabled.Value)
            {
                if (_minimapRoot != null) _minimapRoot.SetActive(false);
                return;
            }
            if (!_initialized)
            {
                TryInitialize();
                return;
            }

            // Scene change detection
            var sceneService = World.Services.Get<SceneService>();
            if (sceneService != null)
            {
                SceneReference newScene = sceneService.ActiveSceneRef;
                if (!newScene.Equals(_currentScene))
                {
                    CleanupForSceneChange();
                    return;
                }
            }

            // Config hot-reload: re-apply position/size if changed
            if (_rootTransform != null)
            {
                float configSize = MinimapConfig.Size.Value;
                if (Mathf.Abs(_rootTransform.sizeDelta.x - configSize) > 0.1f)
                {
                    _rootTransform.sizeDelta = new Vector2(configSize, configSize);
                    if (_outputTexture != null) Destroy(_outputTexture);
                    _outputTexture = new Texture2D((int)configSize, (int)configSize, TextureFormat.RGBA32, false);
                }

                ApplyPosition();
            }

            if (_minimapRoot != null)
                _minimapRoot.SetActive(_isVisible);
        }

        private void TryInitialize()
        {
            var canvasService = World.Services.Get<CanvasService>();
            if (canvasService == null || canvasService.HUDCanvas == null) return;
            var hero = Hero.Current;
            if (hero == null) return;

            var commonRefs = CommonReferences.Get;
            if (commonRefs == null) return;

            var sceneService = World.Services.Get<SceneService>();
            if (sceneService == null) return;

            SceneReference activeScene = sceneService.ActiveSceneRef;
            MapSceneData mapSceneData = default(MapSceneData);
            if (!commonRefs.MapData.byScene.TryGetValue(in activeScene, out mapSceneData))
                return;

            CreateUI(canvasService.HUDCanvas);
            _initialized = true;
            _currentScene = activeScene;

            LoadMapSprite(mapSceneData);

            var mapService = World.Services.Get<MapService>();
            if (mapService != null)
            {
                _fogOfWar = mapService.LoadFogOfWar(activeScene);
            }
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
            _mapReady = true;

            int outputSize = (int)MinimapConfig.Size.Value;
            _outputTexture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);

            ReleaseSprite();
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
            if (_fogOfWar != null)
            {
                var mapService = World.Services.Get<MapService>();
                if (mapService != null)
                    mapService.ReleaseFogOfWar(_fogOfWar);
                _fogOfWar = null;
            }

            if (_fogMaskTexture != null)
            {
                _fogMaskTexture.Release();
                _fogMaskTexture = null;
            }

            ReleaseSprite();

            _mapTexture = null;
            _mapReady = false;

            if (_outputTexture != null)
            {
                Destroy(_outputTexture);
                _outputTexture = null;
            }

            if (_minimapRoot != null)
            {
                Destroy(_minimapRoot);
                _minimapRoot = null;
            }

            _initialized = false;
            _lastFogRefreshTime = 0f;
        }

        void LateUpdate()
        {
            if (!_initialized || !_mapReady || !_isVisible) return;
            if (_mapTexture == null) return;

            var hero = Hero.Current;
            if (hero == null) return;

            if (_fogOfWar != null && Time.time - _lastFogRefreshTime >= FogRefreshInterval)
            {
                if (_fogMaskTexture != null)
                    _fogMaskTexture.Release();
                _fogMaskTexture = _fogOfWar.CreateMaskTexture();
                _lastFogRefreshTime = Time.time;
            }

            float2 playerUV = MinimapRenderer.WorldToNormalizedMapPos(hero.Coords, _mapBoundsRect);

            float rotAngle = MinimapConfig.Rotation.Value == RotationMode.RotateWithPlayer
                ? World.Any<GameCamera>()?.MainCamera?.transform.eulerAngles.y ?? 0f
                : 0f;

            float zoom = MinimapConfig.ZoomLevel.Value;

            int size = (int)MinimapConfig.Size.Value;
            Texture2D mask = GetCircleMask(size);

            MinimapRenderer.CompositeMinimapTexture(
                _mapTexture, mask, _outputTexture,
                playerUV, zoom, rotAngle, _fogMaskTexture);

            _minimapImage.texture = _outputTexture;

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

            UpdateQuestMarkers(playerUV, zoom, rotAngle);
        }

        private void UpdateQuestMarkers(float2 playerUV, float zoom, float rotAngle)
        {
            int markerIdx = 0;
            float minimapRadius = MinimapConfig.Size.Value / 2f;
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
                    Vector3 markerWorldPos = qm.Position;
                    float2 markerUV = MinimapRenderer.WorldToNormalizedMapPos(markerWorldPos, _mapBoundsRect);

                    float offsetU = markerUV.x - playerUV.x;
                    float offsetV = markerUV.y - playerUV.y;

                    float scaledU = offsetU * zoom * MinimapConfig.Size.Value;
                    float scaledV = offsetV * zoom * MinimapConfig.Size.Value;

                    float rotU, rotV;
                    if (rotAngle != 0f)
                    {
                        rotU = scaledU * cosR + scaledV * sinR;
                        rotV = -scaledU * sinR + scaledV * cosR;
                    }
                    else
                    {
                        rotU = scaledU;
                        rotV = scaledV;
                    }

                    float distFromCenter = Mathf.Sqrt(rotU * rotU + rotV * rotV);
                    if (distFromCenter > minimapRadius - 4f)
                    {
                        continue;
                    }

                    _markerTransforms[markerIdx].anchoredPosition = new Vector2(rotU, rotV);
                    _markerImages[markerIdx].gameObject.SetActive(true);
                    markerIdx++;
                }
            }
            catch (System.Exception) { }

            for (int i = markerIdx; i < MaxMarkers; i++)
            {
                if (_markerImages[i] != null)
                    _markerImages[i].gameObject.SetActive(false);
            }
        }

        private void CreateUI(Canvas hudCanvas)
        {
            float size = MinimapConfig.Size.Value;
            _minimapRoot = new GameObject("Minimap");
            _rootTransform = _minimapRoot.AddComponent<RectTransform>();
            _minimapRoot.transform.SetParent(hudCanvas.transform, false);
            ApplyPosition();
            _rootTransform.sizeDelta = new Vector2(size, size);

            var imageObj = new GameObject("MinimapImage");
            imageObj.transform.SetParent(_minimapRoot.transform, false);
            _minimapImage = imageObj.AddComponent<RawImage>();
            var imageRT = imageObj.GetComponent<RectTransform>();
            imageRT.anchorMin = Vector2.zero;
            imageRT.anchorMax = Vector2.one;
            imageRT.sizeDelta = Vector2.zero;
            imageRT.anchoredPosition = Vector2.zero;

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

            _markerImages = new RawImage[MaxMarkers];
            _markerTransforms = new RectTransform[MaxMarkers];
            Texture2D dotTex = GetMarkerDotTexture();
            for (int i = 0; i < MaxMarkers; i++)
            {
                var markerObj = new GameObject("QuestMarker_" + i);
                markerObj.transform.SetParent(_minimapRoot.transform, false);
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

            var arrowObj = new GameObject("PlayerArrow");
            arrowObj.transform.SetParent(_minimapRoot.transform, false);
            _arrowImage = arrowObj.AddComponent<RawImage>();
            _arrowTransform = arrowObj.GetComponent<RectTransform>();
            _arrowTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowTransform.pivot = new Vector2(0.5f, 0.5f);
            _arrowTransform.sizeDelta = new Vector2(16, 16);
            _arrowTransform.anchoredPosition = Vector2.zero;
            _arrowImage.texture = GetArrowTexture();
            _arrowImage.color = Color.white;
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
            }
        }

        public static Texture2D GetCircleMask(int size)
        {
            if (_circleMask != null && _circleMask.width == size) return _circleMask;
            _circleMask = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
            _circleMask.SetPixels(pixels);
            _circleMask.Apply();
            return _circleMask;
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
            int s = 32;
            _arrowTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[s * s];
            float cx = s / 2f;
            for (int y = 0; y < s; y++)
            {
                float progress = (float)y / s;
                float halfWidth = cx * (1f - progress);
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x - cx + 0.5f);
                    pixels[y * s + x] = dx <= halfWidth ? Color.white : Color.clear;
                }
            }
            _arrowTexture.SetPixels(pixels);
            _arrowTexture.Apply();
            return _arrowTexture;
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

        void OnDestroy()
        {
            CleanupForSceneChange();
        }
    }
}
