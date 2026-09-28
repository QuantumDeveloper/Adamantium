namespace Adamantium.Graphics.Core.Presentation
{
    /// <summary>
    /// How a window's frames reach the screen. Stated as an INTENT rather than as a Vulkan present mode, because the
    /// mode a surface can actually give is a property of the driver and the display - the presenter picks the closest
    /// one it is offered and never fails over a preference.
    /// </summary>
    public enum PresentPolicy
    {
        /// <summary>Follow whoever owns the default - a window follows its application. FIRST so that it is also
        /// <c>default(PresentPolicy)</c>: a window that was never told anything inherits, which is what almost every
        /// window wants. Never reaches a presenter - it is resolved to a real policy before the swapchain is built.</summary>
        Inherit,

        /// <summary>No waiting for the display at all: the frame goes up as soon as it is ready, and the loop is never
        /// paced by presentation. Tearing is possible. This is what an engine runs at when it is being measured, and
        /// what a 3D viewport wants - AcquireNextImage is a SYNCHRONOUS block in the frame loop, so with update and
        /// render on one thread anything else here paces the whole engine, not just the screen. (Vulkan: Immediate.)</summary>
        Immediate,

        /// <summary>No tearing, and still no waiting for the application: the newest finished frame replaces whatever
        /// was queued. Costs back-pressure - images come back on the display's schedule, measured here at 0.6-0.8 ms a
        /// frame of pure blocking. The right default for ordinary UI. (Vulkan: Mailbox, falling back to Fifo.)</summary>
        Adaptive,

        /// <summary>Locked to the display's refresh. No tearing, lowest power, frames paced exactly. What a shipped
        /// application usually wants on a laptop. (Vulkan: Fifo - the one mode every driver must support.)</summary>
        VSync
    }
}
