using System.Reflection;

namespace Adamantium.UI.Markup.CodeGeneration.Reflection;

public class ReflectionResolvedType : IResolvedType
{
    private readonly Type _type;

    public ReflectionResolvedType(Type type) => _type = type;

    /// <summary>The underlying runtime type (used by the instantiator to <c>Activator.CreateInstance</c>).</summary>
    public Type ClrType => _type;

    public string Name => _type.Name;
    public string Namespace => _type.Namespace ?? string.Empty;
    public string AssemblyName => _type.Assembly.GetName().Name;
    public string FullName => string.IsNullOrEmpty(_type.Namespace) ? _type.Name : $"{_type.Namespace}.{_type.Name}";

    public bool IsNamedType => true;
    public bool IsGenericType => _type.IsGenericType;

    public IEnumerable<IResolvedType> TypeArguments =>
        _type.GetGenericArguments().Select(t => (IResolvedType)new ReflectionResolvedType(t));

    public IResolvedType BaseType => _type.BaseType != null ? new ReflectionResolvedType(_type.BaseType) : null;

    public IEnumerable<IResolvedMember> Members =>
        _type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Field or MemberTypes.Method)
            .Select(m => (IResolvedMember)new ReflectionResolvedMember(m, this));

    public bool IsAssignableTo(string fullName)
    {
        for (var t = _type; t != null; t = t.BaseType)
            if (FullNameOf(t) == fullName) return true;
        return _type.GetInterfaces().Any(i => FullNameOf(i) == fullName);
    }

    public bool InheritsFrom(string baseTypeName)
    {
        for (var t = _type.BaseType; t != null; t = t.BaseType)
            if (FullNameOf(t) == baseTypeName) return true;
        return false;
    }

    public bool HasAttribute(string attributeName) =>
        _type.GetCustomAttributes(false).Any(a => a.GetType().FullName?.EndsWith(attributeName) ?? false);

    public IResolvedAttribute GetAttribute(string fullName)
    {
        var attr = _type.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().FullName == fullName) as Attribute;
        return attr != null ? new ReflectionResolvedAttribute(attr) : null;
    }

    public IEnumerable<IResolvedAttribute> GetAttributes() =>
        _type.GetCustomAttributes(false).OfType<Attribute>().Select(a => (IResolvedAttribute)new ReflectionResolvedAttribute(a));

    public IResolvedMember GetMemberByName(string memberName)
    {
        var member = _type.GetMember(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault();
        return member != null ? new ReflectionResolvedMember(member, this) : null;
    }

    public List<IResolvedProperty> GetAllProperties() =>
        _type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (IResolvedProperty)new ReflectionResolvedProperty(p)).ToList();

    public bool ImplementsInterface(string interfaceName) =>
        _type.GetInterfaces().Any(i => i.Name == interfaceName);

    public bool IsCollection() => ImplementsInterface("ICollection") || ImplementsInterface("IList");

    public IResolvedType GetInterface(string interfaceName)
    {
        var i = _type.GetInterfaces().FirstOrDefault(x => x.Name == interfaceName);
        return i != null ? new ReflectionResolvedType(i) : null;
    }

    public bool InheritsFromMarkupExtension(string fullyQualifiedName)
    {
        for (var t = _type.BaseType; t != null; t = t.BaseType)
            if (FullNameOf(t) == fullyQualifiedName) return true;
        return false;
    }

    public bool FindPropertyWithAttribute(string attributeFullName, out IResolvedProperty property)
    {
        property = GetAllProperties().FirstOrDefault(x => x.HasAttribute(attributeFullName));
        return property != null;
    }

    public ResolvedSpecialType SpecialType => _type switch
    {
        _ when _type == typeof(double) => ResolvedSpecialType.System_Double,
        _ when _type == typeof(short) => ResolvedSpecialType.System_Int16,
        _ when _type == typeof(int) => ResolvedSpecialType.System_Int32,
        _ when _type == typeof(long) => ResolvedSpecialType.System_Int64,
        _ when _type == typeof(ushort) => ResolvedSpecialType.System_UInt16,
        _ when _type == typeof(uint) => ResolvedSpecialType.System_UInt32,
        _ when _type == typeof(ulong) => ResolvedSpecialType.System_UInt64,
        _ when _type == typeof(float) => ResolvedSpecialType.System_Single,
        _ when _type == typeof(decimal) => ResolvedSpecialType.System_Decimal,
        _ when _type == typeof(sbyte) => ResolvedSpecialType.System_SByte,
        _ when _type == typeof(byte) => ResolvedSpecialType.System_Byte,
        _ when _type == typeof(string) => ResolvedSpecialType.System_String,
        _ when _type == typeof(object) => ResolvedSpecialType.System_Object,
        _ when _type == typeof(bool) => ResolvedSpecialType.System_Boolean,
        _ when _type.IsEnum => ResolvedSpecialType.System_Enum,
        _ when _type.IsArray => ResolvedSpecialType.System_Array,
        _ => ResolvedSpecialType.None
    };

    public ResolvedTypeKind TypeKind => _type switch
    {
        _ when _type.IsInterface => ResolvedTypeKind.Interface,
        _ when _type.IsEnum => ResolvedTypeKind.Enum,
        _ when _type.IsValueType => ResolvedTypeKind.Struct,
        _ when _type.IsClass => ResolvedTypeKind.Class,
        _ => ResolvedTypeKind.Unknown
    };

    public ResolvedMemberKind MemberKind => ResolvedMemberKind.Unknown;

    public EntityType EntityType
    {
        get
        {
            if (ImplementsInterface("IWindow")) return EntityType.Window;
            if (ImplementsInterface("IPage")) return EntityType.Page;
            if (ImplementsInterface("IView")) return EntityType.View;
            if (ImplementsInterface("IUIApplication")) return EntityType.UIApplication;
            if (ImplementsInterface("ITheme")) return EntityType.Theme;
            if (ImplementsInterface("IResourceDictionary")) return EntityType.ResourceDictionary;
            if (ImplementsInterface("IStyleSet")) return EntityType.StyleSet;
            return EntityType.Unknown;
        }
    }

    private static string FullNameOf(Type t) => string.IsNullOrEmpty(t.Namespace) ? t.Name : $"{t.Namespace}.{t.Name}";

    public override string ToString() => FullName;
}
