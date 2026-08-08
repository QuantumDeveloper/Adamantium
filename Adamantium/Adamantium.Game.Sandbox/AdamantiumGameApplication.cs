using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls.Navigation;

namespace Adamantium.Game.Sandbox;

// Graphics debug (Vulkan validation layers) is controlled by the entry point (Program.cs: EnableGraphicsDebug).
// Previously this ctor forced it true, which silently won over Program.cs's false and left the validation layer
// loaded - costing ~3/4 of the frame time. Validation is a dev tool; flip it on in Program.cs when chasing a GPU bug.
public class AdamantiumGameApplication : GameApplication
{
    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        base.RegisterServices(containerRegistry);
        // Register the app's own window shell under "workspace"; the Workspace command opens the second window in it.
        Container.Resolve<IWindowShellRegistry>().Register<WorkspaceWindow>("workspace");
        // A dedicated shell for the drag-drop cross-window demo, so items can be dragged between it and the main window.
        Container.Resolve<IWindowShellRegistry>().Register<DragDropWindow>("dragdrop");
        // Its caption carries the quick-access bar, so it needs a window of its own.
        Container.Resolve<IWindowShellRegistry>().Register<RibbonWindow>("ribbon");
        // Shared demo setting (single instance) so the title-bar command and the Navigation-tab toggle see the same flag.
        containerRegistry.RegisterInstance<ViewModels.WindowDemoSettings>(new ViewModels.WindowDemoSettings());
    }
}
