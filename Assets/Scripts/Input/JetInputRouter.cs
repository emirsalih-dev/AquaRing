using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Aquaring.Input
{
    /// <summary>
    /// Central hub for jet input. The on-screen <see cref="Aquaring.UI.WaterJetButton"/>s
    /// push their held-state into this component; <see cref="Aquaring.Gameplay.RingController"/>
    /// reads it every physics step. Keeping the routing here means we can add more
    /// input sources later (gamepad, tilt, tutorial ghost) without touching gameplay code.
    ///
    /// A keyboard fallback (A / Left = left jet, D / Right = right jet) is enabled by
    /// default so the prototype is playable in the Editor without clicking the UI.
    /// </summary>
    public class JetInputRouter : MonoBehaviour, IJetInput
    {
        [Header("Editor / Desktop convenience")]
        [Tooltip("Also read the keyboard (A/Left = left jet, D/Right = right jet). " +
                 "Has no effect on a touch device.")]
        [SerializeField] private bool _enableKeyboardFallback = true;

        // Held-state coming from the touch buttons. One flag per pointer side.
        private bool _leftButtonHeld;
        private bool _rightButtonHeld;

        public bool LeftHeld { get; private set; }
        public bool RightHeld { get; private set; }

        /// <summary>Called by <see cref="Aquaring.UI.WaterJetButton"/> on pointer down/up.</summary>
        public void SetButtonHeld(JetSide side, bool held)
        {
            if (side == JetSide.Left) _leftButtonHeld = held;
            else _rightButtonHeld = held;
        }

        /// <summary>Clears every input source. Used by the GameManager on retry.</summary>
        public void ResetInput()
        {
            _leftButtonHeld = false;
            _rightButtonHeld = false;
            LeftHeld = false;
            RightHeld = false;
        }

        private void Update()
        {
            bool left = _leftButtonHeld;
            bool right = _rightButtonHeld;

            if (_enableKeyboardFallback)
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    left |= kb.aKey.isPressed || kb.leftArrowKey.isPressed;
                    right |= kb.dKey.isPressed || kb.rightArrowKey.isPressed;
                }
#endif
            }

            LeftHeld = left;
            RightHeld = right;
        }
    }
}
