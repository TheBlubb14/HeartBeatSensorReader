using System.Runtime.CompilerServices;

namespace BleExplorer;

public static class UiSynchronization
{
    private static SynchronizationContext _context;

    public static void Init(SynchronizationContext? context = null)
    {
        _context = context ?? SynchronizationContext.Current!;
    }

    public static SynchronizationContext SwitchToUiThread()
    {
        return _context;
    }
}

public static class SynchronizationContextAwaiterExtension
{
    public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext context)
    {
        return new SynchronizationContextAwaiter(context);
    }
}

public struct SynchronizationContextAwaiter : INotifyCompletion
{
    // https://thomaslevesque.com/2015/11/11/explicitly-switch-to-the-ui-thread-in-an-async-method/

    private static readonly SendOrPostCallback _postCallback = state => ((Action)state)();
    private readonly SynchronizationContext _context;

    public SynchronizationContextAwaiter(SynchronizationContext context)
    {
        _context = context;
    }

    public void OnCompleted(Action continuation) => _context.Post(_postCallback, continuation);

#pragma warning disable CA1822 // Declare as static
    public void GetResult() { }
#pragma warning restore CA1822 // Declare as static
    public bool IsCompleted => _context == SynchronizationContext.Current;
}
