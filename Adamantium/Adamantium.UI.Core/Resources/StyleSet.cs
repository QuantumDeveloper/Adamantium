namespace Adamantium.UI.Core.Resources;

public class StyleSet : AdamantiumComponent, IStyleSet
{
    public StylesCollection Styles { get; }

    public StyleSet()
    {
        Styles = new StylesCollection();
    }

    public void AddStyles(IEnumerable<Style> styles)
    {
        Styles.AddRange(styles);
    }

    public void Add(Style style)
    {
        if (!Styles.Contains(style))
        {
            Styles.Add(style);
        }
    }

    public void AddOrSetChildComponent(object component)
    {
        if (component is Style style && !Styles.Contains(style))
        {
            Styles.Add(style);
        }
    }

    public void RemoveAllChildComponents()
    {
        Styles.Clear();
    }

    public void Initialize(ITheme theme)
    {
        if (!Initialized)
        {
            OnInitialize(theme);
            foreach(var style in Styles)
            {
                style.Theme = theme;
            }
            Initialized = true;
        }
    }

    protected virtual void OnInitialize(ITheme theme)
    {

    }

    public string Name { get; set; }

    public bool Initialized { get; private set; }
}