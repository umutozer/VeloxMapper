using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="IMappingExpression{TSource,TDestination}"/> implementasyonu.
/// CreateMap çağrısından döner; ForMember, Ignore, ConvertUsing, ReverseMap,
/// ConstructUsing ve ForAllMembers yapılandırmalarını toplar.
/// Profil veya konfigürasyon oluşturulurken <c>Build</c> metodu ile
/// <see cref="MappingRegistration"/> üretir.
/// </summary>
internal sealed class MappingExpression<TSource, TDestination>
    : IMappingExpression<TSource, TDestination>
{
    // ForMember ile toplanan kurallar — anahtar: hedef property adı
    private readonly Dictionary<string, MemberMappingRule> _memberRules = new();

    // ConvertUsing ile atanmış özel dönüştürücü
    private object? _customConverter;

    // Yönlendirilecek hedef tür (As<T> desteği için)
    private Type? _redirectDestinationType;

    // ReverseMap() çağrıldı mı?
    private bool _reverseMapRequested;

    // ConstructUsing factory delegate
    private Func<TSource, TDestination>? _constructUsing;

    // ForAllMembers condition: strongly-typed → erased delegate'e box et
    private Func<TSource, TDestination, object?, bool>? _forAllMembersCondition;

    // ForAllOtherMembers callback'i
    private Action<IMemberConfigurationExpression<TSource, TDestination, object>>? _forAllOtherMembersAction;

    // ForAllMembers().Ignore() çağrıldı mı?
    private bool _forAllMembersIgnored;

    // BeforeMap / AfterMap eylemleri (Type veya Action<TSource, TDestination>)
    private readonly List<object> _beforeMapActions = new();
    private readonly List<object> _afterMapActions = new();

    // MaxDepth ve PreserveReferences alanları
    private int _maxDepth;
    private bool _preserveReferences;

    // Include ve IncludeBase alanları
    private readonly List<(Type DerivedSource, Type DerivedDestination)> _includedDerivedTypes = new();
    private (Type BaseSource, Type BaseDestination)? _baseTypeMapping;
    private bool _includeAllDerivedRequested;

    // ForPath ve ForCtorParam kuralları
    private readonly List<ForPathRule> _forPathRules = new();
    private readonly List<CtorParamRule> _ctorParamRules = new();

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> BeforeMap<TAction>()
        where TAction : IVeloxMappingAction<TSource, TDestination>
    {
        _beforeMapActions.Add(typeof(TAction));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _beforeMapActions.Add(action);
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> AfterMap<TAction>()
        where TAction : IVeloxMappingAction<TSource, TDestination>
    {
        _afterMapActions.Add(typeof(TAction));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _afterMapActions.Add(action);
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
    {
        if (destinationMember == null) throw new ArgumentNullException(nameof(destinationMember));
        if (memberOptions == null)     throw new ArgumentNullException(nameof(memberOptions));

        // Hedef property adını expression'dan çıkar
        var memberName = ExtractMemberName(destinationMember);

        // Builder oluştur ve kullanıcı callback'ini çağır
        var config = new MemberConfigurationExpression<TSource, TDestination, TMember>();
        memberOptions(config);

        // Sonucu kaydet
        var rule = new MemberMappingRule(
            memberName,
            config.IsIgnored,
            config.IsDoNotValidate,
            config.MapFromExpression,
            config.ResolverType,
            config.MemberValueResolverType,
            config.SourceMemberForResolver,
            config.ValueConverter,
            config.ValueConverterType,
            config.ValueConverterSourceMember,
            config.ConditionDelegate,
            config.PreConditionDelegate,
            config.NullSubstituteValue,
            config.HasNullSubstitute,
            config.KeepDestinationValue,
            config.MappingOrder
        );

        _memberRules[memberName] = rule;

        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ConvertUsing(
        IVeloxTypeConverter<TSource, TDestination> converter)
    {
        _customConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ConvertUsing(
        Func<TSource?, TDestination> mappingFunction)
    {
        if (mappingFunction == null)
        {
            throw new ArgumentNullException(nameof(mappingFunction)); // Dönüşüm fonksiyonu null olamaz.
        }
        _customConverter = new FuncTypeConverterAdapter<TSource, TDestination>(mappingFunction);
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> Ignore(
        Expression<Func<TDestination, object?>> destinationMember)
    {
        if (destinationMember == null) throw new ArgumentNullException(nameof(destinationMember));

        var memberName = ExtractMemberName(destinationMember);
        _memberRules[memberName] = new MemberMappingRule(memberName);
        return this;
    }

    // ─── AutoMapper Geçiş Uyumluluk Metotları ────────────────────────────────

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ReverseMap()
    {
        _reverseMapRequested = true;
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ConstructUsing(
        Func<TSource, TDestination> factory)
    {
        _constructUsing = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ForAllMembers(
        Action<IForAllMembersExpression<TSource, TDestination>> memberOptions)
    {
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));

        // Toplu kural toplayıcısını oluştur ve callback'i çalıştır
        var expr = new ForAllMembersExpression();
        memberOptions(expr);

        _forAllMembersCondition = expr.ConditionPredicate;
        _forAllMembersIgnored   = expr.IsIgnored;
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ForAllOtherMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
    {
        _forAllOtherMembersAction = memberOptions ?? throw new ArgumentNullException(nameof(memberOptions));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> MaxDepth(int depth)
    {
        _maxDepth = depth;
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> PreserveReferences()
    {
        _preserveReferences = true;
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination
    {
        _includedDerivedTypes.Add((typeof(TDerivedSource), typeof(TDerivedDestination)));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
    {
        _baseTypeMapping = (typeof(TBaseSource), typeof(TBaseDestination));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> IncludeAllDerived()
    {
        _includeAllDerivedRequested = true;
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> As<TDestinationRedirect>()
        where TDestinationRedirect : TDestination
    {
        _redirectDestinationType = typeof(TDestinationRedirect);
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationPath,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
    {
        if (destinationPath == null) throw new ArgumentNullException(nameof(destinationPath));
        if (memberOptions == null)     throw new ArgumentNullException(nameof(memberOptions));

        var segments = ExtractPathSegments(destinationPath);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Yol en az bir segment içermelidir.", nameof(destinationPath));
        }

        var config = new MemberConfigurationExpression<TSource, TDestination, TMember>();
        memberOptions(config);

        var memberRule = new MemberMappingRule(
            segments[segments.Length - 1],
            config.IsIgnored,
            config.IsDoNotValidate,
            config.MapFromExpression,
            config.ResolverType,
            config.MemberValueResolverType,
            config.SourceMemberForResolver,
            config.ValueConverter,
            config.ValueConverterType,
            config.ValueConverterSourceMember,
            config.ConditionDelegate,
            config.PreConditionDelegate,
            config.NullSubstituteValue,
            config.HasNullSubstitute,
            config.KeepDestinationValue,
            config.MappingOrder
        );

        _forPathRules.Add(new ForPathRule(segments, memberRule));
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ForCtorParam(
        string ctorParamName,
        Action<ICtorParamConfigurationExpression<TSource>> paramOptions)
    {
        if (string.IsNullOrWhiteSpace(ctorParamName))
            throw new ArgumentNullException(nameof(ctorParamName));
        if (paramOptions == null)
            throw new ArgumentNullException(nameof(paramOptions));

        var config = new CtorParamConfigurationExpression<TSource>();
        paramOptions(config);

        if (config.MapFromExpression != null)
        {
            _ctorParamRules.Add(new CtorParamRule(ctorParamName, config.MapFromExpression));
        }
        return this;
    }

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> ConvertUsingEnumMapping(
        Action<EnumMappingExpression<TSource, TDestination>> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var expr = new EnumMappingExpression<TSource, TDestination>();
        configure(expr);

        Func<TSource, TDestination> enumConverter = src =>
        {
            if (src == null) return default!;

            if (expr.ValueOverrides.TryGetValue(src, out var overrideVal))
            {
                return overrideVal;
            }

            if (expr.MapByNameRequested)
            {
                var srcName = src.ToString();
                if (srcName != null)
                {
                    try
                    {
                        return (TDestination)Enum.Parse(typeof(TDestination), srcName, true);
                    }
                    catch
                    {
                        // Eşleşen isim bulunamazsa fallback
                    }
                }
            }

            return (TDestination)(object)src;
        };

        return ConvertUsing(src => enumConverter(src!));
    }

    // ─── Build ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Toplanan tüm yapılandırmayı <see cref="MappingRegistration"/> olarak dondurur.
    /// VeloxProfile ve MapperConfiguration tarafından çağrılır.
    /// </summary>
    internal MappingRegistration Build(string? profileName)
    {
        if (_forAllOtherMembersAction != null)
        {
            var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var destFields = typeof(TDestination).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var prop in destProps)
            {
                if (!_memberRules.ContainsKey(prop.Name))
                {
                    var config = new MemberConfigurationExpression<TSource, TDestination, object>();
                    _forAllOtherMembersAction(config);

                    var rule = new MemberMappingRule(
                        prop.Name,
                        config.IsIgnored,
                        config.IsDoNotValidate,
                        config.MapFromExpression,
                        config.ResolverType,
                        config.MemberValueResolverType,
                        config.SourceMemberForResolver,
                        config.ValueConverter,
                        config.ValueConverterType,
                        config.ValueConverterSourceMember,
                        config.ConditionDelegate,
                        config.PreConditionDelegate,
                        config.NullSubstituteValue,
                        config.HasNullSubstitute,
                        config.KeepDestinationValue,
                        config.MappingOrder
                    );
                    _memberRules[prop.Name] = rule;
                }
            }

            foreach (var field in destFields)
            {
                if (!field.IsInitOnly && !_memberRules.ContainsKey(field.Name))
                {
                    var config = new MemberConfigurationExpression<TSource, TDestination, object>();
                    _forAllOtherMembersAction(config);

                    var rule = new MemberMappingRule(
                        field.Name,
                        config.IsIgnored,
                        config.IsDoNotValidate,
                        config.MapFromExpression,
                        config.ResolverType,
                        config.MemberValueResolverType,
                        config.SourceMemberForResolver,
                        config.ValueConverter,
                        config.ValueConverterType,
                        config.ValueConverterSourceMember,
                        config.ConditionDelegate,
                        config.PreConditionDelegate,
                        config.NullSubstituteValue,
                        config.HasNullSubstitute,
                        config.KeepDestinationValue,
                        config.MappingOrder
                    );
                    _memberRules[field.Name] = rule;
                }
            }
        }

        // ConstructUsing: strongly-typed factory → object→object delegate'e box et
        Func<object, object>? factoryDelegate = null;
        if (_constructUsing != null)
        {
            var capturedFactory = _constructUsing;
            factoryDelegate = src => capturedFactory((TSource)src)!;
        }

        // ForAllMembers condition: strongly-typed → erased delegate'e box et
        Func<object, object, object?, bool>? condition = null;
        if (_forAllMembersCondition != null)
        {
            var capturedCondition = _forAllMembersCondition;
            condition = (src, dest, val) =>
                capturedCondition((TSource)src, (TDestination)dest, val);
        }

        return new MappingRegistration(
            typeof(TSource),
            typeof(TDestination),
            new Dictionary<string, MemberMappingRule>(_memberRules),
            _customConverter,
            profileName,
            factoryDelegate,
            condition,
            _forAllMembersIgnored,
            _reverseMapRequested,
            _beforeMapActions,
            _afterMapActions,
            _maxDepth,
            _preserveReferences,
            _includedDerivedTypes,
            _baseTypeMapping,
            _forPathRules,
            _ctorParamRules,
            _includeAllDerivedRequested,
            _redirectDestinationType);
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lambda ifadesinden property adını çıkarır.
    /// dest =&gt; dest.FullName → "FullName"
    /// </summary>
    private static string ExtractMemberName<T>(Expression<T> expression)
    {
        // Olası yapılar:
        // 1. dest => dest.Property              (MemberExpression)
        // 2. dest => (object)dest.Property      (UnaryExpression wrapping — boxing)
        var body = expression.Body;

        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is MemberExpression member && member.Member is PropertyInfo)
            return member.Member.Name;

        throw new ArgumentException(
            $"İfade bir property erişimi olmalıdır (dest => dest.PropertyName). " +
            $"Geçersiz ifade: {expression}",
            nameof(expression));
    }

    /// <summary>
    /// Lambda ifadesinden property yolunu segmentler halinde çıkarır.
    /// dest =&gt; dest.Customer.Address.City → ["Customer", "Address", "City"]
    /// </summary>
    private static string[] ExtractPathSegments<T>(Expression<T> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        var segments = new List<string>();
        var current = body;

        while (current is MemberExpression member)
        {
            if (member.Member is PropertyInfo || member.Member is FieldInfo)
            {
                segments.Add(member.Member.Name);
                current = member.Expression;
            }
            else
            {
                break;
            }
        }

        if (current is ParameterExpression)
        {
            segments.Reverse();
            return segments.ToArray();
        }

        throw new ArgumentException(
            $"İfade geçerli bir property yolu olmalıdır (dest => dest.Nested.PropertyName). " +
            $"Geçersiz ifade: {expression}",
            nameof(expression));
    }

    // ─── İç Sınıf: ForAllMembers Toplayıcısı ─────────────────────────────────

    /// <summary>
    /// ForAllMembers() callback'inin alacağı nesne.
    /// Condition ve Ignore kurallarını toplar.
    /// </summary>
    private sealed class ForAllMembersExpression
        : IForAllMembersExpression<TSource, TDestination>
    {
        /// <summary>Kullanıcı tarafından verilen koşul. Null ise koşul yok.</summary>
        internal Func<TSource, TDestination, object?, bool>? ConditionPredicate { get; private set; }

        /// <summary>Ignore() çağrıldıysa true.</summary>
        internal bool IsIgnored { get; private set; }

        /// <inheritdoc />
        public void Condition(Func<TSource, TDestination, object?, bool> predicate)
        {
            ConditionPredicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <inheritdoc />
        public void Ignore()
        {
            IsIgnored = true;
        }
    }
}

/// <summary>
/// Açık generic (Open Generic) eşleştirmeler için kullanılan ve generic parametre içermeyen
/// IMappingExpression implementasyonu.
/// </summary>
internal sealed class OpenGenericMappingExpression : IMappingExpression
{
    /// <summary>Kaynak türü</summary>
    public Type SourceType { get; }

    /// <summary>Hedef türü</summary>
    public Type DestinationType { get; }

    /// <summary>
    /// OpenGenericMappingExpression için yeni bir örnek oluşturur.
    /// </summary>
    public OpenGenericMappingExpression(Type sourceType, Type destinationType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType)); // Kaynak tür null olamaz.
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType)); // Hedef tür null olamaz.
    }

    /// <summary>
    /// Eşleştirme kaydını dondurur.
    /// </summary>
    internal MappingRegistration Build(string? profileName)
    {
        return new MappingRegistration(
            SourceType,
            DestinationType,
            new Dictionary<string, MemberMappingRule>(),
            null,
            profileName,
            null,
            null,
            false,
            false,
            new List<object>(),
            new List<object>(),
            0,
            false,
            new List<(Type, Type)>(),
            null,
            new List<ForPathRule>(),
            new List<CtorParamRule>(),
            false
        );
    }
}

/// <summary>
/// Lambda dönüşüm fonksiyonunu IVeloxTypeConverter arayüzüne sarmalayan adaptör sınıfı.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
/// <typeparam name="TDestination">Hedef tür</typeparam>
internal sealed class FuncTypeConverterAdapter<TSource, TDestination> : IVeloxTypeConverter<TSource, TDestination>, IConstantConverter
{
    private readonly Func<TSource?, TDestination> _func;

    /// <summary>
    /// FuncTypeConverterAdapter için yeni bir örnek oluşturur.
    /// </summary>
    public FuncTypeConverterAdapter(Func<TSource?, TDestination> func)
    {
        _func = func ?? throw new ArgumentNullException(nameof(func)); // Dönüşüm fonksiyonu null olamaz.
    }

    /// <summary>
    /// Lambda ifadesini çalıştırarak dönüştürme işlemini gerçekleştirir.
    /// </summary>
    public TDestination Convert(TSource? source)
    {
        return _func(source);
    }
}
