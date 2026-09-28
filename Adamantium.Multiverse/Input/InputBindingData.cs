namespace Adamantium.Multiverse.Input;

internal sealed class InputBindingData
{
    public string Control { get; set; }

    public string Negative { get; set; }

    public string Positive { get; set; }

    public string Up { get; set; }

    public string Down { get; set; }

    public string Left { get; set; }

    public string Right { get; set; }

    public string X { get; set; }

    public string Y { get; set; }

    public float? ScaleX { get; set; }

    public float? ScaleY { get; set; }

    public string[] Modifiers { get; set; }

    public static InputBindingData From(InputBinding binding)
    {
        var controls = binding.Controls;
        var data = new InputBindingData
        {
            Modifiers = binding.Modifiers.Count == 0 ? null : binding.Modifiers.Select(control => control.ToString()).ToArray()
        };

        switch (binding.Kind)
        {
            case InputBindingKind.Control:
                data.Control = controls[0].ToString();
                data.ScaleX = Scale(binding.ScaleX);
                break;
            case InputBindingKind.Axis:
                data.Negative = controls[0].ToString();
                data.Positive = controls[1].ToString();
                data.ScaleX = Scale(binding.ScaleX);
                break;
            case InputBindingKind.Vector:
                data.Up = controls[0].ToString();
                data.Down = controls[1].ToString();
                data.Left = controls[2].ToString();
                data.Right = controls[3].ToString();
                break;
            case InputBindingKind.Stick:
                data.X = controls[0].ToString();
                data.Y = controls[1].ToString();
                data.ScaleX = Scale(binding.ScaleX);
                data.ScaleY = Scale(binding.ScaleY);
                break;
        }

        return data;
    }

    public InputBinding ToBinding()
    {
        var modifiers = Modifiers?.Select(InputControl.Parse).ToArray() ?? [];
        if (Control != null)
        {
            return InputBinding.Control(InputControl.Parse(Control), ScaleX ?? 1, modifiers);
        }

        if (Negative != null || Positive != null)
        {
            return InputBinding.Axis(Parse(Negative, nameof(Negative)), Parse(Positive, nameof(Positive)), modifiers);
        }

        if (Up != null || Down != null || Left != null || Right != null)
        {
            return InputBinding.Vector(Parse(Up, nameof(Up)), Parse(Down, nameof(Down)), Parse(Left, nameof(Left)),
                Parse(Right, nameof(Right)), modifiers);
        }

        if (X != null || Y != null)
        {
            return InputBinding.Stick(Parse(X, nameof(X)), Parse(Y, nameof(Y)), ScaleX ?? 1, ScaleY ?? 1, modifiers);
        }

        throw new FormatException("A binding needs a control, negative/positive, up/down/left/right or x/y.");
    }

    private static InputControl Parse(string text, string part)
    {
        return text == null
            ? throw new FormatException($"The binding's '{part}' is missing.")
            : InputControl.Parse(text);
    }

    private static float? Scale(float scale)
    {
        return scale == 1 ? null : scale;
    }
}
