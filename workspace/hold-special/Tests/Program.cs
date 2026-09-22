using SephiriaHoldSpecial;

int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    count++;
    Console.WriteLine("PASS " + name);
}

var cycle = new RepeatCycle();
cycle.Begin(0, 0.12f);
Check(!cycle.Ready(0, false), "physical press is not duplicated in its frame");
Check(!cycle.Ready(0.15f, false), "wait for animation acknowledgement instead of repeatedly paying cost");
Check(!cycle.Ready(0.3f, true), "never retrigger while animation is running");
Check(cycle.Ready(0.31f, false), "resume immediately after observed animation ends");
cycle.Sent(0.31f, 0.12f, true);
Check(!cycle.Ready(0.4f, false), "minimum repeat interval");
Check(!cycle.Ready(0.6f, false), "network delay does not cause immediate duplicate");
Check(cycle.Ready(0.82f, false), "rejected input is retried after timeout");
cycle.Sent(1, 0.25f, false);
Check(!cycle.Ready(1.2f, false), "instant action throttled");
Check(cycle.Ready(1.26f, false), "buff/reload actions need no attack animation acknowledgement");
Check(!cycle.CanReleaseCharge(false, true), "stale full-charge display without active charge is ignored");
Check(!cycle.CanReleaseCharge(true, false), "partial charge is not released");
Check(cycle.CanReleaseCharge(true, true), "full charge is released");
cycle.ReleasedCharge(2, 0.12f);
Check(!cycle.CanReleaseCharge(true, true), "replication delay cannot release the same charge twice");
Check(!cycle.Ready(2.01f, false), "do not press again immediately after releasing charge");
Check(!cycle.Ready(2.2f, true), "wait for charged attack animation");
Check(cycle.Ready(2.21f, false), "next charge can begin after attack ends");
Check(!cycle.CanReleaseCharge(false, false), "charge cycle reset after native charge clears");
Check(cycle.CanReleaseCharge(true, true), "a new completed charge can release again");
cycle.Begin(10, 0.12f);
Check(!cycle.Ready(10.2f, false), "new physical hold does not inherit previous animation acknowledgement");
Check(cycle.CanReleaseCharge(true, true), "new physical hold clears release latch");
cycle.Sent(11, 0.12f, true);
cycle.Observe(true);
Check(cycle.Ready(11.13f, false), "busy observed before gating still acknowledges animation");
Console.WriteLine($"{count} repeat/charge lifecycle checks passed.");
