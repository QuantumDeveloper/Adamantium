using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>An async guard on the OUTGOING view model: return false to veto leaving (e.g. unsaved changes). Runs before
/// the target is resolved.</summary>
public interface IConfirmNavigation
{
    Task<bool> CanNavigateAwayAsync(NavigationContext context, CancellationToken cancellationToken = default);
}
