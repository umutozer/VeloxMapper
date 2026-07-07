using System;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// ForMember ile tanımlanan tek bir üye (property) eşleştirme kuralını temsil eder.
/// Bir hedef property için ya özel kaynak ifadesi (MapFrom) ya da yok sayma (Ignore) belirlenir.
/// </summary>
public sealed class MemberMappingRule
{
    /// <summary>
    /// Hedef property adı. ForMember ifadesinden çözümlenir.
    /// </summary>
    public string DestinationMemberName { get; }

    /// <summary>
    /// Eğer true ise bu property mapping sırasında atlanır.
    /// </summary>
    /// <summary>
    /// Eğer true ise bu property mapping sırasında atlanır.
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    /// Eğer true ise bu property mapping sırasında atlanır ve doğrulama dışı bırakılır.
    /// </summary>
    public bool IsDoNotValidate { get; }

    /// <summary>
    /// Özel kaynak ifadesi (MapFrom ile belirtilmiş lambda).
    /// Null ise ignore kuralıdır.
    /// </summary>
    public LambdaExpression? MapFromExpression { get; }

    /// <summary>
    /// IVeloxValueResolver tipi (DI ile çözümlenecek).
    /// </summary>
    public Type? ResolverType { get; internal set; }

    /// <summary>
    /// IVeloxMemberValueResolver tipi.
    /// </summary>
    public Type? MemberValueResolverType { get; internal set; }

    /// <summary>
    /// MemberValueResolver için kaynak üye ifadesi.
    /// </summary>
    public LambdaExpression? SourceMemberForResolver { get; internal set; }

    /// <summary>
    /// IVeloxValueConverter örneği (DI gerektirmez, doğrudan kullanılabilir).
    /// </summary>
    public object? ValueConverter { get; internal set; }

    /// <summary>
    /// IVeloxValueConverter tipi (DI gerektirebilir).
    /// </summary>
    public Type? ValueConverterType { get; internal set; }

    /// <summary>
    /// ValueConverter için kaynak üye ifadesi.
    /// </summary>
    public LambdaExpression? ValueConverterSourceMember { get; internal set; }

    /// <summary>
    /// Condition koşul delegesi.
    /// </summary>
    public Delegate? ConditionDelegate { get; internal set; }

    /// <summary>
    /// PreCondition koşul delegesi.
    /// </summary>
    public Delegate? PreConditionDelegate { get; internal set; }

    /// <summary>
    /// NullSubstitute varsayılan değeri.
    /// </summary>
    public object? NullSubstituteValue { get; internal set; }

    /// <summary>
    /// NullSubstitute değeri tanımlı mı?
    /// </summary>
    public bool HasNullSubstitute { get; }

    /// <summary>
    /// Hedef değer korunsun mu? (UseDestinationValue ile belirtilmiş)
    /// </summary>
    public bool KeepDestinationValue { get; }

    /// <summary>
    /// Eşleştirme sırası (SetMappingOrder ile belirtilmiş). Varsayılan: 0.
    /// </summary>
    public int MappingOrder { get; }

    /// <summary>
    /// Yok sayma (Ignore) kuralı oluşturur.
    /// </summary>
    internal MemberMappingRule(string destinationMemberName)
    {
        DestinationMemberName = destinationMemberName
            ?? throw new ArgumentNullException(nameof(destinationMemberName));
        IsIgnored = true;
        IsDoNotValidate = false;
        MapFromExpression = null;
    }

    /// <summary>
    /// Özel kaynak eşleştirmesi (MapFrom) kuralı oluşturur.
    /// </summary>
    internal MemberMappingRule(string destinationMemberName, LambdaExpression mapFromExpression)
    {
        DestinationMemberName = destinationMemberName
            ?? throw new ArgumentNullException(nameof(destinationMemberName));
        MapFromExpression = mapFromExpression
            ?? throw new ArgumentNullException(nameof(mapFromExpression));
        IsIgnored = false;
        IsDoNotValidate = false;
    }

    /// <summary>
    /// Tüm parametreleri alan detaylı constructor.
    /// </summary>
    internal MemberMappingRule(
        string destinationMemberName,
        bool isIgnored,
        bool isDoNotValidate,
        LambdaExpression? mapFromExpression,
        Type? resolverType,
        Type? memberValueResolverType,
        LambdaExpression? sourceMemberForResolver,
        object? valueConverter,
        Type? valueConverterType,
        LambdaExpression? valueConverterSourceMember,
        Delegate? conditionDelegate,
        Delegate? preConditionDelegate,
        object? nullSubstituteValue,
        bool hasNullSubstitute,
        bool keepDestinationValue,
        int mappingOrder)
    {
        DestinationMemberName = destinationMemberName ?? throw new ArgumentNullException(nameof(destinationMemberName));
        IsIgnored = isIgnored;
        IsDoNotValidate = isDoNotValidate;
        MapFromExpression = mapFromExpression;
        ResolverType = resolverType;
        MemberValueResolverType = memberValueResolverType;
        SourceMemberForResolver = sourceMemberForResolver;
        ValueConverter = valueConverter;
        ValueConverterType = valueConverterType;
        ValueConverterSourceMember = valueConverterSourceMember;
        ConditionDelegate = conditionDelegate;
        PreConditionDelegate = preConditionDelegate;
        NullSubstituteValue = nullSubstituteValue;
        HasNullSubstitute = hasNullSubstitute;
        KeepDestinationValue = keepDestinationValue;
        MappingOrder = mappingOrder;
    }
}
