using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Aquaring.Input;

namespace Aquaring.UI
{
    /// <summary>
    /// A hold-to-fire on-screen button for one water jet. Put it on a UI <see cref="Image"/>.
    /// Uses pointer events (not <see cref="Button"/>) so it reports the *held* state and
    /// supports multi-touch – the player can press both jets at once.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class WaterJetButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private JetSide _side = JetSide.Left;

        [Tooltip("Router that receives the held state. Auto-found in the scene if empty.")]
        [SerializeField] private JetInputRouter _router;

        [Header("Press feedback")]
        [SerializeField] private float _pressedScale = 0.92f;
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.30f);
        [SerializeField] private Color _pressedColor = new Color(0.4f, 0.85f, 1f, 0.55f);

        private Image _image;
        private Vector3 _baseScale;
        private int _activePointerId = int.MinValue;

        public JetSide Side
        {
            get => _side;
            set => _side = value;
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            _baseScale = transform.localScale;
            if (_router == null)
            {
#if UNITY_2022_2_OR_NEWER
                _router = Object.FindFirstObjectByType<JetInputRouter>();
#else
                _router = Object.FindObjectOfType<JetInputRouter>();
#endif
            }
            SetVisualPressed(false);
        }

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return; // already tracking a finger
            _activePointerId = eventData.pointerId;
            _router?.SetButtonHeld(_side, true);
            SetVisualPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            Release();
        }

        private void Release()
        {
            _activePointerId = int.MinValue;
            _router?.SetButtonHeld(_side, false);
            SetVisualPressed(false);
        }

        private void SetVisualPressed(bool pressed)
        {
            if (_image != null)
                _image.color = pressed ? _pressedColor : _normalColor;
            transform.localScale = pressed ? _baseScale * _pressedScale : _baseScale;
        }
    }
}
