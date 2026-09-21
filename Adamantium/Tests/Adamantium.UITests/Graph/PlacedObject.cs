using System.ComponentModel;
using System.Runtime.CompilerServices;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UITests.Graph;

/// <summary>AN APPLICATION'S THING ON THE PLANE, as a test has to supply one: the canvas holds no such type of its own -
/// what is on the plane is the application's data, and the control draws it.</summary>
public class PlacedObject : ICanvasObject, INotifyPropertyChanged
{
    private double _left;
    private double _top;
    private double _width;
    private double _height;
    private object _content;

    public event PropertyChangedEventHandler PropertyChanged;

    public double Left
    {
        get => _left;
        set => Set(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => Set(ref _top, value);
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, value);
    }

    public object Content
    {
        get => _content;
        set => Set(ref _content, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (Equals(field, value)) return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
