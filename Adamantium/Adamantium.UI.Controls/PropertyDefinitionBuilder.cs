using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Controls;

/// <summary>Builds inspector definitions from a type by reflection: a property becomes the definition that fits it, and
/// <c>[Category]</c> becomes a section.
/// <para>A CLASS OF ITS OWN, not a method on the grid. An inspector that is written out by hand must not drag
/// reflection in with it, and an editor that builds its properties from metadata of its own must be able to replace
/// this wholesale.</para>
/// <para>What it makes are ordinary definitions carrying ordinary bindings - the same thing markup would have declared,
/// so nothing downstream can tell a generated inspector from a written one.</para>
/// <para>It knows nothing about any component system: it is handed objects and gives back sections. What an entity's
/// components are is the application's business.</para></summary>
public class PropertyDefinitionBuilder
{
    /// <summary>How deep a nested object is opened into properties of its own. One level by default: a transform inside
    /// a component is worth showing, a whole object graph is not - and a cycle would never end.</summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>Properties to leave out whatever their attributes say - an escape hatch for a type you do not own.</summary>
    public HashSet<string> Skip { get; } = new(StringComparer.Ordinal);

    /// <summary>One section per object, named by its type - which for an entity's components is one section per
    /// component, the shape every inspector of that kind has.</summary>
    public IReadOnlyList<PropertySection> BuildSections(IEnumerable<object> targets)
    {
        var sections = new List<PropertySection>();
        if (targets == null) return sections;

        foreach (var target in targets)
        {
            if (target == null) continue;

            var section = new PropertySection
            {
                Header = Caption(target.GetType().Name),
                Category = target.GetType().Name,
                Target = target,
                IsExpanded = true
            };

            foreach (var definition in Build(target.GetType())) section.Properties.Add(definition);
            sections.Add(section);
        }

        return sections;
    }

    /// <summary>The sections a single type asks for: one per <c>[Category]</c>, or one for everything when the type
    /// names no categories.</summary>
    public IReadOnlyList<PropertySection> BuildSections(Type type, object target = null)
    {
        var sections = new List<PropertySection>();
        if (type == null) return sections;

        foreach (var group in Properties(type).GroupBy(CategoryOf))
        {
            var section = new PropertySection
            {
                Header = Caption(group.Key ?? type.Name),
                Category = group.Key,
                Target = target,
                IsExpanded = true
            };

            foreach (var property in group)
            {
                if (DefinitionFor(property, 0) is { } definition) section.Properties.Add(definition);
            }

            if (section.Properties.Count > 0) sections.Add(section);
        }

        return sections;
    }

    /// <summary>The definitions a type asks for, flat - categories ignored.</summary>
    public IReadOnlyList<PropertyDefinition> Build(Type type)
    {
        var definitions = new List<PropertyDefinition>();
        if (type == null) return definitions;

        foreach (var property in Properties(type))
        {
            if (DefinitionFor(property, 0) is { } definition) definitions.Add(definition);
        }

        return definitions;
    }

    private IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.CanRead)
            .Where(p => !Skip.Contains(p.Name))
            .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false);

    private PropertyDefinition DefinitionFor(PropertyInfo property, int depth)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var definition = Create(property, type, depth);
        if (definition == null) return null;

        definition.Header = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? Caption(property.Name);
        definition.Binding = new Binding(property.Name);
        definition.Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

        // A property with no setter is read-only whatever anything else says - the inspector must not offer an edit
        // that cannot land. And one that ALREADY said it cannot be edited (an object shown as text because there was
        // no depth left to open it) keeps that: a writable property does not make a line that has no editor editable.
        definition.IsReadOnly |= !property.CanWrite
                                || property.SetMethod is not { IsPublic: true }
                                || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true;

        return definition;
    }

    private PropertyDefinition Create(PropertyInfo property, Type type, int depth)
    {
        if (type == typeof(string)) return new StringProperty();
        if (type == typeof(bool)) return new BooleanProperty();
        if (type.IsEnum) return new ChoiceProperty { EnumType = type };
        if (IsNumber(type)) return Number(type);

        // Anything else is an object: opened into lines of its own while there is depth left, and shown as plain text
        // when there is not. Text rather than nothing, because "Material: Steel" still tells the reader something.
        if (depth >= MaxDepth || type.IsPrimitive) return new StringProperty { IsReadOnly = true };

        var composite = new CompositeProperty();
        foreach (var nested in Properties(type))
        {
            if (DefinitionFor(nested, depth + 1) is not { } child) continue;

            // The child's binding reads the nested object, so its path has to be qualified by the property that holds
            // it - "Position" plus "X" is what reads "Position.X" off the target.
            if (child.Binding is Binding inner && inner.Path?.Path is { Length: > 0 } member)
                child.Binding = new Binding($"{property.Name}.{member}");

            composite.Children.Add(child);
        }

        return composite.Children.Count > 0 ? composite : new StringProperty { IsReadOnly = true };
    }

    private static NumericProperty Number(Type type)
    {
        var integral = type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
                       || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong)
                       || type == typeof(ushort);

        return new NumericProperty
        {
            Decimals = integral ? 0 : 3,
            Step = integral ? 1 : 0.1,
            Minimum = type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)
                ? 0
                : Double.MinValue
        };
    }

    private static bool IsNumber(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)
        || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    private static string CategoryOf(PropertyInfo property) => property.GetCustomAttribute<CategoryAttribute>()?.Category;

    // "IsEnabled" reads as "Is Enabled" in an inspector, the way every other one shows it.
    private static string Caption(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var text = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) text.Append(' ');
            text.Append(name[i]);
        }

        return text.ToString();
    }
}
