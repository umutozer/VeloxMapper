using System;
using System.Collections.Generic;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Bir profilin (veya global yapılandırmanın) eşleştirme tanımlarını ve konvansiyon ayarlarını tutan iç depo.
/// <see cref="VeloxProfile"/> ve <c>VeloxMapperOptions</c> bu sınıfa delege eder.
/// </summary>
internal sealed class ProfileConfiguration
{
    internal ProfileConfiguration(string? name)
    {
        Name = name;
    }

    /// <summary>Profil adı (global yapılandırma için <c>null</c>).</summary>
    public string? Name { get; set; }

    public ICustomNamingConvention? SourceMemberNamingConvention { get; set; }
    public ICustomNamingConvention? DestinationMemberNamingConvention { get; set; }
    public List<string> Prefixes { get; } = new();
    public List<string> Postfixes { get; } = new();
    public List<string> DestinationPrefixes { get; } = new();
    public List<string> DestinationPostfixes { get; } = new();
    public bool PrefixesCleared { get; set; }
    public List<KeyValuePair<string, string>> MemberNameReplacers { get; } = new();
    public List<string> GlobalIgnores { get; } = new();
    public bool? AllowNullCollections { get; set; }
    public bool? AllowNullDestinationValues { get; set; }
    public bool? EnableNullPropagationForQueryMapping { get; set; }
    public Func<PropertyInfo, bool>? ShouldMapProperty { get; set; }
    public Func<FieldInfo, bool>? ShouldMapField { get; set; }
    public Func<MethodInfo, bool>? ShouldMapMethod { get; set; }
    public Func<ConstructorInfo, bool>? ShouldUseConstructor { get; set; }
    public bool ConstructorMappingDisabled { get; set; }
    public List<Type> SourceExtensionMethodTypes { get; } = new();
    public ValueTransformerCollection ValueTransformers { get; } = new();
    public List<Action<TypeMap, IMappingExpression>> ForAllMapsActions { get; } = new();
    public List<KeyValuePair<Func<PropertyMap, bool>, Action<PropertyMap, IMemberConfigurationExpression>>> ForAllPropertyMapsActions { get; } = new();

    /// <summary>Bu profilde tanımlanan eşleştirmeler.</summary>
    public List<IMappingExpressionBuilder> Maps { get; } = new();

    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList)
    {
        var expression = new MappingExpression<TSource, TDestination>();
        if (memberList != MemberList.Destination) expression.ValidateMemberList(memberList);
        Maps.Add(expression);
        return expression;
    }

    public IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        IMappingExpressionBuilder builder;
        if (sourceType.IsGenericTypeDefinition || destinationType.IsGenericTypeDefinition)
        {
            builder = new OpenGenericMappingExpression(sourceType, destinationType);
        }
        else
        {
            var expressionType = typeof(MappingExpression<,>).MakeGenericType(sourceType, destinationType);
            builder = (IMappingExpressionBuilder)Activator.CreateInstance(expressionType, nonPublic: true)!;
        }

        if (memberList != MemberList.Destination) builder.AsNonGeneric.ValidateMemberList(memberList);
        Maps.Add(builder);
        return builder.AsNonGeneric;
    }

    public void AddPrefixes(List<string> target, string[]? values)
    {
        if (values == null) return;
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value);
        }
    }
}

/// <summary>
/// Tüm eşleştirme ifadelerinin ortak iç sözleşmesi: tür bilgisi, non-generic görünüm ve kayıt üretimi.
/// </summary>
internal interface IMappingExpressionBuilder
{
    Type SourceType { get; }
    Type DestinationType { get; }
    IMappingExpression AsNonGeneric { get; }

    /// <summary>İfadeyi (ve varsa ReverseMap ile oluşturulan ters ifadeyi) dondurulmuş kayıtlara çevirir.</summary>
    IEnumerable<MappingRegistration> Build(string? profileName);
}
