namespace Aquaring.Input
{
    /// <summary>
    /// Read-only contract that exposes whether the left / right water jets are
    /// currently being pushed. Any input backend (touch buttons, keyboard, AI,
    /// replay system, automated tests) can implement this so the gameplay layer
    /// never depends on a concrete input device.
    /// </summary>
    public interface IJetInput
    {
        /// <summary>True while the left jet should be firing.</summary>
        bool LeftHeld { get; }

        /// <summary>True while the right jet should be firing.</summary>
        bool RightHeld { get; }
    }

    /// <summary>Identifies one of the two water jets.</summary>
    public enum JetSide
    {
        Left = 0,
        Right = 1
    }
}
