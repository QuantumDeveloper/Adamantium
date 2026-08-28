namespace Adamantium.UI.Core.Resources;

public interface ISetter
{
   string Property { get; set; }

   Object Value { get; set; }

   public string TargetName { get; set; }

   /// <summary>Where this setter stands in the markup, among all the triggers of one collection. Two triggers writing
   /// the SAME property on the same part are resolved by this - the one written LOWER wins, which is the only rule a
   /// theme author can reason about. Resolving by which fired last instead made the look depend on the history of
   /// events: a drop-down row that was both selected and keyboard-highlighted came out accent or grey depending on
   /// whether it was the first opening or the third. Stamped when the trigger joins its collection.</summary>
   int DeclarationOrder { get; set; }

   /// <summary>How far down a <see cref="Style.BasedOn"/> chain the style that owns this setter stands. Compared BEFORE
   /// <see cref="DeclarationOrder"/>, so a derived style always outranks the base it is built on however the two happen
   /// to be numbered: the more local rule wins, which is the only reading a theme author can hold in their head. A
   /// CheckBox is BasedOn a ToggleButton and must not wear its accent-filled label.</summary>
   int StyleBand { get; set; }


   void Apply(IFundamentalUIComponent component, Style style, ITheme theme);
   
   void Remove(IFundamentalUIComponent component, Style style, ITheme theme);
}