using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

public class InputEventArgs:RoutedEventArgs
{
   public InputEventArgs(InputModifiers modifiers, uint timestep)
   {
      Modifiers = modifiers;
      Timestamp = timestep;
   }

   public InputModifiers Modifiers { get; }
   public uint Timestamp { get; }
}