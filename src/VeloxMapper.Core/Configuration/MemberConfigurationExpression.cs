using System;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="IMemberConfigurationExpression{TSource,TDestination,TMember}"/> implementasyonu.
/// ForMember callback'i içinde MapFrom veya Ignore çağrılarını kaydeder.
/// </summary>
internal sealed class MemberConfigurationExpression<TSource, TDestination, TMember>
    : IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    /// <summary>Ignore çağrıldı mı?</summary>
    internal bool IsIgnored { get; private set; }

    /// <summary>DoNotValidate çağrıldı mı?</summary>
    internal bool IsDoNotValidate { get; private set; }

    /// <summary>MapFrom ile belirtilen kaynak lambda ifadesi.</summary>
    internal LambdaExpression? MapFromExpression { get; private set; }

    /// <summary>IVeloxValueResolver tipi.</summary>
    internal Type? ResolverType { get; private set; }

    /// <summary>IVeloxMemberValueResolver tipi.</summary>
    internal Type? MemberValueResolverType { get; private set; }

    /// <summary>MemberValueResolver için kaynak üye ifadesi.</summary>
    internal LambdaExpression? SourceMemberForResolver { get; private set; }

    /// <summary>IVeloxValueConverter örneği.</summary>
    internal object? ValueConverter { get; private set; }

    /// <summary>IVeloxValueConverter tipi.</summary>
    internal Type? ValueConverterType { get; private set; }

    /// <summary>ValueConverter için kaynak üye ifadesi.</summary>
    internal LambdaExpression? ValueConverterSourceMember { get; private set; }

    /// <summary>Condition koşul delegesi.</summary>
    internal Delegate? ConditionDelegate { get; private set; }

    /// <summary>PreCondition koşul delegesi.</summary>
    internal Delegate? PreConditionDelegate { get; private set; }

    /// <summary>NullSubstitute varsayılan değeri.</summary>
    internal object? NullSubstituteValue { get; private set; }

    /// <summary>NullSubstitute değeri tanımlı mı?</summary>
    internal bool HasNullSubstitute { get; private set; }

    /// <summary>Hedef değer korunsun mu?</summary>
    internal bool KeepDestinationValue { get; private set; }

    /// <summary>Eşleştirme sırası</summary>
    internal int MappingOrder { get; private set; }

    /// <inheritdoc />
    public void MapFrom(Expression<Func<TSource, TMember>> sourceMember)
    {
        if (sourceMember == null)
            throw new ArgumentNullException(nameof(sourceMember));

        MapFromExpression = sourceMember;
        IsIgnored = false;
        IsDoNotValidate = false;
    }

    /// <inheritdoc />
    public void Ignore()
    {
        IsIgnored = true;
        IsDoNotValidate = false;
        MapFromExpression = null;
        ResolverType = null;
        MemberValueResolverType = null;
        SourceMemberForResolver = null;
        ValueConverter = null;
        ValueConverterType = null;
        ValueConverterSourceMember = null;
    }

    /// <inheritdoc />
    public void DoNotValidate()
    {
        IsIgnored = true;
        IsDoNotValidate = true;
        MapFromExpression = null;
        ResolverType = null;
        MemberValueResolverType = null;
        SourceMemberForResolver = null;
        ValueConverter = null;
        ValueConverterType = null;
        ValueConverterSourceMember = null;
    }

    /// <inheritdoc />
    public void MapFrom<TResolver>()
        where TResolver : class, IVeloxValueResolver<TSource, TDestination, TMember>
    {
        ResolverType = typeof(TResolver);
        IsIgnored = false;
        MapFromExpression = null;
    }

    /// <inheritdoc />
    public void MapFrom<TResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TResolver : class, IVeloxMemberValueResolver<TSource, TDestination, TSourceMember, TMember>
    {
        if (sourceMember == null)
            throw new ArgumentNullException(nameof(sourceMember));

        MemberValueResolverType = typeof(TResolver);
        SourceMemberForResolver = sourceMember;
        IsIgnored = false;
        MapFromExpression = null;
    }

    /// <inheritdoc />
    public void ConvertUsing<TConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TConverter : class, IVeloxValueConverter<TSourceMember, TMember>
    {
        if (sourceMember == null)
            throw new ArgumentNullException(nameof(sourceMember));

        ValueConverterType = typeof(TConverter);
        ValueConverterSourceMember = sourceMember;
        IsIgnored = false;
        MapFromExpression = null;
    }

    /// <inheritdoc />
    public void ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember, TMember> converter, Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));
        if (sourceMember == null)
            throw new ArgumentNullException(nameof(sourceMember));

        ValueConverter = converter;
        ValueConverterSourceMember = sourceMember;
        IsIgnored = false;
        MapFromExpression = null;
    }

    /// <inheritdoc />
    public void Condition(Func<TSource, TDestination, TMember, bool> predicate)
    {
        ConditionDelegate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <inheritdoc />
    public void PreCondition(Func<TSource, bool> predicate)
    {
        PreConditionDelegate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <inheritdoc />
    public void NullSubstitute(TMember substituteValue)
    {
        NullSubstituteValue = substituteValue;
        HasNullSubstitute = true;
    }

    /// <inheritdoc />
    public void UseDestinationValue()
    {
        KeepDestinationValue = true;
    }

    /// <inheritdoc />
    public void SetMappingOrder(int mappingOrder)
    {
        MappingOrder = mappingOrder;
    }
}
