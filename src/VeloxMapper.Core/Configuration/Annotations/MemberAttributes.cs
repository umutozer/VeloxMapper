using System;

namespace VeloxMapper.Configuration.Annotations;

/// <summary>
/// <c>[AutoMap]</c> ile eşlenen bir hedef üyeyi yok sayar. AutoMapper'ın <c>[Ignore]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreAttribute : Attribute
{
}

/// <summary>
/// <c>[AutoMap]</c> ile eşlenen bir hedef üyenin değerini adı verilen kaynak üyeden alır. AutoMapper'ın <c>[SourceMember]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SourceMemberAttribute : Attribute
{
    /// <summary>Kaynak üye adını veya noktalı yolunu belirtir.</summary>
    /// <param name="name">Kaynak üye adı.</param>
    public SourceMemberAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Kaynak üye adı.</summary>
    public string Name { get; }
}

/// <summary>
/// Kaynak değer <c>null</c> ise hedefe yazılacak değeri belirler. AutoMapper'ın <c>[NullSubstitute]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NullSubstituteAttribute : Attribute
{
    /// <summary>Yerine geçecek değeri belirtir.</summary>
    /// <param name="value">Değer.</param>
    public NullSubstituteAttribute(object? value)
    {
        Value = value;
    }

    /// <summary>Yerine geçecek değer.</summary>
    public object? Value { get; }
}

/// <summary>
/// Hedef üyenin değerini bir value resolver ile üretir. AutoMapper'ın <c>[ValueResolver]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValueResolverAttribute : Attribute
{
    /// <summary>Resolver türünü belirtir.</summary>
    /// <param name="type"><c>IValueResolver&lt;,,&gt;</c> uygulayan tür.</param>
    public ValueResolverAttribute(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    /// <summary>Resolver türü.</summary>
    public Type Type { get; }
}

/// <summary>
/// Aynı adlı (veya <see cref="SourceMemberAttribute"/> ile belirtilen) kaynak üyeyi bir value converter ile dönüştürür.
/// AutoMapper'ın <c>[ValueConverter]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValueConverterAttribute : Attribute
{
    /// <summary>Converter türünü belirtir.</summary>
    /// <param name="type"><c>IValueConverter&lt;,&gt;</c> uygulayan tür.</param>
    public ValueConverterAttribute(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    /// <summary>Converter türü.</summary>
    public Type Type { get; }
}

/// <summary>
/// Hedef üyenin mevcut değerini korur (<c>UseDestinationValue()</c>). AutoMapper'ın <c>[UseExistingValue]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class UseExistingValueAttribute : Attribute
{
}

/// <summary>
/// Üyenin atanma sırasını belirler (<c>SetMappingOrder</c>). AutoMapper'ın <c>[MappingOrder]</c> özniteliği ile uyumludur.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MappingOrderAttribute : Attribute
{
    /// <summary>Sıra değerini belirtir.</summary>
    /// <param name="value">Sıra değeri; küçük değerler önce atanır.</param>
    public MappingOrderAttribute(int value)
    {
        Value = value;
    }

    /// <summary>Sıra değeri.</summary>
    public int Value { get; }
}

/// <summary>
/// AutoMapper'ın <c>[MapAtRuntime]</c> özniteliği ile uyumluluk için kabul edilir; VeloxMapper davranışını değiştirmez.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MapAtRuntimeAttribute : Attribute
{
}
