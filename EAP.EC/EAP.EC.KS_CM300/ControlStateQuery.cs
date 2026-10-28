using EAP.HC.Core;
using EAP.HC.Core.Entities;
using Secs4Net;

namespace EAP.EC.KS_CM300;

internal enum ControlStateQueryStatus
{
    Success,
    Unavailable,
    NoReply,
    InvalidReply,
    Timeout,
    Cancelled,
    StaleConnection,
    Failed
}

internal sealed record ControlStateQueryResult(
    ControlStateQueryStatus Status, ControlState? State = null, string Message = "", Exception Exception = null)
{
    public bool Success => Status == ControlStateQueryStatus.Success;
}

internal static class ControlStateQuery
{
    public static async Task<ControlStateQueryResult> ExecuteAsync(
        Func<SecsMessage, CancellationToken, Task<SecsMessage>> send,
        TimeSpan timeout, CancellationToken cancellationToken = default, string queryId = null)
    {
        using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = new SecsMessage(1, 3)
            {
                Name = $"SVID29:{queryId}",
                SecsItem = Item.L(Item.U4(29))
            };
            // Cancel transport in finally, before returning, so a timeout cannot outrun pending-reply cleanup.
            var reply = await send(message, transportCancellation.Token).WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            if (reply == null)
            {
                return new(ControlStateQueryStatus.NoReply,
                    Message: "S1F3/SVID29 received no reply (send failed, disconnected or T3 expired).");
            }

            if (reply.S != 1 || reply.F != 4 || reply.SecsItem?.Format != SecsFormat.List
                || reply.SecsItem.Count != 1 || reply.SecsItem[0].Count != 1)
            {
                return new(ControlStateQueryStatus.InvalidReply,
                    Message: "S1F3/SVID29 expected S1F4 with exactly one scalar control state.");
            }

            var value = SecsItemUtil.GetItemValue(reply.SecsItem[0]);
            if (!int.TryParse(value?.ToString(), out var state)
                || !Enum.IsDefined(typeof(ControlState), state))
            {
                return new(ControlStateQueryStatus.InvalidReply,
                    Message: $"S1F3/SVID29 returned an invalid control state: {value}.");
            }

            return new(ControlStateQueryStatus.Success, (ControlState)state);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(ControlStateQueryStatus.Cancelled, Message: "S1F3/SVID29 query was cancelled.");
        }
        catch (TimeoutException)
        {
            return new(ControlStateQueryStatus.Timeout,
                Message: $"S1F3/SVID29 query exceeded its {timeout.TotalMilliseconds:F0} ms deadline.");
        }
        catch (Exception ex)
        {
            return new(ControlStateQueryStatus.Failed,
                Message: $"S1F3/SVID29 query failed: {ex.Message}", Exception: ex);
        }
        finally
        {
            transportCancellation.Cancel();
        }
    }
}
