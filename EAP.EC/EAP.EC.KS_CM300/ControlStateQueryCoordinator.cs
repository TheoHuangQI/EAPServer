namespace EAP.EC.KS_CM300;

// Shares only an unfinished refresh. Completed control states are never cached here.
internal sealed class ControlStateQueryCoordinator
{
    private sealed class Refresh(long generation)
    {
        public long Generation { get; } = generation;
        public string QueryId { get; } = Guid.NewGuid().ToString("N");
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<ControlStateQueryResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiters;
        public bool Finished;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Refresh> _active = new();

    public async Task<ControlStateQueryResult> RunAsync(string equipmentId, long generation,
        Func<string, CancellationToken, Task<ControlStateQueryResult>> refresh,
        CancellationToken cancellationToken = default, Action<string, bool> joined = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Refresh current;
        bool shared;
        lock (_gate)
        {
            shared = _active.TryGetValue(equipmentId, out current)
                && current.Generation == generation && !current.Cancellation.IsCancellationRequested
                && !current.Finished;
            if (!shared)
            {
                current = new Refresh(generation);
                _active[equipmentId] = current;
            }
            current.Waiters++;
        }

        try
        {
            // Record every caller's source/trace, including callers sharing the same query ID.
            try { joined?.Invoke(current.QueryId, shared); } catch { }
            if (!shared)
                _ = RefreshAsync(equipmentId, current, refresh);
            return await current.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                current.Waiters--;
                if (current.Waiters == 0)
                {
                    RemoveIfCurrent(equipmentId, current);
                    // One cancelled caller cannot cancel another caller's query.
                    // Cancel the transport only after the final waiter leaves.
                    if (!current.Finished)
                        current.Cancellation.Cancel();
                    else
                        current.Cancellation.Dispose();
                }
            }
        }
    }

    public void Invalidate(string equipmentId)
    {
        lock (_gate)
        {
            if (_active.Remove(equipmentId, out var current))
                current.Cancellation.Cancel();
        }
    }

    private async Task RefreshAsync(string equipmentId, Refresh current,
        Func<string, CancellationToken, Task<ControlStateQueryResult>> refresh)
    {
        ControlStateQueryResult result;
        try
        {
            current.Cancellation.Token.ThrowIfCancellationRequested();
            result = await refresh(current.QueryId, current.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (current.Cancellation.IsCancellationRequested)
        {
            result = new(ControlStateQueryStatus.Cancelled, Message: "Shared SVID29 refresh was cancelled.");
        }
        catch (Exception ex)
        {
            result = new(ControlStateQueryStatus.Failed, Message: ex.Message, Exception: ex);
        }
        lock (_gate)
        {
            current.Finished = true;
            RemoveIfCurrent(equipmentId, current);
            current.Completion.TrySetResult(result);
            if (current.Waiters == 0)
                current.Cancellation.Dispose();
        }
    }

    private void RemoveIfCurrent(string equipmentId, Refresh current)
    {
        if (_active.TryGetValue(equipmentId, out var active) && ReferenceEquals(active, current))
            _active.Remove(equipmentId);
    }
}
