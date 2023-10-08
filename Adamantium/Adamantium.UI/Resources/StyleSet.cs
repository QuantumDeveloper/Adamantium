using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Resources;

public class StyleSet : AdamantiumComponent, IStyleSet
{
    private bool _isInitialized;

    [Content]
    public StylesCollection Styles { get; }
    
    public Uri Source { get; set; }

    public StyleSet()
    {
        Styles = new StylesCollection();
    }

    public void AddStyles(IEnumerable<Style> styles)
    {
        Styles.AddRange(styles);
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
            foreach(var style in Styles)
            {
                style.Theme = theme;
            }
            OnInitialize(theme);
            _isInitialized = true;
        }
    }

    protected virtual void OnInitialize(ITheme theme)
    {

    }

    public string Name { get; set; }

    public bool Initialized => _isInitialized;
}