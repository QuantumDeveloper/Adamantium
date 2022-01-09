using System;
using System.Threading;

namespace Adamantium.UI.Threading;

public class DispatcherContext : IEquatable<DispatcherContext>
{
    public DispatcherContext(IApplicationPlatform platform, Thread mainThread)
    {
        Platform = platform;
        MainThread = mainThread;
    }
        
    public IApplicationPlatform Platform { get; }
        
    public Thread MainThread { get; }
        
    public Thread UIThread { get; }

    public static bool operator == (DispatcherContext context1, DispatcherContext context2)
    {
        if (ReferenceEquals(context1, null) || ReferenceEquals(context2, null)) return false;

        return context1.Platform == context2.Platform && 
               context1.MainThread == context2.MainThread &&
               context1.UIThread == context2.UIThread;
    }

    public static bool operator !=(DispatcherContext context1, DispatcherContext context2)
    {
        return !(context1 == context2);
    }

    public bool Equals(DispatcherContext other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Platform, other.Platform) && Equals(MainThread, other.MainThread) && Equals(UIThread, other.UIThread);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DispatcherContext) obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Platform, MainThread, UIThread);
    }
}