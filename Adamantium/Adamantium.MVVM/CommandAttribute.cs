using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put on a parameterless method to have the generator emit a lazy <c>ICommand</c> property (a
/// <see cref="RelayCommand"/>) that invokes it: <c>void Save()</c> → <c>public ICommand SaveCommand</c>.
/// Set <see cref="CanExecute"/> to the name of a <c>bool</c> method/property gating execution. Override the
/// generated command name with <see cref="Name"/>. (Async and parameterized commands come in a later phase.)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>Name of a <c>bool</c> method or property that gates execution; null = always executable.</summary>
    public string CanExecute { get; set; }

    /// <summary>Override the generated command property name (default = method name + "Command").</summary>
    public string Name { get; set; }
}
