namespace Adamantium.UI.Core;

public enum ValuePriority : int
{
    Animation = 0,
    Local = 1,
    Binding = 2,
    Trigger = 3,
    Template = 4,
    Style = 5,
    Inherited = 6,
    /// <summary>What a style says about a TYPE and nothing more - a selector that narrows by nothing but the type name,
    /// setting an INHERITABLE property. Weaker than <see cref="Inherited"/> on purpose: "all text is 14pt" is a default
    /// for the type, and anything a nearer ancestor actually says outranks it.
    /// <para>Without this slot such a setter landed at <see cref="Style"/> and beat inheritance, which is the channel a
    /// control uses to recolour its OWN content on a state change - so one blanket TextBlock style anywhere in an
    /// application silently stopped every selected row, pressed button and disabled label from following its state
    /// (measured in TextBlockStyleMaskTests). The failure was silent, non-local, and reachable by anyone who wrote the
    /// most natural style in the world.</para>
    /// <para>It does not travel: <c>HasExplicitValue</c> scans Animation..Style, so an ancestor holding only a type
    /// default is not an inheritance source. That is deliberate and is what keeps a control's own default safe - a
    /// window saying "text is grey" no longer overrules a button's own style, whatever the theme.</para></summary>
    TypeDefault = 7,
    Default,
    Effective
}