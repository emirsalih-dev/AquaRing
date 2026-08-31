using UnityEngine;
using Aquaring.Input;

namespace Aquaring.Gameplay
{
    /// <summary>
    /// Drives the floating ring with 2D physics only. There is no real fluid sim –
    /// the "water feel" is faked with three ingredients:
    ///   1. Buoyancy   – a constant upward force that almost cancels gravity, so the
    ///                    ring sinks slowly instead of dropping like a stone.
    ///   2. Wobble     – low-amplitude Perlin-noise force that makes it drift and bob.
    ///   3. Jets       – while a button is held, an upward impulse is applied off-centre
    ///                    (left of / right of the ring) so it also rotates and slides
    ///                    sideways, exactly like the water stream in the classic toy.
    ///
    /// Everything lives in <see cref="FixedUpdate"/> and works on a single
    /// <see cref="Rigidbody2D"/>; the visual 2.5D tilt is purely camera-side.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RingController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Input source for the two jets. If left empty it is looked up in the scene.")]
        [SerializeField] private MonoBehaviour _jetInputSource;

        [Header("Buoyancy (water lift)")]
        [Tooltip("Fraction of gravity cancelled by buoyancy. 1 = floats forever, " +
                 "0 = full gravity. ~0.8 gives a slow, watery sink.")]
        [Range(0f, 1f)]
        [SerializeField] private float _buoyancy = 0.82f;

        [Header("Jet push")]
        [Tooltip("Upward force applied while a jet button is held.")]
        [SerializeField] private float _jetForce = 22f;

        [Tooltip("How far left/right of the ring centre the jet pushes. " +
                 "Bigger = more spin and sideways drift per press.")]
        [SerializeField] private float _jetOffset = 0.45f;

        [Tooltip("Extra sideways shove from a jet, as a fraction of jet force. " +
                 "Left jet shoves right, right jet shoves left – lets the player aim.")]
        [Range(0f, 1f)]
        [SerializeField] private float _jetSidePush = 0.35f;

        [Header("Wobble (idle water motion)")]
        [SerializeField] private float _wobbleForce = 1.0f;
        [SerializeField] private float _wobbleSpeed = 0.9f;

        [Header("Clamps")]
        [SerializeField] private float _maxSpeed = 9f;
        [SerializeField] private float _maxAngularSpeed = 220f;

        private Rigidbody2D _body;
        private IJetInput _input;
        private Vector2 _wobbleSeed;
        private bool _frozen;

        // Cached so external systems (PegTrigger) can query "is the ring calm?".
        public Rigidbody2D Body => _body;
        public bool IsCalm => _body.linearVelocity.magnitude < 1.6f &&
                              Mathf.Abs(_body.angularVelocity) < 60f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _wobbleSeed = new Vector2(Random.value * 100f, Random.value * 100f);
            ResolveInput();
        }

        private void ResolveInput()
        {
            _input = _jetInputSource as IJetInput;
            if (_input == null)
            {
#if UNITY_2022_2_OR_NEWER
                var router = Object.FindFirstObjectByType<JetInputRouter>();
#else
                var router = Object.FindObjectOfType<JetInputRouter>();
#endif
                _input = router;
                _jetInputSource = router;
            }

            if (_input == null)
                Debug.LogWarning($"{nameof(RingController)}: no IJetInput found – jets will not fire.", this);
        }

        private void FixedUpdate()
        {
            if (_frozen) return;

            ApplyBuoyancy();
            ApplyWobble();
            ApplyJets();
            ClampVelocities();
        }

        private void ApplyBuoyancy()
        {
            // Counteract most of gravity with a steady upward force.
            Vector2 gravity = Physics2D.gravity * (_body.gravityScale * _body.mass);
            _body.AddForce(-gravity * _buoyancy);
        }

        private void ApplyWobble()
        {
            float t = Time.time * _wobbleSpeed;
            float nx = Mathf.PerlinNoise(_wobbleSeed.x + t, 0f) - 0.5f;
            float ny = Mathf.PerlinNoise(0f, _wobbleSeed.y + t) - 0.5f;
            _body.AddForce(new Vector2(nx, ny) * (_wobbleForce * 2f));
        }

        private void ApplyJets()
        {
            if (_input == null) return;

            if (_input.LeftHeld)
                FireJet(JetSide.Left);
            if (_input.RightHeld)
                FireJet(JetSide.Right);
        }

        private void FireJet(JetSide side)
        {
            float dir = side == JetSide.Left ? -1f : 1f;

            // Push point sits below the ring, offset to one side -> lift + spin.
            Vector2 pushPoint = _body.worldCenterOfMass + new Vector2(dir * _jetOffset, -_jetOffset);

            // Mostly up, with a nudge toward the opposite side so the player can steer.
            Vector2 force = Vector2.up * _jetForce +
                            Vector2.right * (-dir * _jetForce * _jetSidePush);

            _body.AddForceAtPosition(force, pushPoint);
        }

        private void ClampVelocities()
        {
            if (_body.linearVelocity.magnitude > _maxSpeed)
                _body.linearVelocity = _body.linearVelocity.normalized * _maxSpeed;

            _body.angularVelocity = Mathf.Clamp(_body.angularVelocity, -_maxAngularSpeed, _maxAngularSpeed);
        }

        /// <summary>Stops the ring dead and holds it in place (used on win).</summary>
        public void Freeze()
        {
            _frozen = true;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.bodyType = RigidbodyType2D.Kinematic;
        }

        /// <summary>Teleports the ring back to a spawn pose and resumes simulation.</summary>
        public void ResetTo(Vector2 position, float rotationZ = 0f)
        {
            _frozen = false;
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.position = position;
            _body.rotation = rotationZ;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }
    }
}
