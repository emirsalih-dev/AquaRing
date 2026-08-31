using UnityEngine;

namespace Aquaring.Gameplay
{
    /// <summary>
    /// Fake contact shadow that stays glued to the tank floor and shrinks / fades
    /// as its target rises. Pure cosmetic – sells the 2.5D depth without any real
    /// lighting. Attach to a sprite object and point <see cref="_target"/> at the ring.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GroundShadow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _groundY = -3.45f;
        [SerializeField] private float _maxHeight = 7f;
        [SerializeField] private Vector2 _scaleFactor = new Vector2(1f, 0.45f); // near, far
        [SerializeField] private Vector2 _alpha = new Vector2(0.34f, 0.06f);    // near, far

        private SpriteRenderer _sr;
        private Vector3 _baseScale;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        public void SetTarget(Transform target) => _target = target;

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 pos = transform.position;
            pos.x = _target.position.x;
            pos.y = _groundY;
            transform.position = pos;

            float h = Mathf.Clamp01((_target.position.y - _groundY) / _maxHeight);
            float k = Mathf.Lerp(_scaleFactor.x, _scaleFactor.y, h);
            transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, 1f);

            Color c = _sr.color;
            c.a = Mathf.Lerp(_alpha.x, _alpha.y, h);
            _sr.color = c;
        }
    }
}
