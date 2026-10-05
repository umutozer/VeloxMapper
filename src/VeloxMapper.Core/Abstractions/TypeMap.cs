using System;
using System.Reflection;

namespace VeloxMapper;

/// <summary>
/// <c>ForAllMaps</c> ve <c>ForAllPropertyMaps</c> çağrılarına aktarılan, bir eşleştirmenin özet bilgisi.
/// AutoMapper'ın <c>TypeMap</c> sınıfının yaygın kullanılan üyelerini sunar.
/// </summary>
public sealed class TypeMap
{
    internal TypeMap(Type sourceType, Type destinationType, string? profileName)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        ProfileName = profileName;
    }

    /// <summary>Kaynak tür.</summary>
    public Type SourceType { get; }

    /// <summary>Hedef tür.</summary>
    public Type DestinationType { get; }

    /// <summary>Eşleştirmenin tanımlandığı profil (inline tanımlar için <c>null</c>).</summary>
    public string? ProfileName { get; }

    /// <inheritdoc />
    public override string ToString() => $"{SourceType.Name} -> {DestinationType.Name}";
}

/// <summary>
/// <c>ForAllPropertyMaps</c> çağrısına aktarılan, tek bir hedef üyenin eşleştirme bilgisi.
/// </summary>
public sealed class PropertyMap
{
    internal PropertyMap(TypeMap typeMap, MemberInfo destinationMember, Type destinationType, MemberInfo? sourceMember)
    {
        TypeMap = typeMap;
        DestinationMember = destinationMember;
        DestinationType = destinationType;
        SourceMember = sourceMember;
    }

    /// <summary>Üyenin ait olduğu eşleştirme.</summary>
    public TypeMap TypeMap { get; }

    /// <summary>Hedef üye.</summary>
    public MemberInfo DestinationMember { get; }

    /// <summary>Hedef üyenin adı.</summary>
    public string DestinationName => DestinationMember.Name;

    /// <summary>Hedef üyenin türü.</summary>
    public Type DestinationType { get; }

    /// <summary>Konvansiyonla eşleşen kaynak üye (yoksa <c>null</c>).</summary>
    public MemberInfo? SourceMember { get; }

    /// <summary>Konvansiyonla eşleşen kaynak üyenin türü (yoksa <c>null</c>).</summary>
    public Type? SourceType => SourceMember switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        MethodInfo m => m.ReturnType,
        _ => null
    };
}
