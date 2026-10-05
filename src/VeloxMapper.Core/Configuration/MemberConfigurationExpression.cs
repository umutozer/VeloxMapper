using System;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="IMemberConfigurationExpression{TSource,TDestination,TMember}"/> implementasyonu.
/// Çağrıları doğrudan bir <see cref="MemberMappingRule"/> üzerine işler; aynı üye için yapılan ardışık
/// yapılandırmalar (ör. <c>ForMember</c> + <c>ForAllMembers</c>) aynı kural üzerinde birleşir.
/// </summary>
internal class MemberConfigurationExpression<TSource, TDestination, TMember>
    : IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    internal MemberConfigurationExpression(MemberMappingRule rule, MemberInfo? destinationMember = null)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        DestinationMember = destinationMember!;
    }

    /// <summary>Yapılandırılan kural.</summary>
    internal MemberMappingRule Rule { get; }

    /// <inheritdoc />
    public MemberInfo DestinationMember { get; }

    // ─── MapFrom ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> mapExpression)
    {
        if (mapExpression == null) throw new ArgumentNullException(nameof(mapExpression));
        Rule.ClearValueSource();
        Rule.MapFromExpression = mapExpression;
    }

    /// <inheritdoc />
    public void MapFrom(string sourceMembersPath)
    {
        if (string.IsNullOrWhiteSpace(sourceMembersPath)) throw new ArgumentNullException(nameof(sourceMembersPath));
        Rule.ClearValueSource();

        // Kaynak türü biliniyorsa yolu hemen doğrulayıp ifadeye çeviriyoruz; aksi halde (object) eşleştirme
        // oluşturulurken gerçek kaynak türüne göre çözülür.
        if (typeof(TSource) != typeof(object))
        {
            Rule.MapFromExpression = MemberPath.BuildAccessor(typeof(TSource), sourceMembersPath);
        }
        else
        {
            Rule.SourceMemberPath = sourceMembersPath;
        }
    }

    /// <inheritdoc />
    public void MapFrom<TResult>(Func<TSource, TDestination, TResult> mappingFunction) => SetMapFromFunc(mappingFunction, 2);

    /// <inheritdoc />
    public void MapFrom<TResult>(Func<TSource, TDestination, TMember, TResult> mappingFunction) => SetMapFromFunc(mappingFunction, 3);

    /// <inheritdoc />
    public void MapFrom<TResult>(Func<TSource, TDestination, TMember, ResolutionContext, TResult> mappingFunction) => SetMapFromFunc(mappingFunction, 4);

    private void SetMapFromFunc(Delegate mappingFunction, int arity)
    {
        if (mappingFunction == null) throw new ArgumentNullException(nameof(mappingFunction));
        Rule.ClearValueSource();
        Rule.MapFromFunc = mappingFunction;
        Rule.MapFromFuncArity = arity;
    }

    /// <inheritdoc />
    public void MapFrom<TValueResolver>() => MapFrom(typeof(TValueResolver));

    /// <inheritdoc />
    public void MapFrom(Type valueResolverType)
    {
        if (valueResolverType == null) throw new ArgumentNullException(nameof(valueResolverType));
        ExtensibilityTypes.EnsureImplements(valueResolverType, ExtensibilityTypes.ValueResolvers, "MapFrom<TValueResolver>()");
        Rule.ClearValueSource();
        Rule.ResolverType = valueResolverType;
    }

    /// <inheritdoc />
    public void MapFrom(IValueResolver<TSource, TDestination, TMember> valueResolver)
    {
        if (valueResolver == null) throw new ArgumentNullException(nameof(valueResolver));
        Rule.ClearValueSource();
        Rule.ResolverInstance = valueResolver;
    }

    /// <inheritdoc />
    public void MapFrom<TValueResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        ExtensibilityTypes.EnsureImplements(typeof(TValueResolver), ExtensibilityTypes.MemberValueResolvers, "MapFrom<TValueResolver, TSourceMember>()");
        Rule.ClearValueSource();
        Rule.MemberValueResolverType = typeof(TValueResolver);
        Rule.SourceMemberForResolver = sourceMember;
    }

    /// <inheritdoc />
    public void MapFrom<TValueResolver, TSourceMember>(string sourceMemberName)
    {
        if (string.IsNullOrWhiteSpace(sourceMemberName)) throw new ArgumentNullException(nameof(sourceMemberName));
        ExtensibilityTypes.EnsureImplements(typeof(TValueResolver), ExtensibilityTypes.MemberValueResolvers, "MapFrom<TValueResolver, TSourceMember>()");
        Rule.ClearValueSource();
        Rule.MemberValueResolverType = typeof(TValueResolver);
        if (typeof(TSource) != typeof(object))
            Rule.SourceMemberForResolver = MemberPath.BuildAccessor(typeof(TSource), sourceMemberName);
        else
            Rule.SourceMemberNameForResolver = sourceMemberName;
    }

    /// <inheritdoc />
    public void MapFrom<TSourceMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> valueResolver, Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (valueResolver == null) throw new ArgumentNullException(nameof(valueResolver));
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        Rule.ClearValueSource();
        Rule.MemberValueResolverInstance = valueResolver;
        Rule.SourceMemberForResolver = sourceMember;
    }

    // ─── ConvertUsing (value converter) ──────────────────────────────────────

    /// <inheritdoc />
    public void ConvertUsing<TValueConverter, TSourceMember>()
    {
        SetConverterType(typeof(TValueConverter));
    }

    /// <inheritdoc />
    public void ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        SetConverterType(typeof(TValueConverter));
        Rule.ValueConverterSourceMember = sourceMember;
    }

    /// <inheritdoc />
    public void ConvertUsing<TValueConverter, TSourceMember>(string sourceMemberName)
    {
        if (string.IsNullOrWhiteSpace(sourceMemberName)) throw new ArgumentNullException(nameof(sourceMemberName));
        SetConverterType(typeof(TValueConverter));
        SetConverterSourceName(sourceMemberName);
    }

    /// <inheritdoc />
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter)
    {
        SetConverterInstance(valueConverter);
    }

    /// <inheritdoc />
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        SetConverterInstance(valueConverter);
        Rule.ValueConverterSourceMember = sourceMember;
    }

    /// <inheritdoc />
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, string sourceMemberName)
    {
        if (string.IsNullOrWhiteSpace(sourceMemberName)) throw new ArgumentNullException(nameof(sourceMemberName));
        SetConverterInstance(valueConverter);
        SetConverterSourceName(sourceMemberName);
    }

    /// <inheritdoc />
    public void ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember, TMember> converter, Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        SetConverterInstance(converter);
        Rule.ValueConverterSourceMember = sourceMember;
    }

    private void SetConverterType(Type converterType)
    {
        ExtensibilityTypes.EnsureImplements(converterType, ExtensibilityTypes.ValueConverters, "ConvertUsing<TValueConverter, TSourceMember>()");
        Rule.ClearValueSource();
        Rule.ValueConverterType = converterType;
    }

    private void SetConverterInstance(object converter)
    {
        if (converter == null) throw new ArgumentNullException(nameof(converter));
        Rule.ClearValueSource();
        Rule.ValueConverter = converter;
    }

    private void SetConverterSourceName(string sourceMemberName)
    {
        if (typeof(TSource) != typeof(object))
            Rule.ValueConverterSourceMember = MemberPath.BuildAccessor(typeof(TSource), sourceMemberName);
        else
            Rule.ValueConverterSourceMemberName = sourceMemberName;
    }

    // ─── Ignore / doğrulama ──────────────────────────────────────────────────

    /// <inheritdoc />
    public void Ignore()
    {
        Rule.ClearValueSource();
        Rule.IsIgnored = true;
    }

    /// <inheritdoc />
    public void DoNotValidate()
    {
        Rule.IsDoNotValidate = true;
    }

    // ─── Koşullar ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Condition(Func<TSource, bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        Rule.ConditionDelegate = new Func<TSource, TDestination, TMember, bool>((s, _, _) => condition(s));
    }

    /// <inheritdoc />
    public void Condition(Func<TSource, TDestination, bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        Rule.ConditionDelegate = new Func<TSource, TDestination, TMember, bool>((s, d, _) => condition(s, d));
    }

    /// <inheritdoc />
    public void Condition(Func<TSource, TDestination, TMember, bool> condition)
        => Rule.ConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    /// <inheritdoc />
    public void Condition(Func<TSource, TDestination, TMember, TMember, bool> condition)
        => Rule.ConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    /// <inheritdoc />
    public void Condition(Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool> condition)
        => Rule.ConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    /// <inheritdoc />
    public void PreCondition(Func<TSource, bool> condition)
        => Rule.PreConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    /// <inheritdoc />
    public void PreCondition(Func<ResolutionContext, bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        Rule.PreConditionDelegate = new Func<TSource, ResolutionContext, bool>((_, ctx) => condition(ctx));
    }

    /// <inheritdoc />
    public void PreCondition(Func<TSource, ResolutionContext, bool> condition)
        => Rule.PreConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    /// <inheritdoc />
    public void PreCondition(Func<TSource, TDestination, ResolutionContext, bool> condition)
        => Rule.PreConditionDelegate = condition ?? throw new ArgumentNullException(nameof(condition));

    // ─── Diğer seçenekler ────────────────────────────────────────────────────

    /// <inheritdoc />
    public void NullSubstitute(object? nullSubstitute)
    {
        Rule.NullSubstituteValue = nullSubstitute;
        Rule.HasNullSubstitute = true;
    }

    /// <inheritdoc />
    public void UseDestinationValue() => Rule.UseDestinationValue = true;

    /// <inheritdoc />
    public void DoNotUseDestinationValue() => Rule.UseDestinationValue = false;

    /// <inheritdoc />
    public void SetMappingOrder(int mappingOrder) => Rule.MappingOrder = mappingOrder;

    /// <inheritdoc />
    public void AddTransform(Expression<Func<TMember, TMember>> transformer)
    {
        if (transformer == null) throw new ArgumentNullException(nameof(transformer));
        Rule.AddTransformer(transformer);
    }

    /// <inheritdoc />
    public void ExplicitExpansion() => Rule.ExplicitExpansion = true;

    /// <inheritdoc />
    public void AllowNull() => Rule.AllowNull = true;

    /// <inheritdoc />
    public void DoNotAllowNull() => Rule.AllowNull = false;

    /// <inheritdoc />
    public void MapAtRuntime()
    {
        // VeloxMapper eşleştirmeyi zaten çalışma zamanında üretir; AutoMapper uyumluluğu için kabul edilir.
    }
}

/// <summary>
/// Non-generic <see cref="IMemberConfigurationExpression"/> implementasyonu.
/// </summary>
internal sealed class MemberConfigurationExpression
    : MemberConfigurationExpression<object, object, object>, IMemberConfigurationExpression
{
    internal MemberConfigurationExpression(MemberMappingRule rule, MemberInfo? destinationMember = null)
        : base(rule, destinationMember)
    {
    }
}

/// <summary>
/// Noktalı üye yollarını (<c>"Address.City"</c>) lambda ifadelerine çeviren yardımcı.
/// </summary>
internal static class MemberPath
{
    /// <summary>
    /// <paramref name="sourceType"/> üzerinde <paramref name="path"/> yolunu okuyan <c>src =&gt; src.A.B</c> ifadesini üretir.
    /// </summary>
    internal static LambdaExpression BuildAccessor(Type sourceType, string path)
    {
        var param = Expression.Parameter(sourceType, "src");
        Expression body = param;
        foreach (var part in path.Split('.'))
        {
            var member = FindMember(body.Type, part)
                ?? throw new ArgumentException($"'{body.Type.FullName}' türünde '{part}' adında bir üye bulunamadı (yol: '{path}').", nameof(path));
            body = member is MethodInfo method ? Expression.Call(body, method) : Expression.MakeMemberAccess(body, member);
        }

        return Expression.Lambda(body, param);
    }

    private static MemberInfo? FindMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;
        return (MemberInfo?)type.GetProperty(name, flags)
            ?? (MemberInfo?)type.GetField(name, flags)
            ?? type.GetMethod(name, flags, null, Type.EmptyTypes, null);
    }
}
