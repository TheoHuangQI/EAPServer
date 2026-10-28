namespace EAP.EC.KS_CM300;

// Owns one channel's lifetime. A restart waits for the cancelled run to leave before polling again.
internal sealed class ChannelPollingTask
{
    private readonly object _gate = new();
    private CancellationTokenSource _cancellation;
    private Task _completion = Task.CompletedTask;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _cancellation != null && !_cancellation.IsCancellationRequested;
        }
    }

    public bool Start(Func<CancellationToken, Task> poll, Action<Exception> onError)
    {
        lock (_gate)
        {
            if (_cancellation != null && !_cancellation.IsCancellationRequested)
                return false;

            var previous = _completion;
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            _cancellation = cancellation;
            _completion = Task.Run(async () =>
            {
                try
                {
                    await previous.ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await poll(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Stop, disconnect and a control-state change are normal exits.
                }
                catch (Exception ex)
                {
                    // Background failures are observed even when no stop event follows.
                    try { onError(ex); } catch { }
                }
                finally
                {
                    lock (_gate)
                    {
                        if (ReferenceEquals(_cancellation, cancellation))
                            _cancellation = null;
                        cancellation.Dispose();
                    }
                }
            });
            return true;
        }
    }

    // Cancellation and disposal use the same lock, so a late stop cannot touch a disposed source.
    public Task StopAsync()
    {
        lock (_gate)
        {
            _cancellation?.Cancel();
            return _completion;
        }
    }
}
