using System;
using Adamantium.Engine;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Services;
using SharpDX.DXGI;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.EntityFramework.Processors
{
   public class VideoPlayerProcessor : EntityProcessor
   {
      private GameWindow window;
       private SystemManager systemManager;
       private IGraphicsDeviceService graphicsDeviceService;
      public VideoPlayerProcessor(EntityWorld world, GameWindow window) : base(world)
      {
         this.window = window;
         MultimediaManager.Enable();
         multimediaManager = new MultimediaManager(Services, window);
         multimediaManager.DurationChanged += MultimediaManager_DurationChanged;
         multimediaManager.CurrentTimeChanged += MultimediaManager_CurrentTimeChanged;
         window.ParametersChanging += Window_ParametersChanging;
         window.ParametersChanged += Window_ParametersChanged;
         graphicsDeviceService.DeviceChangeBegin += GraphicsDeviceManager_DeviceChangeBegin;
         graphicsDeviceService.DeviceChangeEnd += GraphicsDeviceManager_DeviceChangeEnd;
      }

      private void MultimediaManager_CurrentTimeChanged(object sender, EventArgs e)
      {
         RaisePropertyChanged("CurrentTime");
      }

      private void MultimediaManager_DurationChanged(object sender, EventArgs e)
      {
         Duration = multimediaManager.Duration;
      }

      private void GraphicsDeviceManager_DeviceChangeEnd(object sender, EventArgs e)
      {
         multimediaManager.Initialize();
      }

      private void GraphicsDeviceManager_DeviceChangeBegin(object sender, EventArgs e)
      {
         multimediaManager.Dispose();
      }

      private void Window_ParametersChanged(object sender, GameWindowParametersEventArgs e)
      {
         surface = window.BackBuffer;
         rectangle = new Rectangle(0,0,window.Width, window.Height);
      }

      private void Window_ParametersChanging(object sender, GameWindowParametersEventArgs e)
      {
         surface?.Dispose();
      }

      private Surface surface;
      private MultimediaManager multimediaManager;
      private Mathematics.Rectangle rectangle;

      public override void LoadContent()
      {
         surface = window.BackBuffer;
      }

      public override void UnloadContent()
      {
         multimediaManager.Dispose();
      }

      public void SetSource(String filePath)
      {
         multimediaManager.Source = filePath;
      }

      public bool IsPaused => multimediaManager.IsPaused;

      public double Volume
      {
         get { return multimediaManager.Volume; }
         set { multimediaManager.Volume = value; }
      }

      public void Play()
      {
         multimediaManager.Play();
      }

      public void Stop()
      {
         multimediaManager.Stop();
      }


      public override void Draw(IGameTime gameTime)
      {
         multimediaManager.TransferVideoFrame(surface, rectangle);
      }

      private double duration;

      public double Duration
      {
         get { return duration; }
         set
         {
            duration = value;
            RaisePropertyChanged();
         }
      }

      public double CurrentTime {
         get { return multimediaManager.CurrentTime; }
         set
         {
            multimediaManager.CurrentTime = value;
            RaisePropertyChanged();
         }
      }

   }
}
