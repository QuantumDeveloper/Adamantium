using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

/// <summary>What <c>{x:Null}</c> becomes: state NOTHING, on purpose. Needed to turn a themed default off - a style
/// setter puts a value in, and with no way to write "none" the only way back would be a second property whose whole job
/// is to say "ignore the first one".</summary>
public class AumlAstNullValueNode : AumlAstNode, IAumlAstValueNode
{
    public AumlAstNullValueNode(IAumlLineInfo info) : base(info)
    {
    }

    /// <summary>Nothing has no type - the property's own decides what null means for it.</summary>
    public IAumlAstTypeReference TypeReference { get; set; }
}
