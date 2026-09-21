namespace Adamantium.UI.Markup.AST;

/// <summary>Where a <c>x:</c> directive may be written. The two forms are not interchangeable: an attribute directive
/// says something ABOUT the element it sits on (<c>x:Name</c>, <c>x:Key</c>), while a value directive stands where a
/// value is expected and produces one (<c>{x:Type ...}</c>, <c>{x:Null}</c>).</summary>
public enum AumlDirectiveUsage
{
    /// <summary>Written as an attribute on an element: <c>x:Name="Chrome"</c>.</summary>
    Attribute,

    /// <summary>Written in value position, in braces: <c>Background="{x:Null}"</c>.</summary>
    Value
}
