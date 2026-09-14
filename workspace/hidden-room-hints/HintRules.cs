using System;

namespace SephiriaHiddenRoomHints
{
    // Pure geometry shared by runtime and regression tests; no Unity initialization required.
    internal static class HintRules
    {
        internal static bool HasPlannedRoom(int count) => count > 0;

        // A candidate must lie on the game's selected digging lane, inside its owning room.
        // No closest-room or closest-wall fallback: neighboring rooms can share the same index.
        internal static bool OnDiggingLane(float dx, float dy, int inwardX, int inwardY, float depth)
        {
            if (Math.Abs(inwardX) + Math.Abs(inwardY) != 1 || depth <= 0) return false;
            float along = dx * inwardX + dy * inwardY;
            float across = dx * inwardY - dy * inwardX;
            return Math.Abs(across) < 0.15f && along >= -0.15f && along <= depth + 0.15f;
        }
    }
}
