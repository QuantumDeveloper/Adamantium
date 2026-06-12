using System.Reflection;

namespace Adamantium.UI.Markup.CodeGeneration.Reflection;

public class ReflectionResolvedMember : IResolvedMember
{
    private readonly MemberInfo _member;

    public ReflectionResolvedMember(MemberInfo member, IResolvedType declaringType)
    {
        _member = member;
        DeclaringType = declaringType;
    }

    public string Name => _member.Name;

    public IResolvedType MemberType => _member switch
    {
        PropertyInfo p => new ReflectionResolvedType(p.PropertyType),
        FieldInfo f => new ReflectionResolvedType(f.FieldType),
        MethodInfo m => new ReflectionResolvedType(m.ReturnType),
        EventInfo e => new ReflectionResolvedType(e.EventHandlerType),
        _ => null
    };

    public IResolvedType DeclaringType { get; }

    public bool HasAttribute(string attributeMetadataName) =>
        _member.GetCustomAttributes(false).Any(a => a.GetType().FullName == attributeMetadataName);

    public bool HasSetter() => _member switch
    {
        PropertyInfo p => p.SetMethod is { IsPublic: true },
        FieldInfo f => f is { IsInitOnly: false, IsPublic: true },
        _ => false
    };

    public ResolvedMemberKind MemberKind => _member.MemberType switch
    {
        MemberTypes.Field => ResolvedMemberKind.Field,
        MemberTypes.Property => ResolvedMemberKind.Property,
        MemberTypes.Method => ResolvedMemberKind.Method,
        MemberTypes.Event => ResolvedMemberKind.Event,
        _ => ResolvedMemberKind.Unknown
    };
}
