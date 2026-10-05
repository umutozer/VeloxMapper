using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="IMappingExpression{TSource,TDestination}"/> implementasyonu. Yapılandırma çağrılarını toplar ve
/// <see cref="Build"/> ile dondurulmuş <see cref="MappingRegistration"/> kayıtlarına çevirir.
/// </summary>
internal sealed class MappingExpression<TSource, TDestination>
    : IMappingExpression<TSource, TDestination>, IMappingExpression, IMappingExpressionBuilder
{
    private readonly Dictionary<string, MemberMappingRule> _memberRules = new(StringComparer.Ordinal);
    private readonly List<ForPathRule> _forPathRules = new();
    private readonly List<CtorParamRule> _ctorParamRules = new();
    private readonly List<Action<MemberConfigurationExpression<TSource, TDestination, object>>> _forAllMembers = new();
    private readonly List<Action<MemberConfigurationExpression<TSource, TDestination, object>>> _forAllOtherMembers = new();
    private readonly HashSet<string> _sourceMembersNotValidated = new(StringComparer.Ordinal);
    private readonly List<object> _beforeMapActions = new();
    private readonly List<object> _afterMapActions = new();
    private readonly List<(Type, Type)> _includedDerivedTypes = new();
    private readonly List<LambdaExpression> _includeMembers = new();
    private readonly List<LambdaExpression> _valueTransformers = new();

    private object? _typeConverter;
    private Type? _typeConverterType;
    private LambdaExpression? _convertUsingExpression;
    private Delegate? _convertUsingFunc;
    private EnumMappingSnapshot? _enumMapping;
    private Func<TSource, TDestination>? _constructUsing;
    private Func<TSource, ResolutionContext, TDestination>? _constructUsingWithContext;
    private bool _constructUsingServiceLocator;
    private int _maxDepth;
    private bool _preserveReferences;
    private (Type, Type)? _includeBase;
    private bool _includeAllDerived;
    private Type? _redirectType;
    private MemberList _memberList;
    private bool _disableCtorValidation;
    private bool _ignoreSourceWithInaccessibleGetter;
    private MappingExpression<TDestination, TSource>? _reverse;

    public MappingExpression() : this(MemberList.Destination)
    {
    }

    internal MappingExpression(MemberList memberList)
    {
        _memberList = memberList;
    }

    // ─── IMappingExpressionBuilder ──────────────────────────────────────────

    public Type SourceType => typeof(TSource);

    public Type DestinationType => typeof(TDestination);

    public IMappingExpression AsNonGeneric => this;

    // ─── Üye yapılandırması ─────────────────────────────────────────────────

    public IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
    {
        if (destinationMember == null) throw new ArgumentNullException(nameof(destinationMember));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        var path = MemberChain.GetPath(destinationMember);
        if (path.Count != 1)
        {
            throw new ArgumentException(
                $"ForMember yalnızca hedef türün doğrudan üyelerini kabul eder ('{destinationMember}'). " +
                "İç içe hedef yolları için ForPath kullanın.", nameof(destinationMember));
        }

        var member = path[0];
        memberOptions(new MemberConfigurationExpression<TSource, TDestination, TMember>(GetOrCreateRule(member.Name), member));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForMember(
        string name,
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        var member = FindDestinationMember(name);
        memberOptions(new MemberConfigurationExpression<TSource, TDestination, object>(GetOrCreateRule(member.Name), member));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
    {
        if (destinationMember == null) throw new ArgumentNullException(nameof(destinationMember));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        var path = MemberChain.GetPath(destinationMember);
        if (path.Count == 1)
        {
            return ForMember(destinationMember, memberOptions);
        }

        var segments = path.Select(m => m.Name).ToArray();
        var key = string.Join(".", segments);
        var existing = _forPathRules.FirstOrDefault(r => string.Join(".", r.PathSegments) == key);
        var rule = existing?.MemberRule ?? new MemberMappingRule(segments[segments.Length - 1]);

        memberOptions(new MemberConfigurationExpression<TSource, TDestination, TMember>(rule, path[path.Count - 1]));

        if (existing == null) _forPathRules.Add(new ForPathRule(segments, rule));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForSourceMember(
        Expression<Func<TSource, object?>> sourceMember,
        Action<ISourceMemberConfigurationExpression> memberOptions)
    {
        if (sourceMember == null) throw new ArgumentNullException(nameof(sourceMember));
        var path = MemberChain.GetPath(sourceMember);
        return ForSourceMember(path[0].Name, memberOptions);
    }

    public IMappingExpression<TSource, TDestination> ForSourceMember(
        string sourceMemberName,
        Action<ISourceMemberConfigurationExpression> memberOptions)
    {
        if (string.IsNullOrWhiteSpace(sourceMemberName)) throw new ArgumentNullException(nameof(sourceMemberName));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        var cfg = new SourceMemberConfigurationExpression();
        memberOptions(cfg);
        if (cfg.Skip) _sourceMembersNotValidated.Add(sourceMemberName);
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForCtorParam(
        string ctorParamName,
        Action<ICtorParamConfigurationExpression<TSource>> paramOptions)
    {
        if (string.IsNullOrWhiteSpace(ctorParamName)) throw new ArgumentNullException(nameof(ctorParamName));
        if (paramOptions == null) throw new ArgumentNullException(nameof(paramOptions));

        var cfg = new CtorParamConfigurationExpression<TSource>();
        paramOptions(cfg);

        _ctorParamRules.RemoveAll(r => string.Equals(r.ParameterName, ctorParamName, StringComparison.OrdinalIgnoreCase));
        if (cfg.MapFromExpression != null) _ctorParamRules.Add(new CtorParamRule(ctorParamName, cfg.MapFromExpression));
        else if (cfg.MapFromFunc != null) _ctorParamRules.Add(new CtorParamRule(ctorParamName, cfg.MapFromFunc));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForAllMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
    {
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        _forAllMembers.Add(memberOptions);
        return this;
    }

    public IMappingExpression<TSource, TDestination> ForAllOtherMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
    {
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        _forAllOtherMembers.Add(memberOptions);
        return this;
    }

    public IMappingExpression<TSource, TDestination> Ignore(Expression<Func<TDestination, object?>> destinationMember)
        => ForMember(destinationMember, o => o.Ignore());

    public IMappingExpression<TSource, TDestination> AddTransform<TValue>(Expression<Func<TValue, TValue>> transformer)
    {
        _valueTransformers.Add(transformer ?? throw new ArgumentNullException(nameof(transformer)));
        return this;
    }

    // ─── Tür dönüştürme ─────────────────────────────────────────────────────

    public IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> mappingExpression)
    {
        ClearConverters();
        _convertUsingExpression = mappingExpression ?? throw new ArgumentNullException(nameof(mappingExpression));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, TDestination> mappingFunction)
    {
        ClearConverters();
        _convertUsingFunc = mappingFunction ?? throw new ArgumentNullException(nameof(mappingFunction));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, ResolutionContext, TDestination> mappingFunction)
    {
        ClearConverters();
        _convertUsingFunc = mappingFunction ?? throw new ArgumentNullException(nameof(mappingFunction));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsing(ITypeConverter<TSource, TDestination> converter)
    {
        ClearConverters();
        _typeConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsing(IVeloxTypeConverter<TSource, TDestination> converter)
    {
        ClearConverters();
        _typeConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsing<TTypeConverter>() => ConvertUsing(typeof(TTypeConverter));

    public IMappingExpression<TSource, TDestination> ConvertUsing(Type typeConverterType)
    {
        if (typeConverterType == null) throw new ArgumentNullException(nameof(typeConverterType));
        ExtensibilityTypes.EnsureImplements(typeConverterType, ExtensibilityTypes.TypeConverters, "ConvertUsing<TTypeConverter>()");
        ClearConverters();
        _typeConverterType = typeConverterType;
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConvertUsingEnumMapping(Action<EnumMappingExpression<TSource, TDestination>> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var expression = new EnumMappingExpression<TSource, TDestination>();
        configure(expression);
        var snapshot = expression.ToSnapshot();

        ClearConverters();
        _enumMapping = snapshot;
        _convertUsingFunc = new Func<TSource, TDestination, TDestination>((src, _) => src == null ? default! : (TDestination)snapshot.Convert(src));
        return this;
    }

    private void ClearConverters()
    {
        _typeConverter = null;
        _typeConverterType = null;
        _convertUsingExpression = null;
        _convertUsingFunc = null;
        _enumMapping = null;
    }

    // ─── Nesne oluşturma ────────────────────────────────────────────────────

    public IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> ctor)
    {
        _constructUsing = ctor ?? throw new ArgumentNullException(nameof(ctor));
        _constructUsingWithContext = null;
        _constructUsingServiceLocator = false;
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, ResolutionContext, TDestination> ctor)
    {
        _constructUsingWithContext = ctor ?? throw new ArgumentNullException(nameof(ctor));
        _constructUsing = null;
        _constructUsingServiceLocator = false;
        return this;
    }

    public IMappingExpression<TSource, TDestination> ConstructUsingServiceLocator()
    {
        _constructUsingServiceLocator = true;
        _constructUsing = null;
        _constructUsingWithContext = null;
        return this;
    }

    // ─── Before / After ─────────────────────────────────────────────────────

    public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> beforeFunction)
    {
        _beforeMapActions.Add(beforeFunction ?? throw new ArgumentNullException(nameof(beforeFunction)));
        return this;
    }

    public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> beforeFunction)
    {
        _beforeMapActions.Add(beforeFunction ?? throw new ArgumentNullException(nameof(beforeFunction)));
        return this;
    }

    public IMappingExpression<TSource, TDestination> BeforeMap<TMappingAction>()
    {
        ExtensibilityTypes.EnsureImplements(typeof(TMappingAction), ExtensibilityTypes.MappingActions, "BeforeMap<TMappingAction>()");
        _beforeMapActions.Add(typeof(TMappingAction));
        return this;
    }

    public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> afterFunction)
    {
        _afterMapActions.Add(afterFunction ?? throw new ArgumentNullException(nameof(afterFunction)));
        return this;
    }

    public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> afterFunction)
    {
        _afterMapActions.Add(afterFunction ?? throw new ArgumentNullException(nameof(afterFunction)));
        return this;
    }

    public IMappingExpression<TSource, TDestination> AfterMap<TMappingAction>()
    {
        ExtensibilityTypes.EnsureImplements(typeof(TMappingAction), ExtensibilityTypes.MappingActions, "AfterMap<TMappingAction>()");
        _afterMapActions.Add(typeof(TMappingAction));
        return this;
    }

    // ─── Kalıtım ────────────────────────────────────────────────────────────

    public IMappingExpression<TSource, TDestination> Include<TOtherSource, TOtherDestination>()
        where TOtherSource : TSource
        where TOtherDestination : TDestination
        => Include(typeof(TOtherSource), typeof(TOtherDestination));

    public IMappingExpression<TSource, TDestination> Include(Type derivedSourceType, Type derivedDestinationType)
    {
        if (derivedSourceType == null) throw new ArgumentNullException(nameof(derivedSourceType));
        if (derivedDestinationType == null) throw new ArgumentNullException(nameof(derivedDestinationType));
        if (!_includedDerivedTypes.Contains((derivedSourceType, derivedDestinationType)))
            _includedDerivedTypes.Add((derivedSourceType, derivedDestinationType));
        return this;
    }

    public IMappingExpression<TSource, TDestination> IncludeBase<TSourceBase, TDestinationBase>()
        => IncludeBase(typeof(TSourceBase), typeof(TDestinationBase));

    public IMappingExpression<TSource, TDestination> IncludeBase(Type sourceBase, Type destinationBase)
    {
        if (sourceBase == null) throw new ArgumentNullException(nameof(sourceBase));
        if (destinationBase == null) throw new ArgumentNullException(nameof(destinationBase));
        _includeBase = (sourceBase, destinationBase);
        return this;
    }

    public IMappingExpression<TSource, TDestination> IncludeAllDerived()
    {
        _includeAllDerived = true;
        return this;
    }

    public IMappingExpression<TSource, TDestination> IncludeMembers(params Expression<Func<TSource, object?>>[] memberExpressions)
    {
        if (memberExpressions == null) throw new ArgumentNullException(nameof(memberExpressions));
        foreach (var expression in memberExpressions)
        {
            if (expression != null) _includeMembers.Add(expression);
        }

        return this;
    }

    public IMappingExpression<TSource, TDestination> As<TOtherDestination>() where TOtherDestination : TDestination
    {
        _redirectType = typeof(TOtherDestination);
        return this;
    }

    // ─── Diğer ──────────────────────────────────────────────────────────────

    public IMappingExpression<TDestination, TSource> ReverseMap()
        => _reverse ??= new MappingExpression<TDestination, TSource>(MemberList.None);

    public IMappingExpression<TSource, TDestination> MaxDepth(int depth)
    {
        if (depth < 0) throw new ArgumentOutOfRangeException(nameof(depth));
        _maxDepth = depth;
        return this;
    }

    public IMappingExpression<TSource, TDestination> PreserveReferences()
    {
        _preserveReferences = true;
        return this;
    }

    public IMappingExpression<TSource, TDestination> ValidateMemberList(MemberList memberList)
    {
        _memberList = memberList;
        return this;
    }

    public IMappingExpression<TSource, TDestination> DisableCtorValidation()
    {
        _disableCtorValidation = true;
        return this;
    }

    public IMappingExpression<TSource, TDestination> IgnoreAllPropertiesWithAnInaccessibleSetter()
    {
        foreach (var property in typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var setter = property.GetSetMethod(nonPublic: true);
            if (setter != null && !setter.IsPublic && !_memberRules.ContainsKey(property.Name))
            {
                GetOrCreateRule(property.Name).IsIgnored = true;
            }
        }

        return this;
    }

    public IMappingExpression<TSource, TDestination> IgnoreAllSourcePropertiesWithAnInaccessibleSetter()
    {
        _ignoreSourceWithInaccessibleGetter = true;
        return this;
    }

    // ─── Non-generic görünüm ────────────────────────────────────────────────

    IMappingExpression IMappingExpression.ReverseMap() => (IMappingExpression)ReverseMap();

    IMappingExpression IMappingExpression.ForMember(string name, Action<IMemberConfigurationExpression> memberOptions)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        var member = FindDestinationMember(name);
        memberOptions(new MemberConfigurationExpression(GetOrCreateRule(member.Name), member));
        return this;
    }

    IMappingExpression IMappingExpression.ForAllMembers(Action<IMemberConfigurationExpression> memberOptions)
    {
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        _forAllMembers.Add(cfg => memberOptions(new MemberConfigurationExpression(cfg.Rule, cfg.DestinationMember)));
        return this;
    }

    IMappingExpression IMappingExpression.ForAllOtherMembers(Action<IMemberConfigurationExpression> memberOptions)
    {
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        _forAllOtherMembers.Add(cfg => memberOptions(new MemberConfigurationExpression(cfg.Rule, cfg.DestinationMember)));
        return this;
    }

    IMappingExpression IMappingExpression.ForSourceMember(string sourceMemberName, Action<ISourceMemberConfigurationExpression> memberOptions)
    {
        ForSourceMember(sourceMemberName, memberOptions);
        return this;
    }

    IMappingExpression IMappingExpression.ConvertUsing(Type typeConverterType)
    {
        ConvertUsing(typeConverterType);
        return this;
    }

    IMappingExpression IMappingExpression.Include(Type derivedSourceType, Type derivedDestinationType)
    {
        Include(derivedSourceType, derivedDestinationType);
        return this;
    }

    IMappingExpression IMappingExpression.IncludeBase(Type sourceBase, Type destinationBase)
    {
        IncludeBase(sourceBase, destinationBase);
        return this;
    }

    IMappingExpression IMappingExpression.IncludeAllDerived()
    {
        IncludeAllDerived();
        return this;
    }

    IMappingExpression IMappingExpression.As(Type typeOverride)
    {
        if (typeOverride == null) throw new ArgumentNullException(nameof(typeOverride));
        if (!typeof(TDestination).IsAssignableFrom(typeOverride))
            throw new ArgumentException($"'{typeOverride.FullName}' türü '{typeof(TDestination).FullName}' türünden türemelidir.", nameof(typeOverride));
        _redirectType = typeOverride;
        return this;
    }

    IMappingExpression IMappingExpression.MaxDepth(int depth)
    {
        MaxDepth(depth);
        return this;
    }

    IMappingExpression IMappingExpression.PreserveReferences()
    {
        PreserveReferences();
        return this;
    }

    IMappingExpression IMappingExpression.ValidateMemberList(MemberList memberList)
    {
        ValidateMemberList(memberList);
        return this;
    }

    IMappingExpression IMappingExpression.DisableCtorValidation()
    {
        DisableCtorValidation();
        return this;
    }

    IMappingExpression IMappingExpression.IgnoreAllPropertiesWithAnInaccessibleSetter()
    {
        IgnoreAllPropertiesWithAnInaccessibleSetter();
        return this;
    }

    IMappingExpression IMappingExpression.IgnoreAllSourcePropertiesWithAnInaccessibleSetter()
    {
        IgnoreAllSourcePropertiesWithAnInaccessibleSetter();
        return this;
    }

    // ─── Build ──────────────────────────────────────────────────────────────

    public IEnumerable<MappingRegistration> Build(string? profileName)
    {
        var registration = BuildRegistration(profileName);
        yield return registration;

        if (_reverse != null)
        {
            yield return ReverseMapBuilder.Build(registration, _reverse.BuildRegistration(profileName));
        }
    }

    internal MappingRegistration BuildRegistration(string? profileName)
    {
        var registration = new MappingRegistration(typeof(TSource), typeof(TDestination), profileName);
        var rules = registration.MutableMemberRules;

        foreach (var kvp in _memberRules)
        {
            rules[kvp.Key] = kvp.Value.Clone();
        }

        var forPathRules = _forPathRules.Select(r => new ForPathRule(r.PathSegments, r.MemberRule.Clone())).ToList();

        if (_forAllMembers.Count > 0 || _forAllOtherMembers.Count > 0)
        {
            var explicitlyConfigured = new HashSet<string>(rules.Keys, StringComparer.Ordinal);
            foreach (var pathRule in forPathRules) explicitlyConfigured.Add(pathRule.PathSegments[0]);

            foreach (var member in DestinationMembers.GetWritable(typeof(TDestination)))
            {
                if (!explicitlyConfigured.Contains(member.Name))
                {
                    foreach (var action in _forAllOtherMembers) ApplyBulk(rules, member, action);
                }

                foreach (var action in _forAllMembers) ApplyBulk(rules, member, action);
            }
        }

        // Non-generic yapılandırmadan gelen ad tabanlı yolları gerçek kaynak türüne göre çöz
        foreach (var rule in rules.Values.Concat(forPathRules.Select(r => r.MemberRule)))
        {
            ResolveNamedSources(rule);
        }

        registration.ForPathRules = forPathRules;
        registration.CtorParamRules = _ctorParamRules.ToList();
        registration.CustomConverter = _typeConverter;
        registration.CustomConverterType = _typeConverterType;
        registration.ConvertUsingExpression = _convertUsingExpression;
        registration.ConvertUsingFunc = _convertUsingFunc;
        registration.EnumMapping = _enumMapping;

        if (_constructUsing != null)
        {
            var factory = _constructUsing;
            registration.FactoryDelegate = src => factory((TSource)src)!;
        }

        if (_constructUsingWithContext != null)
        {
            var factory = _constructUsingWithContext;
            registration.FactoryDelegateWithContext = new Func<object, ResolutionContext, object>((src, ctx) => factory((TSource)src, ctx)!);
        }

        registration.ConstructUsingServiceLocator = _constructUsingServiceLocator;
        registration.BeforeMapActions = _beforeMapActions.ToList();
        registration.AfterMapActions = _afterMapActions.ToList();
        registration.MaxDepth = _maxDepth;
        registration.PreserveReferences = _preserveReferences;
        foreach (var included in _includedDerivedTypes) registration.AddIncludedDerivedType(included.Item1, included.Item2);
        registration.BaseTypeMapping = _includeBase;
        registration.IsIncludeAllDerivedRequested = _includeAllDerived;
        registration.IncludeMembers = _includeMembers.Count > 0 ? _includeMembers.ToList() : null;
        registration.RedirectDestinationType = _redirectType;
        registration.MemberList = _memberList;
        registration.CtorValidationDisabled = _disableCtorValidation;
        registration.SourceMembersNotValidated = _sourceMembersNotValidated.ToList();
        registration.IgnoreSourceMembersWithInaccessibleGetter = _ignoreSourceWithInaccessibleGetter;
        registration.ValueTransformers = _valueTransformers.ToList();
        return registration;
    }

    private static void ApplyBulk(
        Dictionary<string, MemberMappingRule> rules,
        MemberInfo member,
        Action<MemberConfigurationExpression<TSource, TDestination, object>> action)
    {
        if (!rules.TryGetValue(member.Name, out var rule))
        {
            rule = new MemberMappingRule(member.Name);
            rules[member.Name] = rule;
        }

        action(new MemberConfigurationExpression<TSource, TDestination, object>(rule, member));
    }

    private static void ResolveNamedSources(MemberMappingRule rule)
    {
        if (rule.SourceMemberPath != null && rule.MapFromExpression == null)
        {
            rule.MapFromExpression = MemberPath.BuildAccessor(typeof(TSource), rule.SourceMemberPath);
            rule.SourceMemberPath = null;
        }

        if (rule.SourceMemberNameForResolver != null && rule.SourceMemberForResolver == null)
        {
            rule.SourceMemberForResolver = MemberPath.BuildAccessor(typeof(TSource), rule.SourceMemberNameForResolver);
            rule.SourceMemberNameForResolver = null;
        }

        if (rule.ValueConverterSourceMemberName != null && rule.ValueConverterSourceMember == null)
        {
            rule.ValueConverterSourceMember = MemberPath.BuildAccessor(typeof(TSource), rule.ValueConverterSourceMemberName);
            rule.ValueConverterSourceMemberName = null;
        }
    }

    private MemberMappingRule GetOrCreateRule(string memberName)
    {
        if (!_memberRules.TryGetValue(memberName, out var rule))
        {
            rule = new MemberMappingRule(memberName);
            _memberRules[memberName] = rule;
        }

        return rule;
    }

    private static MemberInfo FindDestinationMember(string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return (MemberInfo?)typeof(TDestination).GetProperty(name, flags)
            ?? (MemberInfo?)typeof(TDestination).GetField(name, flags)
            ?? throw new ArgumentException($"'{typeof(TDestination).FullName}' türünde '{name}' adında bir hedef üye bulunamadı.", nameof(name));
    }

    private sealed class SourceMemberConfigurationExpression : ISourceMemberConfigurationExpression
    {
        public bool Skip { get; private set; }
        public void DoNotValidate() => Skip = true;
        public void Ignore() => Skip = true;
    }
}

/// <summary>
/// Open generic tür çiftleri için eşleştirme ifadesi (<c>CreateMap(typeof(Page&lt;&gt;), typeof(PageDto&lt;&gt;))</c>).
/// Kurallar kapatılmış türe uygulanırken çözülür.
/// </summary>
internal sealed class OpenGenericMappingExpression : IMappingExpression, IMappingExpressionBuilder
{
    private readonly Dictionary<string, MemberMappingRule> _memberRules = new(StringComparer.Ordinal);
    private readonly List<Action<MemberConfigurationExpression>> _forAllMembers = new();
    private readonly List<Action<MemberConfigurationExpression>> _forAllOtherMembers = new();
    private readonly HashSet<string> _sourceMembersNotValidated = new(StringComparer.Ordinal);
    private readonly List<(Type, Type)> _includedDerivedTypes = new();
    private Type? _typeConverterType;
    private (Type, Type)? _includeBase;
    private bool _includeAllDerived;
    private Type? _redirectType;
    private int _maxDepth;
    private bool _preserveReferences;
    private MemberList _memberList;
    private bool _disableCtorValidation;
    private bool _ignoreSourceWithInaccessibleGetter;
    private OpenGenericMappingExpression? _reverse;

    public OpenGenericMappingExpression(Type sourceType, Type destinationType, MemberList memberList = MemberList.Destination)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
        _memberList = memberList;
    }

    public Type SourceType { get; }

    public Type DestinationType { get; }

    public IMappingExpression AsNonGeneric => this;

    public IMappingExpression ReverseMap() => _reverse ??= new OpenGenericMappingExpression(DestinationType, SourceType, MemberList.None);

    public IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        if (!_memberRules.TryGetValue(name, out var rule))
        {
            rule = new MemberMappingRule(name);
            _memberRules[name] = rule;
        }

        memberOptions(new MemberConfigurationExpression(rule));
        return this;
    }

    public IMappingExpression ForAllMembers(Action<IMemberConfigurationExpression> memberOptions)
    {
        _forAllMembers.Add(memberOptions ?? throw new ArgumentNullException(nameof(memberOptions)));
        return this;
    }

    public IMappingExpression ForAllOtherMembers(Action<IMemberConfigurationExpression> memberOptions)
    {
        _forAllOtherMembers.Add(memberOptions ?? throw new ArgumentNullException(nameof(memberOptions)));
        return this;
    }

    public IMappingExpression ForSourceMember(string sourceMemberName, Action<ISourceMemberConfigurationExpression> memberOptions)
    {
        if (string.IsNullOrWhiteSpace(sourceMemberName)) throw new ArgumentNullException(nameof(sourceMemberName));
        _sourceMembersNotValidated.Add(sourceMemberName);
        return this;
    }

    public IMappingExpression ConvertUsing(Type typeConverterType)
    {
        _typeConverterType = typeConverterType ?? throw new ArgumentNullException(nameof(typeConverterType));
        ExtensibilityTypes.EnsureImplements(typeConverterType, ExtensibilityTypes.TypeConverters, "ConvertUsing(Type)");
        return this;
    }

    public IMappingExpression Include(Type derivedSourceType, Type derivedDestinationType)
    {
        _includedDerivedTypes.Add((derivedSourceType, derivedDestinationType));
        return this;
    }

    public IMappingExpression IncludeBase(Type sourceBase, Type destinationBase)
    {
        _includeBase = (sourceBase, destinationBase);
        return this;
    }

    public IMappingExpression IncludeAllDerived()
    {
        _includeAllDerived = true;
        return this;
    }

    public IMappingExpression As(Type typeOverride)
    {
        _redirectType = typeOverride ?? throw new ArgumentNullException(nameof(typeOverride));
        return this;
    }

    public IMappingExpression MaxDepth(int depth)
    {
        _maxDepth = depth;
        return this;
    }

    public IMappingExpression PreserveReferences()
    {
        _preserveReferences = true;
        return this;
    }

    public IMappingExpression ValidateMemberList(MemberList memberList)
    {
        _memberList = memberList;
        return this;
    }

    public IMappingExpression DisableCtorValidation()
    {
        _disableCtorValidation = true;
        return this;
    }

    public IMappingExpression IgnoreAllPropertiesWithAnInaccessibleSetter() => this;

    public IMappingExpression IgnoreAllSourcePropertiesWithAnInaccessibleSetter()
    {
        _ignoreSourceWithInaccessibleGetter = true;
        return this;
    }

    public IEnumerable<MappingRegistration> Build(string? profileName)
    {
        yield return BuildRegistration(profileName);
        if (_reverse != null) yield return _reverse.BuildRegistration(profileName);
    }

    private MappingRegistration BuildRegistration(string? profileName)
    {
        var registration = new MappingRegistration(SourceType, DestinationType, profileName)
        {
            CustomConverterType = _typeConverterType,
            BaseTypeMapping = _includeBase,
            IsIncludeAllDerivedRequested = _includeAllDerived,
            RedirectDestinationType = _redirectType,
            MaxDepth = _maxDepth,
            PreserveReferences = _preserveReferences,
            MemberList = _memberList,
            CtorValidationDisabled = _disableCtorValidation,
            SourceMembersNotValidated = _sourceMembersNotValidated.ToList(),
            IgnoreSourceMembersWithInaccessibleGetter = _ignoreSourceWithInaccessibleGetter,
        };

        foreach (var kvp in _memberRules) registration.MutableMemberRules[kvp.Key] = kvp.Value.Clone();
        foreach (var included in _includedDerivedTypes) registration.AddIncludedDerivedType(included.Item1, included.Item2);
        registration.OpenGenericForAllMembers = _forAllMembers.ToList();
        registration.OpenGenericForAllOtherMembers = _forAllOtherMembers.ToList();
        return registration;
    }
}

/// <summary>
/// Hedef türün yazılabilir üyelerini (setter'ı olan property'ler ve readonly olmayan public field'lar) listeler.
/// </summary>
internal static class DestinationMembers
{
    internal static IEnumerable<MemberInfo> GetWritable(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0 && property.CanWrite && ProfileMap.DefaultShouldMapProperty(property))
                yield return property;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.IsInitOnly) yield return field;
        }
    }
}

/// <summary>
/// <c>d =&gt; d.A.B</c> biçimindeki üye erişim zincirlerini çözen yardımcı.
/// </summary>
internal static class MemberChain
{
    /// <summary>
    /// Lambda gövdesindeki üye zincirini kökten yaprağa doğru döndürür. Geçersiz ifadede hata fırlatır.
    /// </summary>
    internal static IReadOnlyList<MemberInfo> GetPath(LambdaExpression expression)
        => TryGetPath(expression.Body, expression.Parameters[0])
           ?? throw new ArgumentException($"İfade bir üye erişimi olmalıdır (ör. d =&gt; d.Property). Geçersiz ifade: {expression}", nameof(expression));

    /// <summary>
    /// İfade yalnızca parametreden başlayan property/field erişimlerinden oluşuyorsa zinciri, aksi halde <c>null</c> döndürür.
    /// </summary>
    internal static IReadOnlyList<MemberInfo>? TryGetPath(Expression body, ParameterExpression parameter)
    {
        while (body is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            body = unary.Operand;

        var members = new List<MemberInfo>();
        var current = body;
        while (current is MemberExpression member && (member.Member is PropertyInfo || member.Member is FieldInfo))
        {
            members.Add(member.Member);
            current = member.Expression;
        }

        if (current != parameter || members.Count == 0) return null;
        members.Reverse();
        return members;
    }
}
