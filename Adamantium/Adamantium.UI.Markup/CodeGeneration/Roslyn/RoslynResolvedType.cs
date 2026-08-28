using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynResolvedType : IResolvedType
{
    private ITypeSymbol _symbol;
    
    public RoslynResolvedType(ITypeSymbol symbol)
    {
        _symbol = symbol;
    }

    /// <summary>The underlying Roslyn symbol — used by tooling (go-to-definition) to read source/metadata locations.</summary>
    public ITypeSymbol Symbol => _symbol;

    public string Name => _symbol.Name;
    public string Namespace => _symbol.ContainingNamespace.ToDisplayString();

    public string AssemblyName => _symbol.ContainingAssembly.Name;

    public string FullName => _symbol.ToDisplayString();
    
    public bool IsNamedType => _symbol is INamedTypeSymbol;

    public bool IsGenericType => _symbol is INamedTypeSymbol { IsGenericType: true };
    
    public IEnumerable<IResolvedType> TypeArguments
    {
        get
        {
            if (_symbol is INamedTypeSymbol named)
                return named.TypeArguments.Select(t => new RoslynResolvedType(t));
            return [];
        }
    }

    public IResolvedType BaseType
    {
        get
        {
            if (_symbol is INamedTypeSymbol { BaseType: not null } named)
                return new RoslynResolvedType(named.BaseType);
            return null;
        }
    }

    public IEnumerable<IResolvedMember> Members
    {
        get
        {
            if (_symbol is INamedTypeSymbol named)
                return named.GetMembers()
                    .Where(m => m.Kind is SymbolKind.Property or SymbolKind.Field or SymbolKind.Method)
                    .Select(m => new RoslynResolvedMember(m, this));
            return [];
        }
    }
    
    public bool IsAssignableTo(string fullName)
    {
        var current = _symbol;
        while (current != null)
        {
            if (current.ToDisplayString() == fullName)
                return true;
            current = (current as INamedTypeSymbol)?.BaseType;
        }
        return false;
    }
    
    public bool InheritsFrom(string baseTypeName)
    {
        var baseType = _symbol.BaseType;
        while (baseType != null)
        {
            if (baseType.ToDisplayString() == baseTypeName)
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    public bool HasAttribute(string attributeName)
    {
        var result = _symbol.GetAttributes().Any(attr => (attr.AttributeClass.ToDisplayString().EndsWith(attributeName)));
        return result;
    }

    public IResolvedAttribute GetAttribute(string attributeName)
    {
        var attribute = _symbol.GetAttributes().FirstOrDefault(x=>x.AttributeClass?.ToDisplayString() == attributeName);
        return attribute != null ? new RoslynResolvedAttribute(attribute) : null;
    }
    
    public IEnumerable<IResolvedAttribute> GetAttributes()
    {
        return _symbol.GetAttributes()
            .Select(attr => new RoslynResolvedAttribute(attr));
    }
    
    public IResolvedMember GetMemberByName(string name)
    {
        if (_symbol == null)
        {
            return null;
        }

        var @base = _symbol;
        while (@base != null)
        {
            var members = @base.GetMembers(name).ToList();
            if (members.Count > 0)
            {
                var data = members[0];
                return new RoslynResolvedMember(data, this);
            }
            @base = @base.BaseType;
        }

        return null;
    }

    public bool ImplementsInterface(string interfaceName)
    {
        var @interface = _symbol.AllInterfaces.FirstOrDefault(x => x.Name == interfaceName);
        return @interface != null;
    }

    public bool IsCollection()
    {
        return ImplementsInterface("ICollection") || ImplementsInterface("IList");
    }

    public IResolvedType GetInterface(string interfaceName)
    {
        var @interface = _symbol.AllInterfaces.FirstOrDefault(x => x.Name == interfaceName);
        return new RoslynResolvedType(@interface);
    }

    public bool InheritsFromMarkupExtension(string fullyQualifiedName)
    {
        var type = _symbol;
        while (type.BaseType is not null)
        {
            type = type.BaseType;
            if (type.ToDisplayString() == fullyQualifiedName)
            {
                return true;
            }
        }

        return false;
    }
    
    public List<IResolvedProperty> GetAllProperties()
    {
        if (_symbol == null)
        {
            return null;
        }

        var properties = new List<IResolvedProperty>();
        var @base = _symbol;
        while (@base != null)
        {
            var list = @base.GetMembers().OfType<IPropertySymbol>().ToList();
            foreach (var symbol in list)
            {
                properties.Add(new RoslynResolvedProperty(symbol));
            }
            @base = @base.BaseType;
        }

        return properties;
    }

    public bool FindPropertyWithAttribute(string attributeFullName, out IResolvedProperty property)
    {
        property = GetAllProperties().FirstOrDefault(x=>x.HasAttribute(attributeFullName));
        return property != null;
    }

    public ResolvedSpecialType SpecialType =>
        _symbol.SpecialType switch
        {
            Microsoft.CodeAnalysis.SpecialType.None => ResolvedSpecialType.None,
            Microsoft.CodeAnalysis.SpecialType.System_Double => ResolvedSpecialType.System_Double,
            Microsoft.CodeAnalysis.SpecialType.System_Int16 => ResolvedSpecialType.System_Int16,
            Microsoft.CodeAnalysis.SpecialType.System_Int32 => ResolvedSpecialType.System_Int32,
            Microsoft.CodeAnalysis.SpecialType.System_Int64 => ResolvedSpecialType.System_Int64,
            Microsoft.CodeAnalysis.SpecialType.System_UInt16 => ResolvedSpecialType.System_UInt16,
            Microsoft.CodeAnalysis.SpecialType.System_UInt32 => ResolvedSpecialType.System_UInt32,
            Microsoft.CodeAnalysis.SpecialType.System_UInt64 => ResolvedSpecialType.System_UInt64,
            Microsoft.CodeAnalysis.SpecialType.System_Single => ResolvedSpecialType.System_Single,
            Microsoft.CodeAnalysis.SpecialType.System_Decimal => ResolvedSpecialType.System_Decimal,
            Microsoft.CodeAnalysis.SpecialType.System_SByte => ResolvedSpecialType.System_SByte,
            Microsoft.CodeAnalysis.SpecialType.System_Byte => ResolvedSpecialType.System_Byte,
            Microsoft.CodeAnalysis.SpecialType.System_String => ResolvedSpecialType.System_String,
            Microsoft.CodeAnalysis.SpecialType.System_Object => ResolvedSpecialType.System_Object,
            Microsoft.CodeAnalysis.SpecialType.System_Boolean => ResolvedSpecialType.System_Boolean,
            Microsoft.CodeAnalysis.SpecialType.System_Enum => ResolvedSpecialType.System_Enum,
            Microsoft.CodeAnalysis.SpecialType.System_Array => ResolvedSpecialType.System_Array,
        };

    public ResolvedTypeKind TypeKind =>
        _symbol.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Unknown => ResolvedTypeKind.Unknown,
            Microsoft.CodeAnalysis.TypeKind.Class => ResolvedTypeKind.Class,
            Microsoft.CodeAnalysis.TypeKind.Struct => ResolvedTypeKind.Struct,
            Microsoft.CodeAnalysis.TypeKind.Interface => ResolvedTypeKind.Interface,
            Microsoft.CodeAnalysis.TypeKind.Enum => ResolvedTypeKind.Enum,
        };
    
    public ResolvedMemberKind MemberKind =>
        _symbol.Kind switch
        {
            SymbolKind.Field => ResolvedMemberKind.Field,
            SymbolKind.Property => ResolvedMemberKind.Property,
            SymbolKind.Method => ResolvedMemberKind.Method,
            SymbolKind.Event => ResolvedMemberKind.Event,
            _ => ResolvedMemberKind.Unknown
        };

    public EntityType EntityType
    {
        get
        {
            if (_symbol == null)
            {
                return EntityType.Unknown;
            }

            if (ImplementsInterface("IWindow"))
            {
                return EntityType.Window;
            }
            else if (ImplementsInterface("IPage"))
            {
                return EntityType.Page;
            }
            else if (ImplementsInterface("IView"))
            {
                return EntityType.View;
            }
            else if (ImplementsInterface("IUIApplication"))
            {
                return EntityType.UIApplication;
            }
            else if (ImplementsInterface("ITheme"))
            {
                return EntityType.Theme;
            }
            else if (ImplementsInterface("IResourceDictionary"))
            {
                return EntityType.ResourceDictionary;
            }
            else if (ImplementsInterface("IStyleSet"))
            {
                return EntityType.StyleSet;
            }
            else if (ImplementsInterface("IThemeVariant"))
            {
                return EntityType.ThemeVariant;
            }

            return EntityType.Unknown;
        }
    }

    public override string ToString()
    {
        return $"{FullName}";
    }
}