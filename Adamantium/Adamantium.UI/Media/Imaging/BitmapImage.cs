using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media.Imaging
{
   public sealed class BitmapImage : BitmapSource
   {
      public static readonly AdamantiumProperty UriSourceProperty = AdamantiumProperty.Register(nameof(UriSource),
         typeof (Uri), typeof (BitmapImage), new PropertyMetadata(null, UriChanged));

      private static void UriChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
      {
         var o = adamantiumObject as BitmapImage;
         if (o != null)
         {
            // var device = Application.Current.Services.Get<GraphicsDevice>();
            // if (device != null)
            // {
            //    var source = (Uri) e.NewValue;
            //    o.DXTexture?.Dispose();
            //    try
            //    {
            //       //o.DXTexture = Texture.Load(device, source.IsAbsoluteUri ? source.AbsolutePath : source.OriginalString);
            //    }
            //    catch (Exception exception)
            //    {
            //       o.DecodeFailed?.Invoke(o, new ExceptionEventArgs(exception));
            //    }
            // }
         }
      }

      public Uri UriSource
      {
         get { return GetValue<Uri>(UriSourceProperty); }
         set { SetValue(UriSourceProperty, value);}
      }

      public BitmapImage(Uri source)
      {
         // var device = Application.Current.Services.Get<GraphicsDevice>();
         // try
         // {
         //    //DXTexture = Texture.Load(device, source.IsAbsoluteUri ? source.AbsolutePath : source.OriginalString);
         // }
         // catch (Exception exception)
         // {
         //    DecodeFailed?.Invoke(this, new ExceptionEventArgs(exception));
         // }
      }

      public event EventHandler<ExceptionEventArgs> DecodeFailed;
   }
}
