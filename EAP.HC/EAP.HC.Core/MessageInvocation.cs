using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace EAP.HC.Core;

internal static class MessageInvocation
{
    public static async Task InvokeAsync(object instance, string messageName, Arguments args)
    {
        var method = instance.GetType().GetMethod(messageName, new[] { typeof(Arguments) })
            ?? throw new MissingMethodException(instance.GetType().FullName, messageName);
        if (method.ReturnType == typeof(void)
            && method.IsDefined(typeof(AsyncStateMachineAttribute), false))
        {
            throw new InvalidOperationException($"{messageName} is async void; return Task so dispatch can await completion.");
        }

        object returned;
        try
        {
            returned = method.Invoke(instance, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        if (returned is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }
}
