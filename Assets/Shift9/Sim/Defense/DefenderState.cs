using UnityEngine;

namespace Shift9.Sim.Defense
{
    /// <summary>
    /// A defender as the contest model sees them: where they are and how good their contest is.
    /// <see cref="ContestRating"/> is supplied by the caller (e.g. perimeter defense for a
    /// three, interior defense at the rim) so this stays decoupled from the full attribute set.
    /// </summary>
    public readonly struct DefenderState
    {
        public readonly Vector3 Position;
        public readonly byte ContestRating; // 0..99

        public DefenderState(Vector3 position, byte contestRating)
        {
            Position = position;
            ContestRating = contestRating;
        }
    }
}
