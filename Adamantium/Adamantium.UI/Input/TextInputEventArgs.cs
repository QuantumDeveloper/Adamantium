namespace Adamantium.UI.Input
{
   public class TextInputEventArgs:RoutedEventArgs
   {
      public string Text { get; private set; }

      public TextInputEventArgs(string text)
      {
         Text = text;
      }
   }
}
