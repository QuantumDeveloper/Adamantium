using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WHAT A NODE WORKS OUT - the one thing about a graph that nothing here can know and the application has to
/// say.
/// <para>Carried by a node's <see cref="ICanvasNodeSpecialization"/> it makes that node computable, and
/// <see cref="CanvasGraphRunner"/> does the rest: the order, what arrived on each socket, what needs redoing and when.
/// A specialization that does not carry it is drawn, wired and saved like any other and simply has no value - which is
/// what a comment node is, and why this is a separate contract rather than a method everybody must answer.</para>
/// <para>ASYNCHRONOUS, and not as a flourish: a node that renders, reads a file or asks a service takes as long as it
/// takes, and a graph doing that on the thread it is drawn on stops the window. A node that already has its answer
/// pays nothing for it - a <see cref="ValueTask{TResult}"/> around a value allocates no task and never suspends the
/// walk - so an addition is not taxed for a texture's needs.</para></summary>
public interface ICanvasNodeWork
{
    /// <param name="inputs">What arrived, one entry per input socket and in the node's own socket order, so a node can
    /// read them either way: by what a socket CARRIES, or by where it sits.</param>
    /// <param name="token">Dropped when what this pass was working out has changed underneath it - a node that takes
    /// real time is expected to look at it and give up, because the answer it is making is already stale.</param>
    ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token);
}
