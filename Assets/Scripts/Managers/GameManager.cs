using UnityEngine;
using Aquaring.Gameplay;
using Aquaring.Input;
using Aquaring.UI;

namespace Aquaring.Managers
{
    /// <summary>
    /// Owns the match flow for the v0 prototype: one ring, one peg, win + retry.
    /// New mechanics (timer, score, next ring, moving peg) should hook in here
    /// rather than in the gameplay components.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Playing, Won }

        [Header("Scene references")]
        [SerializeField] private RingController _ring;
        [SerializeField] private PegTrigger _peg;
        [SerializeField] private JetInputRouter _input;
        [SerializeField] private WinPanel _winPanel;

        [Header("Ring spawn")]
        [Tooltip("Where the ring is placed at start and on retry. " +
                 "If a Transform is assigned its position is used instead.")]
        [SerializeField] private Vector2 _spawnPosition = new Vector2(-1.6f, -2.95f);
        [SerializeField] private Transform _spawnPoint;

        public GameState State { get; private set; } = GameState.Playing;

        private void Awake()
        {
            AutoWire();
        }

        private void OnEnable()
        {
            if (_peg != null) _peg.RingSeated += HandleRingSeated;
            if (_winPanel != null) _winPanel.RetryRequested += Retry;
        }

        private void OnDisable()
        {
            if (_peg != null) _peg.RingSeated -= HandleRingSeated;
            if (_winPanel != null) _winPanel.RetryRequested -= Retry;
        }

        private void Start()
        {
            Retry(); // start in a clean, known state
        }

        private void AutoWire()
        {
            // Fallback lookup for a hand-assembled scene. The scene builder wires
            // every reference explicitly, so this normally does nothing.
            // WinPanel disables its own GameObject in Awake, so include inactive objects.
#if UNITY_2022_2_OR_NEWER
            if (_ring == null)     _ring     = Object.FindFirstObjectByType<RingController>(FindObjectsInactive.Include);
            if (_peg == null)      _peg      = Object.FindFirstObjectByType<PegTrigger>(FindObjectsInactive.Include);
            if (_input == null)    _input    = Object.FindFirstObjectByType<JetInputRouter>(FindObjectsInactive.Include);
            if (_winPanel == null) _winPanel = Object.FindFirstObjectByType<WinPanel>(FindObjectsInactive.Include);
#else
            if (_ring == null)     _ring     = Object.FindObjectOfType<RingController>(true);
            if (_peg == null)      _peg      = Object.FindObjectOfType<PegTrigger>(true);
            if (_input == null)    _input    = Object.FindObjectOfType<JetInputRouter>(true);
            if (_winPanel == null) _winPanel = Object.FindObjectOfType<WinPanel>(true);
#endif
        }

        private void HandleRingSeated()
        {
            if (State == GameState.Won) return;

            State = GameState.Won;
            _ring?.Freeze();
            _winPanel?.Show();
        }

        /// <summary>Resets ring, peg and input and returns to <see cref="GameState.Playing"/>.</summary>
        public void Retry()
        {
            State = GameState.Playing;

            Vector2 spawn = _spawnPoint != null ? (Vector2)_spawnPoint.position : _spawnPosition;
            _ring?.ResetTo(spawn);
            _peg?.ResetTrigger();
            _input?.ResetInput();
            _winPanel?.Hide();
        }
    }
}
