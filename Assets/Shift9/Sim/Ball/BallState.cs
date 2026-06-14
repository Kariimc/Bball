namespace Shift9.Sim.Ball
{
    /// <summary>What the ball is currently doing — drives whose logic owns it each tick.</summary>
    public enum BallState : byte
    {
        InPossession,
        InFlightShot,
        InFlightPass,
        Loose,
        OutOfBounds
    }

    /// <summary>What the ball struck on a given simulation step.</summary>
    public enum BallContact : byte
    {
        None,
        Rim,
        Backboard,
        ThroughNet  // passed cleanly down through the hoop — a made basket
    }
}
