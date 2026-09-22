namespace SephiriaHoldSpecial
{
    // Wait for animation acknowledgement before retrying, including over Mirror.
    // No timers here advance or reset the game's own cooldowns/charge meters.
    internal sealed class RepeatCycle
    {
        private float nextAttempt;
        private bool awaitingAnimation;
        private bool sawAnimation;
        private float retryAfter;
        private bool awaitingChargeEnd;

        internal void Begin(float now, float interval)
        {
            nextAttempt = now + interval;
            awaitingAnimation = true; // The physical press already went through.
            sawAnimation = false;
            retryAfter = now + 0.5f;
            awaitingChargeEnd = false;
        }

        internal void Observe(bool busy)
        {
            if (awaitingAnimation && busy) sawAnimation = true;
        }

        internal bool Ready(float now, bool busy)
        {
            Observe(busy);
            if (busy || now < nextAttempt) return false;
            return !awaitingAnimation || sawAnimation || now >= retryAfter;
        }

        internal void Sent(float now, float interval, bool expectAnimation)
        {
            nextAttempt = now + interval;
            awaitingAnimation = expectAnimation;
            sawAnimation = false;
            retryAfter = now + 0.5f;
        }

        internal bool CanReleaseCharge(bool charging, bool fullyCharged)
        {
            if (!charging) awaitingChargeEnd = false;
            return charging && fullyCharged && !awaitingChargeEnd;
        }

        internal void ReleasedCharge(float now, float interval)
        {
            awaitingChargeEnd = true;
            Sent(now, interval, true);
        }
    }
}
