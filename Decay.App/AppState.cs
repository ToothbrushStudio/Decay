using Decay.Renderer;

namespace Decay.App
{
    /// <summary>
    /// Session-level app state. The tier lives here until Phase 5 wires real
    /// licensing (desktop keys, App Store / Play IAP); the UI already reads
    /// every gate through TierPolicy so activation is a one-line swap.
    /// </summary>
    public static class AppState
    {
        public static DecayTier Tier { get; set; } = DecayTier.Free;
    }
}
