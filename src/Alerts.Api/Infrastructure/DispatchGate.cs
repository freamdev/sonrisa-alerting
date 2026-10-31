namespace Alerts.Api.Infrastructure;

// All dispatcher instances in this single-process application share the gate.
public sealed class DispatchGate : IDisposable
{
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
