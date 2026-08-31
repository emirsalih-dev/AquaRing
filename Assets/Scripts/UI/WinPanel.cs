using UnityEngine;
using UnityEngine.UI;

namespace Aquaring.UI
{
    /// <summary>
    /// Tiny "You did it! / Try again" overlay. The GameManager toggles it and wires
    /// the retry button. Kept deliberately dumb – it owns no game state.
    /// </summary>
    public class WinPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _retryButton;

        /// <summary>Raised when the player taps "Try again".</summary>
        public event System.Action RetryRequested;

        private void Awake()
        {
            if (_root == null) _root = gameObject;
            if (_retryButton != null)
                _retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            // Initial visibility is owned by the GameManager (see GameManager.Retry).
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);
    }
}
