using System;
using UnityEngine;
using UnityEngine.Events;

namespace Aquaring.Gameplay
{
    /// <summary>
    /// Sits on a trigger collider wrapped around the base of the peg. Reports a win
    /// once the ring has stayed inside the catch zone, roughly centred and moving
    /// slowly, for <see cref="_holdToWin"/> seconds.
    ///
    /// The peg itself is visual-only (no solid collider), so in this front-view
    /// prototype "putting the ring on the peg" is really "parking the ring in the
    /// catch zone and keeping it steady".
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PegTrigger : MonoBehaviour
    {
        [Header("Seat conditions")]
        [Tooltip("Seconds the ring must stay seated before it counts as a win.")]
        [SerializeField] private float _holdToWin = 0.6f;

        [Tooltip("Max horizontal distance between ring centre and peg centre to count as aligned.")]
        [SerializeField] private float _alignTolerance = 0.35f;

        [Tooltip("The ring must also be 'calm' (see RingController.IsCalm) to seat.")]
        [SerializeField] private bool _requireCalm = true;

        [Header("Events")]
        [Tooltip("Raised once, the first time the ring is fully seated.")]
        public UnityEvent OnRingSeated;

        /// <summary>C# event mirror of <see cref="OnRingSeated"/> for code subscribers.</summary>
        public event Action RingSeated;

        /// <summary>0..1 progress toward a win while the ring is in the zone (for UI).</summary>
        public float SeatProgress { get; private set; }

        private RingController _ringInside;
        private float _seatTimer;
        private bool _won;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var ring = other.GetComponentInParent<RingController>();
            if (ring != null)
                _ringInside = ring;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var ring = other.GetComponentInParent<RingController>();
            if (ring != null && ring == _ringInside)
            {
                _ringInside = null;
                _seatTimer = 0f;
                SeatProgress = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (_won || _ringInside == null)
                return;

            if (IsSeated(_ringInside))
            {
                _seatTimer += Time.fixedDeltaTime;
                SeatProgress = Mathf.Clamp01(_seatTimer / _holdToWin);

                if (_seatTimer >= _holdToWin)
                    Win();
            }
            else
            {
                _seatTimer = Mathf.Max(0f, _seatTimer - Time.fixedDeltaTime * 2f);
                SeatProgress = Mathf.Clamp01(_seatTimer / _holdToWin);
            }
        }

        private bool IsSeated(RingController ring)
        {
            float dx = Mathf.Abs(ring.Body.position.x - transform.position.x);
            if (dx > _alignTolerance)
                return false;

            if (_requireCalm && !ring.IsCalm)
                return false;

            return true;
        }

        private void Win()
        {
            _won = true;
            SeatProgress = 1f;
            OnRingSeated?.Invoke();
            RingSeated?.Invoke();
        }

        /// <summary>Called by the GameManager on retry.</summary>
        public void ResetTrigger()
        {
            _won = false;
            _seatTimer = 0f;
            SeatProgress = 0f;
            _ringInside = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, new Vector3(_alignTolerance * 2f, 0.6f, 0f));
        }
#endif
    }
}
