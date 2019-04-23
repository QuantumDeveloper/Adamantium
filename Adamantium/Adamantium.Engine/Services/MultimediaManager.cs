using System;
using System.Diagnostics;
using System.Threading;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.MediaFoundation;

namespace Adamantium.Engine.Services
{
   public class MultimediaManager:IDisposable
   {
      public static void Enable()
      {
         MediaManager.Startup();
      }

      public static void Disable()
      {
         MediaManager.Shutdown();
      }

      public MultimediaManager(IServiceStorage serviceStorage, GameWindow window)
      {
         this.serviceStorage = serviceStorage;
         Window = window;
         Initialize();
      }

      public void Initialize()
      {
      
         dxgiManager = new DXGIDeviceManager();
         dxgiManager.ResetDevice(graphicsDeviceService.GraphicsDevice);

         // Creates the MediaEngineClassFactory
         var mediaEngineFactory = new MediaEngineClassFactory();
         //Assign our dxgi manager, and set format to bgra
         attributes = new MediaEngineAttributes();
         attributes.VideoOutputFormat = (int)(Format)Window.PixelFormat;
         attributes.DxgiManager = dxgiManager;
         
         // Creates MediaEngine for AudioOnly 
         mediaEngine = new MediaEngine(mediaEngineFactory, attributes);
         // Query for MediaEngineEx interface
         mediaEngineEx = mediaEngine.QueryInterface<MediaEngineEx>();
         // Register our PlayBackEvent
         mediaEngine.PlaybackEvent += OnPlaybackCallback;
         
      }

      public bool IsPaused => mediaEngineEx.IsPaused; 

      public void Play()
      {
         if (mediaEngineEx.IsPaused)
         {
            mediaEngineEx?.Play();
         }
         else
         {
            mediaEngineEx?.Pause();
         }
      }

      public double Volume
      {
         get { return mediaEngineEx.Volume; }
         set { mediaEngineEx.Volume = value; }
      }

      public void Stop()
      {
         if (mediaEngineEx != null)
         {
            mediaEngineEx.Pause();
            mediaEngineEx.Source = "";
            mediaEngineEx.Load();
         }
      }


      private string sourcePath;

      public string Source
      {
         get { return sourcePath; }
         set
         {
            sourcePath = value;
            mediaEngineEx.Source = value;
            mediaEngineEx.Load();
         }
      }

      private IServiceStorage serviceStorage;
      private IGraphicsDeviceService graphicsDeviceService;
      private GameWindow Window;
      private static readonly ManualResetEvent eventReadyToPlay = new ManualResetEvent(false);
      private MediaEngine mediaEngine;
      private MediaEngineEx mediaEngineEx;
      private MediaEngineAttributes attributes;
      DXGIDeviceManager dxgiManager;

      private double duration;

      public Double Duration
      {
         get { return mediaEngineEx.Duration; }
         set { duration = value; }
      }

      public Double CurrentTime
      {
         get { return mediaEngineEx.CurrentTime; }
         set { mediaEngineEx.CurrentTime = value; }
      }


      /// <summary>
      /// Called when [playback callback].
      /// </summary>
      /// <param name="playEvent">The play event.</param>
      /// <param name="param1">The param1.</param>
      /// <param name="param2">The param2.</param>
      private void OnPlaybackCallback(MediaEngineEvent playEvent, long param1, int param2)
      {
            System.Diagnostics.Debug.WriteLine(playEvent);
         switch (playEvent)
         {
            case MediaEngineEvent.DurationChange:
               Duration = mediaEngineEx.Duration;
               DurationChanged?.Invoke(this, EventArgs.Empty);
               break;
            case MediaEngineEvent.LoadedData:
               
               break;
            case MediaEngineEvent.CanPlay:
               eventReadyToPlay.Set();
               break;
            case MediaEngineEvent.TimeUpdate:
               CurrentTimeChanged?.Invoke(this, EventArgs.Empty);
               break;
            case MediaEngineEvent.Error:
            case MediaEngineEvent.Abort:
            case MediaEngineEvent.Ended:
               //isMusicStopped = true;
               break;
         }
      }

      public event EventHandler<EventArgs> DurationChanged;
      public event EventHandler<EventArgs> CurrentTimeChanged;

      public void UpdateVideoStream()
      {
         mediaEngineEx.UpdateVideoStream(null, null, null);
      }

      public bool TransferVideoFrame(Surface surface, Mathematics.Rectangle rectangle, RawColorBGRA? borderColor = null)
      {
         if (!mediaEngineEx.IsPaused)
         {
            long ts;
            //Transfer frame if a new one is available
            if (mediaEngine.OnVideoStreamTick(out ts))
            {
               if (ts > 0)
               {
                  try
                  {
                     mediaEngine.TransferVideoFrame(surface, null, rectangle, borderColor);
                     return true;
                  }
                  catch (Exception e)
                  {
                            System.Diagnostics.Debug.WriteLine(e.Message);
                     return false;
                  }
               }
            }
         }
         return false;
      }

      public void Dispose()
      {
         Stop();
         mediaEngine?.Dispose();
         mediaEngineEx?.Dispose();
         attributes?.Dispose();
         dxgiManager?.Dispose();
      }
   }
}
