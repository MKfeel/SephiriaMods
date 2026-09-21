using System;
using System.Collections;
using System.Collections.Generic;

namespace SephiriaDirectConnect;

// No Unity dependencies: the same timeout/cancellation and iterator handling are tested offline.
internal sealed class SessionOperation
{
    public string Stage { get; private set; }
    public string Failure { get; set; }
    public bool Cancelled { get; set; }
    public bool NeedsRecovery { get; set; }
    public bool Recovering { get; set; }
    public bool AuthenticationAccepted { get; set; }
    public bool Disconnected { get; set; }
    private float deadline;

    public void Enter(string stage, float now, float timeout)
    {
        Stage = stage;
        deadline = now + timeout;
    }

    public void Check(float now)
    {
        if (!Recovering)
        {
            if (Cancelled) throw new OperationCanceledException("已取消连接。");
            if (Failure != null) throw new InvalidOperationException(Failure);
        }
        if (now >= deadline) throw new TimeoutException(Stage + "超时。");
    }

    public static string Rejection(string code)
    {
        switch (code)
        {
            case "DIFFERENT_VERSION": return "游戏版本与主机不一致。";
            case "OWNER_ALREADY_CONNECTED": return "此玩家仍在主机中，请等待旧连接清理后重试。";
            case "LOBBY_NOT_CREATED_OR_RUN_ALREADY_STARTED": return "主机未开放加入，或对局已开始且不符合重连条件。";
            default: return "主机拒绝连接：" + (string.IsNullOrEmpty(code) ? "未知原因" : code);
        }
    }
}

internal sealed class RoutinePump : IDisposable
{
    private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    public object Current { get; private set; }
    public RoutinePump(IEnumerator routine) { stack.Push(routine); }
    public bool MoveNext()
    {
        while (stack.Count != 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                (top as IDisposable)?.Dispose();
                continue;
            }
            if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
            Current = top.Current;
            return true;
        }
        return false;
    }
    public void Dispose()
    {
        // Always dispose the remaining parents even if an inner finally throws.
        Exception failure = null;
        while (stack.Count != 0)
        {
            try { (stack.Pop() as IDisposable)?.Dispose(); }
            catch (Exception ex) { failure = failure ?? ex; }
        }
        if (failure != null) throw failure;
    }
}
