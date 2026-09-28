using System;

namespace Adamantium.MVVM;

/// <summary>Emits a lazy command property for the method: <see cref="AdamantiumCommand"/> for <c>void</c>,
/// <see cref="AdamantiumAsyncCommand"/> for <c>Task</c>. <see cref="CanExecute"/> names the gate.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>Name of a <c>bool</c> method or property that gates execution; null = always executable.</summary>
    public string CanExecute { get; set; }

    /// <summary>Override the generated command property name (default = method name + "Command").</summary>
    public string Name { get; set; }
}
