using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Adamantium.Win32;

namespace Adamantium.Engine
{
   /// <summary>
   /// Extension for standard <see cref="Form"/> control to avoid flickering during resizing and drawing
   /// </summary>
   public class RenderForm : Form
   {
      /// <summary>
      /// Constructs <see cref="RenderForm"/>
      /// </summary>
      public RenderForm()
      {
         ResizeRedraw = false;
         SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
         previousWindowState = FormWindowState.Normal;
         AllowUserResizing = false;
      }

      private const int SIZE_RESTORED = 0;
      private const int SIZE_MINIMIZED = 1;
      private const int SIZE_MAXIMIZED = 2;
      private const uint PBT_APMRESUMESUSPEND = 7;
      private const uint PBT_APMQUERYSUSPEND = 0;
      private const int SC_MONITORPOWER = 0xF170;
      private const int SC_SCREENSAVE = 0xF140;
      private const int MNC_CLOSE = 1;
      private Size cachedSize;
      private FormWindowState previousWindowState;
      private bool isUserResizing;
      private bool allowUserResizing;
      private bool isBackgroundFirstDraw;
      private bool isSizeChangedWithoutResizeBegin;

      /// <summary>
      /// Allow user to resize <see cref="RenderForm"/>
      /// </summary>
      public bool AllowUserResizing
      {
         get
         {
            return allowUserResizing;
         }
         set
         {
            if (allowUserResizing != value)
            {
               allowUserResizing = value;
               MaximizeBox = allowUserResizing;
               FormBorderStyle = allowUserResizing ? FormBorderStyle.Sizable : FormBorderStyle.None;
            }
         }
      }

      /// <summary>
      /// Override windows message loop handling.
      /// </summary>
      /// <param name="m">The Windows <see cref="T:System.Windows.Forms.Message"/> to process.</param>
      protected override void WndProc(ref System.Windows.Forms.Message m)
      {
         long wparam = m.WParam.ToInt64();

         switch (m.Msg)
         {
            case (int)WindowMessages.Size:
               if (wparam == SIZE_MINIMIZED)
               {
                  previousWindowState = FormWindowState.Minimized;
                  OnPauseRendering(EventArgs.Empty);
               }
               else
               {
                  RECT rect;

                  Interop.GetClientRect(m.HWnd, out rect);
                  if (rect.Bottom - rect.Top == 0)
                  {
                     // Rapidly clicking the task bar to minimize and restore a window
                     // can cause a WM_SIZE message with SIZE_RESTORED when 
                     // the window has actually become minimized due to rapid change
                     // so just ignore this message
                  }
                  else if (wparam == SIZE_MAXIMIZED)
                  {
                     if (previousWindowState == FormWindowState.Minimized)
                        OnResumeRendering(EventArgs.Empty);

                     previousWindowState = FormWindowState.Maximized;

                     OnUserResized(EventArgs.Empty);
                     cachedSize = Size;
                  }
                  else if (wparam == SIZE_RESTORED)
                  {
                     if (previousWindowState == FormWindowState.Minimized)
                        OnResumeRendering(EventArgs.Empty);

                     if (!isUserResizing && (Size != cachedSize || previousWindowState == FormWindowState.Maximized))
                     {
                        previousWindowState = FormWindowState.Normal;

                        // Only update when cachedSize is != 0
                        if (cachedSize != Size.Empty)
                        {
                           isSizeChangedWithoutResizeBegin = true;
                        }
                     }
                     else
                        previousWindowState = FormWindowState.Normal;
                  }
               }
               break;
            case (int)WindowMessages.Activateapp:
               if (wparam != 0)
                  OnAppActivated(EventArgs.Empty);
               else
                  OnAppDeactivated(EventArgs.Empty);
               break;
            case (int)WindowMessages.Powerbroadcast:
               if (wparam == PBT_APMQUERYSUSPEND)
               {
                  OnSystemSuspend(EventArgs.Empty);
                  m.Result = new IntPtr(1);
                  return;
               }
               else if (wparam == PBT_APMRESUMESUSPEND)
               {
                  OnSystemResume(EventArgs.Empty);
                  m.Result = new IntPtr(1);
                  return;
               }
               break;
            case (int)WindowMessages.Menuchar:
               m.Result = new IntPtr(MNC_CLOSE << 16);
               return;
            case (int)WindowMessages.Syscommand:
               wparam &= 0xFFF0;
               if (wparam == SC_MONITORPOWER || wparam == SC_SCREENSAVE)
               {
                  var e = new CancelEventArgs();
                  OnScreensaver(e);
                  if (e.Cancel)
                  {
                     m.Result = IntPtr.Zero;
                     return;
                  }
               }
               break;
            case (int)WindowMessages.Displaychange:
               OnMonitorChanged(EventArgs.Empty);
               break;

            case (int)WindowMessages.Input:
                    break;
         }

         base.WndProc(ref m);
      }

      /// <summary>
      /// Raises the <see cref="E:System.Windows.Forms.Form.ResizeBegin"/> event.
      /// </summary>
      /// <param name="e">A <see cref="T:System.EventArgs"/> that contains the event data.</param>
      protected override void OnResizeBegin(EventArgs e)
      {
         isUserResizing = true;

         base.OnResizeBegin(e);
         cachedSize = Size;
         OnPauseRendering(e);
      }

      /// <summary>
      /// Raises the <see cref="E:System.Windows.Forms.Form.ResizeEnd"/> event.
      /// </summary>
      /// <param name="e">A <see cref="T:System.EventArgs"/> that contains the event data.</param>
      protected override void OnResizeEnd(EventArgs e)
      {
         base.OnResizeEnd(e);

         if (isUserResizing && cachedSize != Size)
         {
            OnUserResized(e);
         }

         isUserResizing = false;
         OnResumeRendering(e);
      }

      /// <summary>
      /// Paints the background of the control.
      /// </summary>
      /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
      protected override void OnPaintBackground(PaintEventArgs e)
      {
         if (!isBackgroundFirstDraw)
         {
            base.OnPaintBackground(e);
            isBackgroundFirstDraw = true;
         }
      }

      /// <summary>
      /// Raises the Pause Rendering event.
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnPauseRendering(EventArgs e)
      {
         PauseRendering?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the Resume Rendering event.
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnResumeRendering(EventArgs e)
      {
         ResumeRendering?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the User resized event.
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnUserResized(EventArgs e)
      {
         UserResized?.Invoke(this, e);
      }

      private void OnMonitorChanged(EventArgs e)
      {
         MonitorChanged?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the On App Activated event.
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnAppActivated(EventArgs e)
      {
         AppActivated?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the App Deactivated event
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnAppDeactivated(EventArgs e)
      {
         AppDeactivated?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the System Suspend event
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnSystemSuspend(EventArgs e)
      {
         SystemSuspend?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the System Resume event
      /// </summary>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnSystemResume(EventArgs e)
      {
         SystemResume?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the <see cref="E:Screensaver"/> event.
      /// </summary>
      /// <param name="e">The <see cref="System.ComponentModel.CancelEventArgs"/> instance containing the event data.</param>
      private void OnScreensaver(CancelEventArgs e)
      {
         Screensaver?.Invoke(this, e);
      }

      /// <summary>
      /// Raises the <see cref="E:System.Windows.Forms.Control.ClientSizeChanged"/> event. 
      /// </summary>
      /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data. </param>
      protected override void OnClientSizeChanged(EventArgs e)
      {
         base.OnClientSizeChanged(e);
         if (!isUserResizing && (isSizeChangedWithoutResizeBegin || cachedSize != Size))
         {
            isSizeChangedWithoutResizeBegin = false;
            cachedSize = Size;
            OnUserResized(EventArgs.Empty);
         }
      }

      /// <summary>
      /// Occurs when [app activated].
      /// </summary>
      public event EventHandler<EventArgs> AppActivated;

      /// <summary>
      /// Occurs when [app deactivated].
      /// </summary>
      public event EventHandler<EventArgs> AppDeactivated;

      /// <summary>
      /// Occurs when [monitor changed].
      /// </summary>
      public event EventHandler<EventArgs> MonitorChanged;

      /// <summary>
      /// Occurs when [pause rendering].
      /// </summary>
      public event EventHandler<EventArgs> PauseRendering;

      /// <summary>
      /// Occurs when [resume rendering].
      /// </summary>
      public event EventHandler<EventArgs> ResumeRendering;

      /// <summary>
      /// Occurs when [screensaver].
      /// </summary>
      public event EventHandler<CancelEventArgs> Screensaver;

      /// <summary>
      /// Occurs when [system resume].
      /// </summary>
      public event EventHandler<EventArgs> SystemResume;

      /// <summary>
      /// Occurs when [system suspend].
      /// </summary>
      public event EventHandler<EventArgs> SystemSuspend;

      /// <summary>
      /// Occurs when [user resized].
      /// </summary>
      public event EventHandler<EventArgs> UserResized;

   }

}
