using System;
using System.Collections;
using System.Collections.Generic;
using SephiriaDirectConnect;

static class Program
{
    static int passed;
    static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
    static T Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    static void Main()
    {
        Test("a stalled fade has a bounded deadline", () => {
            var op = new SessionOperation(); op.Enter("fade", 100, 20);
            op.Check(119.9f); Assert(Throws<TimeoutException>(() => op.Check(120)).Message.Contains("fade"));
        });
        Test("each new stage gets its own deadline", () => {
            var op = new SessionOperation(); op.Enter("connect", 0, 15); op.Check(14);
            op.Enter("authenticate", 14, 25); op.Check(38);
            Throws<TimeoutException>(() => op.Check(39));
        });
        Test("cancel interrupts before the timeout", () => {
            var op = new SessionOperation(); op.Enter("enter world", 0, 60); op.Cancelled = true;
            Throws<OperationCanceledException>(() => op.Check(1));
        });
        Test("server rejection is preserved before a later disconnect", () => {
            var op = new SessionOperation(); op.Enter("authenticate", 0, 25);
            op.Failure = SessionOperation.Rejection("DIFFERENT_VERSION"); op.Disconnected = true;
            Assert(Throws<InvalidOperationException>(() => op.Check(1)).Message.Contains("版本"));
        });
        Test("recovery ignores cancellation and old error but remains bounded", () => {
            var op = new SessionOperation { Cancelled = true, Failure = "rejected", Recovering = true };
            op.Enter("recover", 5, 10); op.Check(6); Throws<TimeoutException>(() => op.Check(15));
        });
        Test("nested iterator fault reaches outer runner and executes finally", () => {
            var events = new List<string>(); using var pump = new RoutinePump(Parent(events, Fault()));
            Assert(pump.MoveNext()); Assert(pump.Current == null);
            Throws<InvalidOperationException>(() => pump.MoveNext()); pump.Dispose();
            Assert(events.Count == 1 && events[0] == "released");
        });
        Test("cancelled nested operation releases resources", () => {
            var events = new List<string>(); var op = new SessionOperation(); op.Enter("connect", 0, 15);
            using var pump = new RoutinePump(Parent(events, Waiting(op)));
            Assert(pump.MoveNext()); op.Cancelled = true;
            Throws<OperationCanceledException>(() => pump.MoveNext()); pump.Dispose(); Assert(events.Count == 1);
        });
        Test("disposing suspended runner releases nested and parent resources", () => {
            var events = new List<string>(); var pump = new RoutinePump(Parent(events, Parent(events, Waiting(null))));
            Assert(pump.MoveNext()); pump.Dispose(); pump.Dispose(); Assert(events.Count == 2);
        });
        Test("inner disposal failure cannot skip parent cleanup", () => {
            var events = new List<string>(); var pump = new RoutinePump(Parent(events, new BadDispose()));
            Assert(pump.MoveNext()); Throws<InvalidOperationException>(() => pump.Dispose()); Assert(events.Count == 1);
        });
        Test("normal nested completion does not invent extra wait frames", () => {
            var events = new List<string>(); using var pump = new RoutinePump(Parent(events, OneFrame()));
            Assert(pump.MoveNext()); Assert(!pump.MoveNext()); Assert(events.Count == 1);
        });
        Console.WriteLine($"{passed} regression tests passed. Unity/network runtime is not exercised.");
    }
    static IEnumerator Parent(List<string> events, IEnumerator child)
    {
        try { yield return child; } finally { events.Add("released"); }
    }
    static IEnumerator Fault() { yield return null; throw new InvalidOperationException("injected failure"); }
    static IEnumerator Waiting(SessionOperation operation)
    {
        while (true) { operation?.Check(1); yield return null; }
    }
    static IEnumerator OneFrame() { yield return null; }
    sealed class BadDispose : IEnumerator, IDisposable
    {
        public object Current => null;
        public bool MoveNext() => true;
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => throw new InvalidOperationException("injected disposal failure");
    }
}
