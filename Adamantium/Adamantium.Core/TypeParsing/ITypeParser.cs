namespace Adamantium.Core.TypeParsing;

public interface ITypeParser<out T>
{
    T Parse(string value);
}