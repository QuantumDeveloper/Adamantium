namespace Adamantium.Engine.Graphics
{
   public class GraphicsDeviceParameters
   {
      public GraphicsAdapter Adapter { get; set; }

      public bool D2DSupportEnabled
      {
         get { return (flags & DeviceCreationFlags.BgraSupport) != 0; }
         set
         {
            if (value)
            {
               flags |= DeviceCreationFlags.BgraSupport;
            }
            else
            {
               flags &= ~DeviceCreationFlags.BgraSupport;
            }
         }
      }

      public bool DebugModeEnabled
      {
         get { return (flags & DeviceCreationFlags.Debug) != 0; }
         set
         {
            if (value)
            {
               flags |= DeviceCreationFlags.Debug;
            }
            else
            {
               flags &= ~DeviceCreationFlags.Debug;
            }
         }
      }

      public bool VideoSupportEnabled
      {
         get { return (flags & DeviceCreationFlags.VideoSupport) != 0; }
         set
         {
            if (value)
            {
               flags |= DeviceCreationFlags.VideoSupport;
            }
            else
            {
               flags &= ~DeviceCreationFlags.VideoSupport;
            }
         }
      }

      public FeatureLevel Profile { get; set; }

      private DeviceCreationFlags flags;
      

      public GraphicsDeviceParameters()
      { }

      public GraphicsDeviceParameters(GraphicsAdapter adapter, bool debugEnabled, bool d2dEnabled, bool videoSupport, FeatureLevel profile)
      {
         Adapter = adapter;
         DebugModeEnabled = debugEnabled;
         D2DSupportEnabled = d2dEnabled;
         VideoSupportEnabled = videoSupport;
         Profile = profile;
      }

      public GraphicsDeviceParameters(GraphicsDeviceParameters copy)
      {
         Adapter = copy.Adapter;
         D2DSupportEnabled = copy.D2DSupportEnabled;
         DebugModeEnabled = copy.DebugModeEnabled;
         VideoSupportEnabled = copy.VideoSupportEnabled;
         Profile = copy.Profile;
      }

      public static implicit operator DeviceCreationFlags(GraphicsDeviceParameters parameters)
      {
         return parameters.flags;
      }

   }
}
