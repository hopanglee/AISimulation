using System;
using System.Collections.Generic;

public interface ITickEventHub : IService
{
    void Subscribe(Action<double> handler);
    void Subscribe(Action<double> handler, int priority);
    void Unsubscribe(Action<double> handler);
    void Publish(double ticks);
}

public class TickEventHub : ITickEventHub
{
    private readonly List<(int priority, Action<double> handler)> handlers = new();

    public void Initialize() { }

    public void Subscribe(Action<double> handler)
    {
        Subscribe(handler, 0);
    }

    public void Subscribe(Action<double> handler, int priority)
    {
        handlers.Add((priority, handler));
        handlers.Sort((a, b) => a.priority.CompareTo(b.priority));
    }

    public void Unsubscribe(Action<double> handler)
    {
        int idx = handlers.FindIndex(h => h.handler == handler);
        if (idx >= 0) handlers.RemoveAt(idx);
    }

    public void Publish(double ticks)
    {
        if (handlers.Count == 0) return;
        var snapshot = handlers.ToArray();
        for (int i = 0; i < snapshot.Length; i++) snapshot[i].handler(ticks);
    }
}


