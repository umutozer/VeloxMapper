using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using VeloxMapper.Abstractions;
using VeloxMapper.Caching;
using VeloxMapper.Configuration;
using VeloxMapper.Diagnostics;
using VeloxMapper.Exceptions;
using VeloxMapper.Execution;

namespace VeloxMapper;

/// <summary>
/// Dondurulmuş (değişmez) eşleştirme yapılandırması. AutoMapper'ın <c>MapperConfiguration</c> sınıfı ile aynı kullanım biçimini sunar:
/// <code>
/// var config = new MapperConfiguration(cfg =&gt;
/// {
///     cfg.AddProfile&lt;OrderProfile&gt;();
///     cfg.CreateMap&lt;Customer, CustomerDto&gt;();
/// });
/// config.AssertConfigurationIsValid();
/// IMapper mapper = config.CreateMapper();
/// </code>
/// Derlenmiş eşleştirme delegeleri bu nesnede önbelleklenir ve tüm mapper örnekleri tarafından paylaşılır.
/// Uygulama ömrü boyunca tek örnek (singleton) olarak kullanılmalıdır.
/// </summary>
public sealed class MapperConfiguration : IConfigurationProvider
{
    private static readonly MethodInfo QueryableSelectMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    private readonly ConcurrentDictionary<(Type, Type), MappingRegistration> _registrations = new();
    private readonly List<MappingRegistration> _openGenericRegistrations = new();
    private readonly ConcurrentDictionary<(Type, Type), RegistrationLookup> _lookupCache = new();
    private readonly ConcurrentDictionary<(Type, Type, int), Lazy<Delegate>> _delegates = new();
    private readonly ConcurrentDictionary<(Type, Type, string), Lazy<LambdaExpression>> _projections = new();
    private readonly Dictionary<string, ProfileMap> _profiles = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(Type, Type), bool> _precompiledPairs = new();

    /// <summary>Tür çifti için Source Generator ile üretilmiş bir eşleştirme kayıtlıysa <c>true</c> (iç üyeler satır içi derlenmez).</summary>
    internal bool IsPrecompiled(Type sourceType, Type destinationType) => _precompiledPairs.ContainsKey((sourceType, destinationType));

    /// <summary>
    /// Yapılandırma delegesi ile oluşturur. AutoMapper'daki <c>new MapperConfiguration(cfg =&gt; ...)</c> ile aynıdır.
    /// </summary>
    /// <param name="configure">Yapılandırma.</param>
    public MapperConfiguration(Action<VeloxMapperOptions> configure)
        : this(configure, loggerFactory: null)
    {
    }

    /// <summary>
    /// Yapılandırma delegesi ve log fabrikası ile oluşturur (AutoMapper 15 imzası ile uyumlu). Log fabrikası verilirse
    /// ifade üretimi ve teşhis olayları <c>VeloxMapper</c> kategorisinde loglanır.
    /// </summary>
    /// <param name="configure">Yapılandırma.</param>
    /// <param name="loggerFactory">Log fabrikası (isteğe bağlı).</param>
    public MapperConfiguration(Action<VeloxMapperOptions> configure, ILoggerFactory? loggerFactory)
        : this(Configure(configure), loggerFactory)
    {
    }

    /// <summary>
    /// Önceden doldurulmuş bir yapılandırma ifadesinden oluşturur.
    /// </summary>
    /// <param name="options">Yapılandırma ifadesi.</param>
    public MapperConfiguration(VeloxMapperOptions options)
        : this(options, loggerFactory: null)
    {
    }

    /// <summary>
    /// Önceden doldurulmuş bir yapılandırma ifadesi ve log fabrikasıyla oluşturur.
    /// </summary>
    /// <param name="options">Yapılandırma ifadesi.</param>
    /// <param name="loggerFactory">Log fabrikası (isteğe bağlı).</param>
    public MapperConfiguration(VeloxMapperOptions options, ILoggerFactory? loggerFactory)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        IgnoreNullValues = options.PatchMapping.IgnoreNullValues;
        DiagnosticsSink = options.DiagnosticsSink ?? (loggerFactory != null ? new LoggerDiagnosticsSink(loggerFactory.CreateLogger("VeloxMapper")) : null);
        ServiceCtor = options.ServiceCtor;
        DefaultProfile = ProfileMap.Create(null, options.Global, options.NamingConvention);

        Initialize(options);
    }

    /// <summary>
    /// Verilen assembly'lerdeki profilleri ve <c>[AutoMap]</c> özniteliklerini tarayarak oluşturur.
    /// </summary>
    /// <param name="assemblies">Taranacak assembly'ler.</param>
    public MapperConfiguration(params Assembly[] assemblies)
        : this(cfg => cfg.AddMaps(assemblies ?? throw new ArgumentNullException(nameof(assemblies))))
    {
    }

    private static VeloxMapperOptions Configure(Action<VeloxMapperOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var options = new VeloxMapperOptions();
        configure(options);
        return options;
    }

    // ─── Ayarlar ────────────────────────────────────────────────────────────

    /// <summary>Mevcut nesneye eşlemede null kaynak değerlerinin atlanıp atlanmayacağı.</summary>
    public bool IgnoreNullValues { get; }

    /// <summary>Teşhis log hedefi.</summary>
    public IVeloxDiagnosticsSink? DiagnosticsSink { get; }

    /// <summary>Tür çifti kaydı sayısı (ters eşleştirmeler dahil, open generic tanımlar hariç).</summary>
    public int RegistrationCount => _registrations.Values.Count(r => !IsOpenGeneric(r));

    internal ProfileMap DefaultProfile { get; }

    internal Func<Type, object>? ServiceCtor { get; }

    internal ProfileMap GetProfile(MappingRegistration? registration) => registration?.Profile ?? DefaultProfile;

    /// <summary>Tüm eşleştirme kayıtları (open generic tanımlar hariç).</summary>
    public IReadOnlyCollection<TypeMap> GetAllTypeMaps()
        => _registrations.Values.Where(r => !IsOpenGeneric(r)).Select(r => new TypeMap(r.SourceType, r.DestinationType, r.ProfileName)).ToList();

    internal IEnumerable<MappingRegistration> GetAllRegistrations() => _registrations.Values.Where(r => !IsOpenGeneric(r));

    // ─── Başlatma ───────────────────────────────────────────────────────────

    private void Initialize(VeloxMapperOptions options)
    {
        var sources = new List<(ProfileConfiguration Configuration, string? Name, ProfileMap Map)>
        {
            (options.Global, null, DefaultProfile)
        };

        foreach (var profile in options.Profiles)
        {
            var map = ProfileMap.Create(profile.Configuration, options.Global, options.NamingConvention);
            _profiles[profile.ProfileName] = map;
            sources.Add((profile.Configuration, profile.ProfileName, map));
        }

        // 1. AutoMapper tarzı ForAllMaps: ifadeler kayda dönüşmeden önce uygulanır
        foreach (var (configuration, name, _) in sources)
        {
            foreach (var builder in configuration.Maps)
            {
                var typeMap = new TypeMap(builder.SourceType, builder.DestinationType, name);
                foreach (var action in options.Global.ForAllMapsActions) action(typeMap, builder.AsNonGeneric);
                if (!ReferenceEquals(configuration, options.Global))
                {
                    foreach (var action in configuration.ForAllMapsActions) action(typeMap, builder.AsNonGeneric);
                }
            }
        }

        // 2. Kayıtları üret (açık tanımlar ReverseMap ile üretilenlere üstün gelir)
        var generated = new List<MappingRegistration>();
        foreach (var (configuration, name, map) in sources)
        {
            foreach (var builder in configuration.Maps)
            {
                foreach (var registration in builder.Build(name))
                {
                    registration.Profile = map;
                    if (registration.IsGeneratedReverse)
                    {
                        generated.Add(registration);
                        continue;
                    }

                    AddRegistration(registration);
                }
            }
        }

        foreach (var registration in generated)
        {
            var key = (registration.SourceType, registration.DestinationType);
            if (!_registrations.ContainsKey(key)) AddRegistration(registration);
        }

        // 3. ForAllPropertyMaps
        foreach (var registration in _registrations.Values.ToList())
        {
            if (IsOpenGeneric(registration)) continue;
            var actions = options.Global.ForAllPropertyMapsActions.AsEnumerable();
            var profileConfiguration = options.Profiles.FirstOrDefault(p => p.ProfileName == registration.ProfileName)?.Configuration;
            if (profileConfiguration != null) actions = actions.Concat(profileConfiguration.ForAllPropertyMapsActions);
            var actionList = actions.ToList();
            if (actionList.Count > 0) ApplyPropertyMapActions(registration, actionList);
        }

        // 4. VeloxMapper 5.x tarzı ForAllMaps (dondurulmuş kayıtlar üzerinde)
        foreach (var registration in _registrations.Values)
        {
            foreach (var action in options.RegistrationActions) action(registration);
        }

        // 5. Kalıtım: IncludeAllDerived → Include → IncludeBase birleştirme
        ResolveIncludeAllDerived();
        ResolveInheritance();

        // 6. Döngüsel tür grafiklerinde referans koruması (AutoMapper davranışı)
        EnablePreserveReferencesForCycles();
    }

    private void AddRegistration(MappingRegistration registration)
    {
        var key = (registration.SourceType, registration.DestinationType);
        if (_registrations.TryGetValue(key, out var existing))
        {
            throw new VeloxConfigurationException(
                $"'{registration.SourceType.FullName}' -> '{registration.DestinationType.FullName}' eşleştirmesi birden fazla kez tanımlanmış " +
                $"([{existing.ProfileName ?? "Global"}] ve [{registration.ProfileName ?? "Global"}]). Her tür çifti yalnızca bir kez tanımlanabilir.");
        }

        _registrations[key] = registration;
        if (IsOpenGeneric(registration)) _openGenericRegistrations.Add(registration);
    }

    private static bool IsOpenGeneric(MappingRegistration registration)
        => registration.SourceType.IsGenericTypeDefinition || registration.DestinationType.IsGenericTypeDefinition;

    private void ApplyPropertyMapActions(MappingRegistration registration,
        List<KeyValuePair<Func<PropertyMap, bool>, Action<PropertyMap, IMemberConfigurationExpression>>> actions)
    {
        var profile = GetProfile(registration);
        var typeMap = new TypeMap(registration.SourceType, registration.DestinationType, registration.ProfileName);
        foreach (var member in TypeMembers.GetDestinationMembers(registration.DestinationType, profile))
        {
            var sourceMember = ConventionResolver.FindDirect(registration.SourceType, member.Name, profile);
            var propertyMap = new PropertyMap(typeMap, member, TypeMembers.GetMemberType(member), sourceMember);
            foreach (var action in actions)
            {
                if (!action.Key(propertyMap)) continue;
                if (!registration.MutableMemberRules.TryGetValue(member.Name, out var rule))
                {
                    rule = new MemberMappingRule(member.Name);
                    registration.MutableMemberRules[member.Name] = rule;
                }

                action.Value(propertyMap, new MemberConfigurationExpression(rule, member));
                if (rule.SourceMemberPath != null)
                {
                    rule.MapFromExpression = MemberPath.BuildAccessor(registration.SourceType, rule.SourceMemberPath);
                    rule.SourceMemberPath = null;
                }
            }
        }
    }

    private void ResolveIncludeAllDerived()
    {
        var all = _registrations.Values.Where(r => !IsOpenGeneric(r)).ToList();
        foreach (var registration in all.Where(r => r.IsIncludeAllDerivedRequested))
        {
            foreach (var other in all)
            {
                if (other == registration) continue;
                if (registration.SourceType.IsAssignableFrom(other.SourceType) && registration.SourceType != other.SourceType &&
                    registration.DestinationType.IsAssignableFrom(other.DestinationType) && registration.DestinationType != other.DestinationType)
                {
                    registration.AddIncludedDerivedType(other.SourceType, other.DestinationType);
                }
            }
        }
    }

    private void ResolveInheritance()
    {
        // Include ile bildirilen türetilmiş eşleştirmeler, IncludeBase belirtmemişse tabanı devralır
        foreach (var registration in _registrations.Values.ToList())
        {
            foreach (var (derivedSource, derivedDestination) in registration.IncludedDerivedTypes)
            {
                if (_registrations.TryGetValue((derivedSource, derivedDestination), out var derived) && derived.BaseTypeMapping == null)
                {
                    derived.BaseTypeMapping = (registration.SourceType, registration.DestinationType);
                }
            }

            // IncludeBase ile tanımlanan türetilmiş eşleştirme, taban üzerinden polimorfik olarak da erişilebilir
            if (registration.BaseTypeMapping is { } baseKey && _registrations.TryGetValue(baseKey, out var baseRegistration))
            {
                baseRegistration.AddIncludedDerivedType(registration.SourceType, registration.DestinationType);
            }
        }

        var resolved = new HashSet<(Type, Type)>();
        foreach (var key in _registrations.Keys.ToList()) Merge(key, resolved, new HashSet<(Type, Type)>());
    }

    private void Merge((Type, Type) key, HashSet<(Type, Type)> resolved, HashSet<(Type, Type)> visiting)
    {
        if (resolved.Contains(key) || !visiting.Add(key)) return;
        if (!_registrations.TryGetValue(key, out var derived) || derived.BaseTypeMapping == null)
        {
            resolved.Add(key);
            return;
        }

        var baseKey = (derived.BaseTypeMapping.Value.BaseSource, derived.BaseTypeMapping.Value.BaseDestination);
        Merge(baseKey, resolved, visiting);

        if (_registrations.TryGetValue(baseKey, out var baseRegistration))
        {
            foreach (var kvp in baseRegistration.MemberRules)
            {
                if (!derived.MutableMemberRules.ContainsKey(kvp.Key)) derived.MutableMemberRules[kvp.Key] = kvp.Value.Clone();
            }

            var derivedPaths = new HashSet<string>(derived.ForPathRules.Select(r => string.Join(".", r.PathSegments)));
            derived.ForPathRules = derived.ForPathRules.Concat(baseRegistration.ForPathRules.Where(r => !derivedPaths.Contains(string.Join(".", r.PathSegments)))).ToList();

            var derivedParameters = new HashSet<string>(derived.CtorParamRules.Select(r => r.ParameterName), StringComparer.OrdinalIgnoreCase);
            derived.CtorParamRules = derived.CtorParamRules.Concat(baseRegistration.CtorParamRules.Where(r => !derivedParameters.Contains(r.ParameterName))).ToList();

            derived.BeforeMapActions = baseRegistration.BeforeMapActions.Concat(derived.BeforeMapActions).ToList();
            derived.AfterMapActions = baseRegistration.AfterMapActions.Concat(derived.AfterMapActions).ToList();
            derived.ValueTransformers = baseRegistration.ValueTransformers.Concat(derived.ValueTransformers).ToList();
            if (derived.MaxDepth == 0) derived.MaxDepth = baseRegistration.MaxDepth;
            derived.PreserveReferences |= baseRegistration.PreserveReferences;
            derived.IncludeMembers ??= baseRegistration.IncludeMembers;
        }

        resolved.Add(key);
    }

    private void EnablePreserveReferencesForCycles()
    {
        var registrations = _registrations.Values.Where(r => !IsOpenGeneric(r) && !r.HasTypeConverter && ExpressionBuilder.IsComplex(r.DestinationType)).ToList();
        var edges = new Dictionary<(Type, Type), List<(Type, Type)>>();

        foreach (var registration in registrations)
        {
            var profile = GetProfile(registration);
            var targets = new List<(Type, Type)>();
            foreach (var member in TypeMembers.GetDestinationMembers(registration.DestinationType, profile))
            {
                var destinationType = ElementOrSelf(TypeMembers.GetMemberType(member));
                Type? sourceType = null;
                if (registration.MemberRules.TryGetValue(member.Name, out var rule) && rule.MapFromExpression != null) sourceType = rule.MapFromExpression.ReturnType;
                else sourceType = ConventionResolver.FindDirect(registration.SourceType, member.Name, profile) is { } sourceMember ? TypeMembers.GetMemberType(sourceMember) : null;
                if (sourceType == null) continue;

                var key = (ElementOrSelf(sourceType), destinationType);
                if (_registrations.ContainsKey(key)) targets.Add(key);
            }

            edges[(registration.SourceType, registration.DestinationType)] = targets;
        }

        // Bir kayıttan başlayıp kendisine geri dönen her yol döngüdür
        foreach (var start in edges.Keys)
        {
            var stack = new Stack<(Type, Type)>(edges[start]);
            var seen = new HashSet<(Type, Type)>();
            while (stack.Count > 0)
            {
                var next = stack.Pop();
                if (next == start)
                {
                    var registration = _registrations[start];
                    if (registration.MaxDepth == 0) registration.PreserveReferences = true;
                    break;
                }

                if (!seen.Add(next) || !edges.TryGetValue(next, out var children)) continue;
                foreach (var child in children) stack.Push(child);
            }
        }
    }

    private static Type ElementOrSelf(Type type)
        => CollectionExpressionHelper.IsCollectionType(type) ? CollectionExpressionHelper.GetCollectionElementType(type)! : Nullable.GetUnderlyingType(type) ?? type;

    // ─── Kayıt arama ────────────────────────────────────────────────────────

    /// <summary>
    /// Tür çifti için kaydı bulur: tam eşleşme → open generic kapatma → kaynak türün tabanları (polimorfik kaynaklar).
    /// </summary>
    internal MappingRegistration? FindRegistration(Type sourceType, Type destinationType)
    {
        sourceType = Mapper.GetUnproxiedType(sourceType);
        return _lookupCache.GetOrAdd((sourceType, destinationType), key => new RegistrationLookup(Lookup(key.Item1, key.Item2))).Registration;
    }

    private MappingRegistration? Lookup(Type sourceType, Type destinationType)
    {
        if (_registrations.TryGetValue((sourceType, destinationType), out var exact)) return exact;

        var closed = CloseOpenGeneric(sourceType, destinationType);
        if (closed != null) return closed;

        if (!ExpressionBuilder.IsComplex(sourceType) || !ExpressionBuilder.IsComplex(destinationType)) return null;

        for (var baseType = sourceType.BaseType; baseType != null && baseType != typeof(object); baseType = baseType.BaseType)
        {
            if (_registrations.TryGetValue((baseType, destinationType), out var baseRegistration)) return baseRegistration;
        }

        foreach (var iface in sourceType.GetInterfaces())
        {
            if (_registrations.TryGetValue((iface, destinationType), out var interfaceRegistration)) return interfaceRegistration;
        }

        return null;
    }

    private MappingRegistration? CloseOpenGeneric(Type sourceType, Type destinationType)
    {
        if (_openGenericRegistrations.Count == 0) return null;

        foreach (var open in _openGenericRegistrations)
        {
            var sourceMatches = open.SourceType.IsGenericTypeDefinition
                ? sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == open.SourceType
                : open.SourceType == sourceType;
            var destinationMatches = open.DestinationType.IsGenericTypeDefinition
                ? destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == open.DestinationType
                : open.DestinationType == destinationType;
            if (!sourceMatches || !destinationMatches) continue;

            var closed = open.Clone(sourceType, destinationType);
            closed.Profile = open.Profile;
            foreach (var rule in closed.MemberRules.Values)
            {
                if (rule.SourceMemberPath != null)
                {
                    rule.MapFromExpression = MemberPath.BuildAccessor(sourceType, rule.SourceMemberPath);
                    rule.SourceMemberPath = null;
                }

                if (rule.ValueConverterSourceMemberName != null)
                {
                    rule.ValueConverterSourceMember = MemberPath.BuildAccessor(sourceType, rule.ValueConverterSourceMemberName);
                    rule.ValueConverterSourceMemberName = null;
                }
            }

            if (open.OpenGenericForAllMembers != null || open.OpenGenericForAllOtherMembers != null)
            {
                var configured = new HashSet<string>(closed.MemberRules.Keys);
                foreach (var member in DestinationMembers.GetWritable(destinationType))
                {
                    if (!closed.MutableMemberRules.TryGetValue(member.Name, out var rule))
                    {
                        rule = new MemberMappingRule(member.Name);
                        closed.MutableMemberRules[member.Name] = rule;
                    }

                    if (!configured.Contains(member.Name))
                    {
                        foreach (var action in open.OpenGenericForAllOtherMembers ?? Array.Empty<Action<MemberConfigurationExpression>>())
                            action(new MemberConfigurationExpression(rule, member));
                    }

                    foreach (var action in open.OpenGenericForAllMembers ?? Array.Empty<Action<MemberConfigurationExpression>>())
                        action(new MemberConfigurationExpression(rule, member));
                }
            }

            return _registrations.GetOrAdd((sourceType, destinationType), closed);
        }

        return null;
    }

    private sealed class RegistrationLookup
    {
        public RegistrationLookup(MappingRegistration? registration) => Registration = registration;
        public MappingRegistration? Registration { get; }
    }

    // ─── Delege önbelleği ───────────────────────────────────────────────────

    private const int TypedMap = 0;
    private const int TypedPatch = 1;
    private const int UntypedMap = 2;
    private const int UntypedPatch = 3;

    internal Func<TSource, VeloxResolutionContext, TDestination> GetMapDelegate<TSource, TDestination>()
    {
        // Tek slotluk statik önbellek: tipik uygulamada tek MapperConfiguration vardır; sözlük aramasını atlar.
        var cached = TypedDelegateCache<TSource, TDestination>.Map;
        if (cached != null && ReferenceEquals(cached.Owner, this)) return cached.Delegate;

        var created = (Func<TSource, VeloxResolutionContext, TDestination>)GetDelegate(TypedMap, typeof(TSource), typeof(TDestination));
        TypedDelegateCache<TSource, TDestination>.Map = new CacheEntry<Func<TSource, VeloxResolutionContext, TDestination>>(this, created);
        return created;
    }

    internal Func<TSource, TDestination, VeloxResolutionContext, TDestination> GetPatchDelegate<TSource, TDestination>()
    {
        var cached = TypedDelegateCache<TSource, TDestination>.Patch;
        if (cached != null && ReferenceEquals(cached.Owner, this)) return cached.Delegate;

        var created = (Func<TSource, TDestination, VeloxResolutionContext, TDestination>)GetDelegate(TypedPatch, typeof(TSource), typeof(TDestination));
        TypedDelegateCache<TSource, TDestination>.Patch = new CacheEntry<Func<TSource, TDestination, VeloxResolutionContext, TDestination>>(this, created);
        return created;
    }

    private static class TypedDelegateCache<TSource, TDestination>
    {
        internal static CacheEntry<Func<TSource, VeloxResolutionContext, TDestination>>? Map;
        internal static CacheEntry<Func<TSource, TDestination, VeloxResolutionContext, TDestination>>? Patch;
    }

    private sealed class CacheEntry<TDelegate>
    {
        public CacheEntry(MapperConfiguration owner, TDelegate @delegate)
        {
            Owner = owner;
            Delegate = @delegate;
        }

        public MapperConfiguration Owner { get; }
        public TDelegate Delegate { get; }
    }

    internal Func<object, VeloxResolutionContext, object> GetUntypedMapDelegate(Type sourceType, Type destinationType)
        => (Func<object, VeloxResolutionContext, object>)GetDelegate(UntypedMap, sourceType, destinationType);

    internal Func<object, object, VeloxResolutionContext, object> GetUntypedPatchDelegate(Type sourceType, Type destinationType)
        => (Func<object, object, VeloxResolutionContext, object>)GetDelegate(UntypedPatch, sourceType, destinationType);

    private Delegate GetDelegate(int kind, Type sourceType, Type destinationType)
    {
        var key = (sourceType, destinationType, kind);
        if (_delegates.TryGetValue(key, out var cached)) return cached.Value;
        return _delegates.GetOrAdd(key, k => new Lazy<Delegate>(() => Compile(k.Item3, k.Item1, k.Item2))).Value;
    }

    private Delegate Compile(int kind, Type sourceType, Type destinationType)
    {
        LambdaExpression lambda;
        try
        {
            lambda = kind switch
            {
                TypedMap => ExpressionBuilder.BuildMapLambda(sourceType, destinationType, this),
                TypedPatch => ExpressionBuilder.BuildPatchLambda(sourceType, destinationType, this),
                UntypedMap => WrapUntypedMap(sourceType, destinationType),
                _ => WrapUntypedPatch(sourceType, destinationType)
            };
        }
        catch (VeloxException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VeloxConfigurationException($"{sourceType.FullName} -> {destinationType.FullName} eşleştirme ifadesi üretilemedi: {ex.Message}", ex);
        }

        return lambda.Compile();
    }

    private LambdaExpression WrapUntypedMap(Type sourceType, Type destinationType)
    {
        var source = Expression.Parameter(typeof(object), "source");
        var context = Expression.Parameter(typeof(VeloxResolutionContext), "context");
        var typed = Expression.Constant(GetDelegate(TypedMap, sourceType, destinationType));
        var call = Expression.Invoke(typed, Expression.Convert(source, sourceType), context);
        return Expression.Lambda<Func<object, VeloxResolutionContext, object>>(Expression.Convert(call, typeof(object)), source, context);
    }

    private LambdaExpression WrapUntypedPatch(Type sourceType, Type destinationType)
    {
        var source = Expression.Parameter(typeof(object), "source");
        var destination = Expression.Parameter(typeof(object), "destination");
        var context = Expression.Parameter(typeof(VeloxResolutionContext), "context");
        var typed = Expression.Constant(GetDelegate(TypedPatch, sourceType, destinationType));
        var call = Expression.Invoke(typed, Expression.Convert(source, sourceType), Expression.Convert(destination, destinationType), context);
        return Expression.Lambda<Func<object, object, VeloxResolutionContext, object>>(Expression.Convert(call, typeof(object)), source, destination, context);
    }

    /// <summary>
    /// Source Generator (Layer 1) ile üretilmiş bir eşleştirme fonksiyonunu kaydeder; <c>Map&lt;TSource, TDestination&gt;</c>
    /// çağrıları çalışma zamanı ifadesi yerine bu fonksiyonu kullanır.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür.</typeparam>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="mapFunc">Üretilmiş eşleştirme fonksiyonu.</param>
    public void RegisterPrecompiledMapper<TSource, TDestination>(Func<TSource, TDestination> mapFunc)
    {
        if (mapFunc == null) throw new ArgumentNullException(nameof(mapFunc));
        Func<TSource, VeloxResolutionContext, TDestination> wrapped = (source, _) => mapFunc(source);
        _precompiledPairs[(typeof(TSource), typeof(TDestination))] = true;
        _delegates[(typeof(TSource), typeof(TDestination), TypedMap)] = new Lazy<Delegate>(() => wrapped);
        _delegates.TryRemove((typeof(TSource), typeof(TDestination), UntypedMap), out _);
        if (ReferenceEquals(TypedDelegateCache<TSource, TDestination>.Map?.Owner, this)) TypedDelegateCache<TSource, TDestination>.Map = null;
    }

    // ─── ProjectTo ──────────────────────────────────────────────────────────

    internal IQueryable Project(IQueryable source, Type destinationType, IDictionary<string, object>? parameters, IEnumerable<string>? membersToExpand)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        var sourceType = source.ElementType;
        var expansions = (membersToExpand ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        var key = (sourceType, destinationType, string.Join("|", expansions));
        var lambda = _projections.GetOrAdd(key, k => new Lazy<LambdaExpression>(() => ExpressionBuilder.BuildProjectionLambda(k.Item1, k.Item2, this, expansions))).Value;

        if (parameters != null && parameters.Count > 0)
        {
            lambda = (LambdaExpression)new ParameterValueReplacer(parameters).Visit(lambda)!;
        }

        var call = Expression.Call(QueryableSelectMethod.MakeGenericMethod(sourceType, destinationType), source.Expression, Expression.Quote(lambda));
        return source.Provider.CreateQuery(call);
    }

    /// <summary>
    /// ProjectTo ifadelerindeki kapanış (closure) değişkenlerini verilen parametre değerleriyle değiştirir.
    /// </summary>
    /// <summary>ProjectTo parametre değerini taşıyan, EF Core tarafından SQL parametresine çevrilen sarmalayıcı.</summary>
    private sealed class ParameterHolder<T>
    {
        public ParameterHolder(T value) => Value = value;

        public T Value { get; }
    }

    private sealed class ParameterValueReplacer : ExpressionVisitor
    {
        private readonly IDictionary<string, object> _parameters;

        public ParameterValueReplacer(IDictionary<string, object> parameters) => _parameters = parameters;

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression && _parameters.TryGetValue(node.Member.Name, out var value))
            {
                var type = node.Type;
                if (value != null && !type.IsInstanceOfType(value)) value = System.Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type, System.Globalization.CultureInfo.InvariantCulture);
                // Sabit yerine sarmalayıcı üye erişimi: EF Core bunu SQL parametresine çevirir (sorgu planı önbelleği korunur)
                var holder = Activator.CreateInstance(typeof(ParameterHolder<>).MakeGenericType(type), value)!;
                return Expression.Property(Expression.Constant(holder), "Value");
            }

            return base.VisitMember(node);
        }
    }

    // ─── IConfigurationProvider ─────────────────────────────────────────────

    /// <summary>Bu yapılandırmadan yeni bir mapper oluşturur.</summary>
    /// <returns>Yeni mapper.</returns>
    public IVeloxMapper CreateMapper() => new Mapper(this);

    /// <summary>Resolver/converter örneklerini verilen fabrika ile oluşturan yeni bir mapper oluşturur.</summary>
    /// <param name="serviceCtor">Tip alıp örnek döndüren fabrika.</param>
    /// <returns>Yeni mapper.</returns>
    public IVeloxMapper CreateMapper(Func<Type, object> serviceCtor) => new Mapper(this, serviceCtor);

    IMapper IConfigurationProvider.CreateMapper() => CreateMapper();

    IMapper IConfigurationProvider.CreateMapper(Func<Type, object> serviceCtor) => CreateMapper(serviceCtor);

    /// <inheritdoc />
    public LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));
        return ExpressionBuilder.BuildMapLambda(sourceType, destinationType, this);
    }

    /// <inheritdoc />
    public void CompileMappings()
    {
        foreach (var registration in GetAllRegistrations().ToList())
        {
            if (registration.SourceType.ContainsGenericParameters || registration.DestinationType.ContainsGenericParameters) continue;
            if (registration.DestinationType.IsAbstract && registration.RedirectDestinationType == null && !registration.HasTypeConverter && registration.FactoryDelegate == null && registration.FactoryDelegateWithContext == null) continue;
            GetDelegate(TypedMap, registration.SourceType, registration.DestinationType);
        }
    }

    /// <inheritdoc />
    public void AssertConfigurationIsValid() => ThrowIfInvalid(_ => true, null);

    /// <inheritdoc />
    public void AssertConfigurationIsValid<TProfile>() where TProfile : VeloxProfile
    {
        string name;
        try
        {
            name = ((VeloxProfile)Activator.CreateInstance(typeof(TProfile), nonPublic: true)!).ProfileName;
        }
        catch (MissingMethodException)
        {
            name = typeof(TProfile).FullName!;
        }

        AssertConfigurationIsValid(name);
    }

    /// <inheritdoc />
    public void AssertConfigurationIsValid(string profileName)
    {
        if (profileName == null) throw new ArgumentNullException(nameof(profileName));
        ThrowIfInvalid(r => r.ProfileName == profileName, profileName);
    }

    private void ThrowIfInvalid(Func<MappingRegistration, bool> filter, string? profileName)
    {
        var report = new ConfigurationValidator(this).Validate(GetAllRegistrations().Where(filter).ToList());
        if (report.Count == 0) return;

        var builder = new StringBuilder();
        builder.AppendLine(profileName == null
            ? "VeloxMapper yapılandırma doğrulaması başarısız. Aşağıdaki eşleştirmeleri gözden geçirin:"
            : $"VeloxMapper yapılandırma doğrulaması başarısız ('{profileName}' profili). Aşağıdaki eşleştirmeleri gözden geçirin:");
        builder.AppendLine();
        var index = 1;
        foreach (var error in report)
        {
            builder.Append("  ").Append(index++).Append(". ").AppendLine(error);
        }

        builder.AppendLine();
        builder.Append("Çözüm: eksik üyeler için ForMember(d => d.Uye, o => o.MapFrom(...)) veya o.Ignore() kullanın; ")
               .Append("kaynak doğrulaması için ForSourceMember(...).DoNotValidate(), doğrulamayı kapatmak için ValidateMemberList(MemberList.None).");
        throw new VeloxValidationException(builder.ToString());
    }

    // ─── Teşhis ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bir tür çifti için üye bazında eşleştirme planını (hangi hedef üyenin nereden geldiğini) döndürür.
    /// CI'da snapshot testi için <see cref="MappingPlanReport"/> ile birlikte kullanılabilir.
    /// </summary>
    /// <param name="source">Kaynak tür.</param>
    /// <param name="destination">Hedef tür.</param>
    /// <param name="mode">Eşleştirme modu.</param>
    public MappingPlanDef GetMappingPlan(Type source, Type destination, MappingMode mode = MappingMode.Map)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));

        var registration = FindRegistration(source, destination);
        var profile = GetProfile(registration);
        var parameter = Expression.Parameter(source, "src");
        var properties = new List<MappedPropertyDesc>();

        foreach (var member in TypeMembers.GetDestinationMembers(destination, profile).OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var description = new MappedPropertyDesc { TargetProperty = member.Name };
            MemberMappingRule? rule = null;
            registration?.MemberRules.TryGetValue(member.Name, out rule);
            var pathRule = registration?.ForPathRules.FirstOrDefault(p => p.PathSegments[0] == member.Name);

            if (rule?.IsIgnored == true || (rule == null && profile.IsGloballyIgnored(member.Name)))
            {
                description.SourceProperty = "Ignored";
                description.ExecutionType = "Ignored";
            }
            else if (rule != null && rule.HasValueSource)
            {
                description.SourceProperty = rule.MapFromExpression?.ToString()
                    ?? rule.ResolverType?.Name ?? rule.ResolverInstance?.GetType().Name
                    ?? rule.MemberValueResolverType?.Name ?? rule.ValueConverterType?.Name ?? rule.ValueConverter?.GetType().Name
                    ?? "Custom";
                description.ExecutionType = "Complex";
            }
            else if (pathRule != null)
            {
                description.SourceProperty = pathRule.MemberRule.MapFromExpression?.ToString() ?? string.Join(".", pathRule.PathSegments);
                description.ExecutionType = "Complex";
            }
            else if (ConventionResolver.FindDirect(source, member.Name, profile) is { } direct)
            {
                description.SourceProperty = direct.Name;
                description.ExecutionType = "Assigned";
            }
            else if (ConventionResolver.ResolveFlattening(parameter, member.Name, profile) != null)
            {
                description.SourceProperty = member.Name;
                description.ExecutionType = "Flattened";
            }
            else if (ConventionResolver.Resolve(parameter, member.Name, profile, registration, this) != null)
            {
                description.SourceProperty = member.Name;
                description.ExecutionType = "IncludedMember";
            }
            else
            {
                description.SourceProperty = "None";
                description.ExecutionType = "Unmapped";
            }

            properties.Add(description);
        }

        return new MappingPlanDef
        {
            SourceType = source.FullName ?? source.Name,
            DestinationType = destination.FullName ?? destination.Name,
            ProfileName = registration?.ProfileName ?? string.Empty,
            Mode = mode,
            Properties = properties.ToArray()
        };
    }

    /// <summary>
    /// <see cref="ILogger"/> üzerine yazan teşhis hedefi.
    /// </summary>
    private sealed class LoggerDiagnosticsSink : IVeloxDiagnosticsSink
    {
        private readonly ILogger _logger;

        public LoggerDiagnosticsSink(ILogger logger) => _logger = logger;

        public void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null)
        {
            var level = severity switch
            {
                "Debug" => LogLevel.Debug,
                "Warning" => LogLevel.Warning,
                "Error" => LogLevel.Error,
                _ => LogLevel.Information
            };
            _logger.Log(level, "{Message}", message);
        }
    }
}
