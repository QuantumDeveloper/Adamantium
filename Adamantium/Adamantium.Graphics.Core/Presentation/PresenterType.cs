namespace Adamantium.Graphics.Core.Presentation
{
    public enum PresenterType
   {
      Swapchain = 0,
      RenderTarget = 1,
      /// <summary>A real swapchain on a window-less VK_EXT_headless_surface - for offscreen / designer output.</summary>
      Headless = 2
   }

}
