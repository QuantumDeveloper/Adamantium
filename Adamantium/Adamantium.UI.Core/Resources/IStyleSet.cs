namespace Adamantium.UI.Core.Resources;

public interface IStyleSet : IContainer, IName
{
    public StylesCollection Styles { get; }

    void Initialize(ITheme theme);

    bool Initialized { get; }

    public void AddStyles(IEnumerable<Style> styles);
    
    public void Add(Style style);
}