using System;
using System.Runtime.CompilerServices;
using Adamantium.MVVM;
using Adamantium.UI.Core.Commands;
using NUnit.Framework;

namespace Adamantium.MVVM.Tests;

[TestFixture]
public class WeakCanExecuteChangedRelayTests
{
    private sealed class Target
    {
        public int Pings;
        public void Ping() => Pings++;
    }

    // The whole point: a command (here standing in for one owned by a long-lived VM) must NOT keep the target alive
    // through the CanExecuteChanged subscription.
    [Test]
    public void DoesNotKeepTargetAlive()
    {
        var command = new AdamantiumCommand(static () => { });
        var weak = AttachAndDropTarget(command);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False, "the relay held the target strongly — that's the leak we set out to avoid");
        GC.KeepAlive(command);
    }

    [Test]
    public void RelaysWhileTargetIsAlive()
    {
        var command = new AdamantiumCommand(static () => { });
        var target = new Target();
        _ = new WeakCanExecuteChangedRelay<Target>(command, target, static t => t.Ping());

        command.RaiseCanExecuteChanged();
        command.RaiseCanExecuteChanged();

        Assert.That(target.Pings, Is.EqualTo(2));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndDropTarget(ICommand command)
    {
        var target = new Target();
        // In production the command holds the relay (via the event); the relay holds the target only weakly.
        _ = new WeakCanExecuteChangedRelay<Target>(command, target, static t => t.Ping());
        return new WeakReference(target);
    }
}
