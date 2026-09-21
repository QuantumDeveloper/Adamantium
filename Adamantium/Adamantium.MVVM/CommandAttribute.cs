using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put on a method to have the generator emit a lazy command property that invokes it. A <c>void</c> method
/// gets an <see cref="AdamantiumCommand"/> (<c>void Save()</c> → <c>public AdamantiumCommand SaveCommand</c>);
/// a <c>Task</c>-returning method gets an <see cref="AdamantiumAsyncCommand"/>, and may take a single
/// <c>CancellationToken</c> which the command supplies (and can <c>Cancel</c>). Set <see cref="CanExecute"/> to the
/// name of a <c>bool</c> method/property gating execution. Override the generated command name with <see cref="Name"/>.
/// (Typed-parameter commands come in a later increment.)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>Name of a <c>bool</c> method or property that gates execution; null = always executable.</summary>
    public string CanExecute { get; set; }

    /// <summary>Override the generated command property name (default = method name + "Command").</summary>
    public string Name { get; set; }
}
