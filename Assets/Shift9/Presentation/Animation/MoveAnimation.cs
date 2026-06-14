using Shift9.Sim.Moves;

namespace Shift9.Presentation.Animation
{
    /// <summary>
    /// Maps a sim move to the integer "MoveId" the Animator's action layer branches on (0 = no
    /// special move). The driver sets MoveId then pulses a DoMove trigger; the state graph plays the
    /// matching clip once a rig is in place.
    /// </summary>
    public static class MoveAnimation
    {
        public const int None = 0;
        public const int BlockId = 20;

        public static int Id(DribbleMove m)
        {
            switch (m)
            {
                case DribbleMove.Crossover: return 1;
                case DribbleMove.Hesitation: return 2;
                case DribbleMove.BehindBack: return 3;
                case DribbleMove.BetweenLegs: return 4;
                case DribbleMove.SignatureCrossover: return 5;
                default: return None;
            }
        }

        public static int Id(FinishMove m)
        {
            switch (m)
            {
                case FinishMove.Layup: return 10;
                case FinishMove.Floater: return 11;
                case FinishMove.Dunk: return 12;
                case FinishMove.SignatureDunk: return 13;
                default: return None; // JumpShot uses the normal Shoot trigger
            }
        }
    }
}
