using UnityEngine;
using UnityEngine.UI;
using AcesOverTheLines.Weapons;

namespace AcesOverTheLines.UI
{
    // Screen-space hit-confirmation X markers. Subscribes to
    // WeaponSystem.OnHit; on each event, world hit position is projected
    // to screen space and a small yellow X fades from 1.0 to 0.0 alpha
    // over 0.20 s. Markers stick to the world point so they track if the
    // camera moves during the fade.
    //
    // Ports src/ui/hitMarkers.js to a pre-pooled set of uGUI RectTransforms.
    [RequireComponent(typeof(Canvas))]
    public class HitMarkers : MonoBehaviour
    {
        [SerializeField] WeaponSystem weapons;
        [SerializeField] Camera projectionCamera;
        [SerializeField] int poolSize = 32;
        [SerializeField] float lifetimeS = 0.20f;
        [SerializeField] Color color = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] float armLengthPx = 24f;
        [SerializeField] float armThicknessPx = 2f;

        struct Marker
        {
            public RectTransform Rect;
            public CanvasGroup CG;
            public Vector3 WorldPos;
            public float Life;
            public bool Active;
        }

        Marker[] _pool;
        RectTransform _canvasRect;
        Canvas _canvas;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvasRect = _canvas.GetComponent<RectTransform>();
            BuildPool();
        }

        void OnEnable()
        {
            if (weapons != null) weapons.OnHit += HandleHit;
        }

        void OnDisable()
        {
            if (weapons != null) weapons.OnHit -= HandleHit;
        }

        void BuildPool()
        {
            _pool = new Marker[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("HitMarker_" + i, typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(transform, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(armLengthPx, armLengthPx);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var cg = go.GetComponent<CanvasGroup>();
                cg.interactable = false;
                cg.blocksRaycasts = false;
                cg.alpha = 0f;
                CreateArm(rect,  45f);
                CreateArm(rect, -45f);
                go.SetActive(false);
                _pool[i].Rect = rect;
                _pool[i].CG = cg;
            }
        }

        void CreateArm(RectTransform parent, float angle)
        {
            var armGo = new GameObject("Arm", typeof(RectTransform), typeof(Image));
            armGo.transform.SetParent(parent, false);
            var img = armGo.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rect = (RectTransform)armGo.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(armLengthPx, armThicknessPx);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void HandleHit(Vector3 worldPos, string componentName)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].Active) continue;
                _pool[i].Active = true;
                _pool[i].WorldPos = worldPos;
                _pool[i].Life = lifetimeS;
                _pool[i].Rect.gameObject.SetActive(true);
                _pool[i].CG.alpha = 1f;
                return;
            }
            // Pool exhausted — drop silently.
        }

        void Update()
        {
            if (projectionCamera == null || _pool == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                _pool[i].Life -= dt;
                if (_pool[i].Life <= 0f)
                {
                    _pool[i].Active = false;
                    _pool[i].Rect.gameObject.SetActive(false);
                    continue;
                }

                Vector3 screenPos = projectionCamera.WorldToScreenPoint(_pool[i].WorldPos);
                if (screenPos.z < 0f)
                {
                    // Behind the camera — hide this frame but keep the
                    // life-timer running so it expires normally.
                    _pool[i].CG.alpha = 0f;
                    continue;
                }

                Vector2 canvasPos;
                // Screen Space - Overlay canvas: pass null camera. Screen
                // Space - Camera / World Space: pass canvas.worldCamera.
                Camera refCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screenPos, refCam, out canvasPos);
                _pool[i].Rect.anchoredPosition = canvasPos;
                _pool[i].CG.alpha = Mathf.Clamp01(_pool[i].Life / lifetimeS);
            }
        }
    }
}
