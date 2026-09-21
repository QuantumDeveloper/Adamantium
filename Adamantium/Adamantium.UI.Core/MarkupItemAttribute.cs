namespace Adamantium.UI.Core;

/// <summary>Marks a collection that markup may also write as a comma-separated shorthand: the item type it holds, and
/// the item property one token fills. <c>ColumnDefinitions="Auto,*"</c>, a <c>&lt;ColumnDefinition/&gt;</c> child and
/// <c>ColumnDefinitions="Auto,{TemplateBinding W}"</c> then all mean the same thing.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MarkupItemAttribute : Attribute
{
    public Type ItemType { get; set; }

    public string ItemProperty { get; set; }
}
