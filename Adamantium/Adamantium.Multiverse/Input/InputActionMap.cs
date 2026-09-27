namespace Adamantium.Multiverse.Input;

/// <summary>
/// A set of actions that go together - the camera's, a tool's, a menu's - switched on and off together. Of two maps
/// the one with the higher <see cref="Priority"/> reads first, and a control that drove one of its actions does not
/// reach the maps below that frame: an open menu takes Escape from the game.
/// </summary>
public sealed class InputActionMap
{
    private readonly List<InputAction> actions = [];

    public InputActionMap(string name, int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('.'))
        {
            throw new ArgumentException("A map's name must not contain '.': it is joined with the action's by one.",
                nameof(name));
        }

        Name = name;
        Priority = priority;
    }

    public string Name { get; }

    public int Priority { get; }

    /// <summary>Whether its actions are read. Switched off, they are all up, and the ones held report released.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The gamepad slot its gamepad bindings read.</summary>
    public int GamepadSlot { get; set; }

    public IReadOnlyList<InputAction> Actions => actions;

    /// <summary>The action named <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">The map has no such action.</exception>
    public InputAction this[string name] =>
        TryGetAction(name, out var action) ? action : throw new KeyNotFoundException($"'{Name}' has no action '{name}'.");

    public InputAction Add(InputAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Map != null)
        {
            throw new InvalidOperationException($"'{action.FullName}' is already in a map.");
        }

        if (TryGetAction(action.Name, out _))
        {
            throw new ArgumentException($"'{Name}' already has an action '{action.Name}'.", nameof(action));
        }

        action.Map = this;
        actions.Add(action);
        return action;
    }

    public InputAction Add(string name, InputActionType type, params InputBinding[] defaultBindings)
    {
        return Add(new InputAction(name, type, defaultBindings));
    }

    public bool TryGetAction(string name, out InputAction action)
    {
        foreach (var candidate in actions)
        {
            if (candidate.Name == name)
            {
                action = candidate;
                return true;
            }
        }

        action = null;
        return false;
    }
}
