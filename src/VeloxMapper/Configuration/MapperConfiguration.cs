using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Exceptions;
using VeloxMapper.Diagnostics;
using VeloxMapper.Caching;

namespace VeloxMapper.Configuration;

/// <summary>
/// Patch (yama) moduna özel yapılandırma seçenekleri.
/// </summary>
public sealed class PatchMappingOptions
{
    /// <summary>
    /// Eğer true ise, yama modunda (Patch) kaynak (source) null olan değerler hedefe kopyalanmaz.
    /// Varsayılan: false — null bir valid değerdir ve üzerine yazar.
    /// </summary>
    public bool IgnoreNullValues { get; set; }
}

/// <summary>
/// Mapper'ın çalışma zamanı yapılandırmasını toplamak için kullanılan builder sınıfı.
/// MapperConfiguration oluşturulurken <c>Action&lt;VeloxMapperOptions&gt;</c> ile ayarlanır.
/// <para>
/// Desteklenen yapılandırma yöntemleri:
/// <list type="bullet">
///   <item>Profil ekleme: <c>AddProfile&lt;T&gt;()</c></item>
///   <item>Assembly tarama: <c>AddProfilesFromAssembly()</c></item>
///   <item>Inline eşleştirme: <c>CreateMap&lt;TSource, TDest&gt;()</c></item>
///   <item>Özel dönüştürücü: <c>AddCustomConverter&lt;TSource, TDest&gt;()</c></item>
/// </list>
/// </para>
/// </summary>
public sealed class VeloxMapperOptions
{
    /// <summary>Patch mapping davranışı yapılandırması (opt-in).</summary>
    public PatchMappingOptions PatchMapping { get; } = new();

    /// <summary>İsteğe bağlı diagnostics/loglama havuzu. Null ise sıfır maliyet.</summary>
    public IVeloxDiagnosticsSink? DiagnosticsSink { get; set; }

    /// <summary>Özel isimlendirme kuralı (isteğe bağlı).</summary>
    public ICustomNamingConvention? NamingConvention { get; set; }

    /// <summary>Kaynak özellik isimlendirme kuralı (isteğe bağlı).</summary>
    public ICustomNamingConvention? SourceMemberNamingConvention { get; set; }

    /// <summary>Hedef özellik isimlendirme kuralı (isteğe bağlı).</summary>
    public ICustomNamingConvention? DestinationMemberNamingConvention { get; set; }

    /// <summary>Global value transformer koleksiyonu.</summary>
    public ValueTransformerCollection ValueTransformers { get; } = new();

    internal Dictionary<string, ValueTransformerCollection> ProfileValueTransformers { get; } = new();

    /// <summary>Global düzeyde yoksayılacak property adları listesi.</summary>
    internal HashSet<string> GlobalIgnores { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Kaynak property adlarında tanınacak ön ekler listesi.</summary>
    internal List<string> Prefixes { get; } = new();

    /// <summary>Hedef property adlarında tanınacak ön ekler listesi.</summary>
    internal List<string> DestinationPrefixes { get; } = new();

    /// <summary>Property adlarında tanınacak son ekler listesi.</summary>
    internal List<string> Postfixes { get; } = new();

    /// <summary>Hedef property adlarında tanınacak son ekler listesi.</summary>
    internal List<string> DestinationPostfixes { get; } = new();

    /// <summary>Null koleksiyonların boş koleksiyon yerine null olarak map edilmesini sağlar.</summary>
    public bool AllowNullCollections { get; set; }

    /// <summary>Hangi property'lerin map edileceğini filtreleyen global delege.</summary>
    public Func<PropertyInfo, bool> ShouldMapProperty { get; set; } = p => p.GetMethod != null && p.GetMethod.IsPublic;

    /// <summary>Hangi field'ların map edileceğini filtreleyen global delege.</summary>
    public Func<FieldInfo, bool> ShouldMapField { get; set; } = f => false;

    /// <summary>Tüm mapping kayıtlarına uygulanacak toplu kural delegeleri.</summary>
    internal List<Action<MappingRegistration>> ForAllMapsActions { get; } = new();

    /// <summary>
    /// Belirtilen property adını tüm mapping'lerde yoksayar.
    /// </summary>
    public void AddGlobalIgnore(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return;
        GlobalIgnores.Add(propertyName);
    }

    /// <summary>
    /// Belirtilen property adlarını tüm mapping'lerde yoksayar.
    /// </summary>
    public void AddGlobalIgnore(params string[] propertyNames)
    {
        if (propertyNames == null) return;
        foreach (var name in propertyNames)
        {
            AddGlobalIgnore(name);
        }
    }

    /// <summary>
    /// Kaynak property adlarında aranacak ve eşleşme sırasında çıkarılacak ön ekleri ekler.
    /// </summary>
    public void RecognizePrefixes(params string[] prefixes)
    {
        if (prefixes == null) return;
        Prefixes.AddRange(prefixes);
    }

    /// <summary>
    /// Hedef property adlarında aranacak ve eşleşme sırasında çıkarılacak ön ekleri ekler.
    /// </summary>
    public void RecognizeDestinationPrefixes(params string[] prefixes)
    {
        if (prefixes == null) return;
        DestinationPrefixes.AddRange(prefixes);
    }

    /// <summary>
    /// Property adlarında aranacak ve eşleşme sırasında çıkarılacak son ekleri ekler.
    /// </summary>
    public void RecognizePostfixes(params string[] postfixes)
    {
        if (postfixes == null) return;
        Postfixes.AddRange(postfixes);
    }

    /// <summary>
    /// Hedef property adlarında aranacak ve eşleşme sırasında çıkarılacak son ekleri ekler.
    /// </summary>
    public void RecognizeDestinationPostfixes(params string[] postfixes)
    {
        if (postfixes == null) return;
        DestinationPostfixes.AddRange(postfixes);
    }

    /// <summary>
    /// Kaynak ve hedef property adlarından aranacak tüm ön ek listesini temizler.
    /// </summary>
    public void ClearPrefixes()
    {
        Prefixes.Clear();
        DestinationPrefixes.Clear();
    }

    /// <summary>
    /// Tüm eşleştirme kurallarına toplu olarak kural uygulamak için bir delege kaydeder.
    /// </summary>
    public void ForAllMaps(Action<MappingRegistration> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        ForAllMapsActions.Add(configure);
    }

    // --- Dahili depolar ---

    // Tip dönüştürücülerin dahili deposu
    internal Dictionary<(Type Source, Type Destination), object> Converters { get; } = new();

    // Profil ve inline CreateMap çağrılarından toplanan kayıtlar
    internal List<MappingRegistration> Registrations { get; } = new();

    // CreateMap ile oluşturulan MappingExpression'lar — Build işlemi sırasında Registration'a dönüştürülür
    private readonly List<Func<string?, MappingRegistration>> _inlineFactories = new();

    /// <summary>
    /// Belirtilen kaynak-hedef tür çifti için özel bir tip dönüştürücü kaydeder.
    /// Aynı çift ikinci kez eklenemez (fail-fast).
    /// </summary>
    public void AddCustomConverter<TSource, TDestination>(IVeloxTypeConverter<TSource, TDestination> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var key = (typeof(TSource), typeof(TDestination));
        if (Converters.ContainsKey(key))
        {
            throw new VeloxConfigurationException(
                $"'{typeof(TSource).FullName}' -> '{typeof(TDestination).FullName}' çifti için özel dönüştürücü zaten kayıtlı.");
        }

        Converters[key] = converter;
    }

    /// <summary>
    /// Belirtilen profil tipini ekler ve içindeki tüm eşleştirme kayıtlarını toplar.
    /// </summary>
    /// <typeparam name="TProfile">VeloxProfile alt sınıfı</typeparam>
    public void AddProfile<TProfile>() where TProfile : VeloxProfile, new()
    {
        AddProfile(new TProfile());
    }

    /// <summary>
    /// Verilen profil örneğini ekler ve içindeki tüm eşleştirme kayıtlarını toplar.
    /// </summary>
    public void AddProfile(VeloxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var profileRegistrations = profile.BuildRegistrations();
        Registrations.AddRange(profileRegistrations);

        var profileName = profile.GetType().Name;
        ProfileValueTransformers[profileName] = profile.ProfileValueTransformers;
    }

    /// <summary>
    /// Belirtilen assembly'deki tüm <see cref="VeloxProfile"/> alt sınıflarını otomatik bulur ve kaydeder.
    /// AutoMapper'ın assembly scanning özelliğinin VeloxMapper karşılığıdır.
    /// </summary>
    /// <param name="assembly">Taranacak assembly</param>
    public void AddProfilesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        // Assembly'deki tüm somut (non-abstract) VeloxProfile alt sınıflarını bul
        var profileTypes = assembly.GetTypes()
            .Where(t => typeof(VeloxProfile).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var profileType in profileTypes)
        {
            var profile = (VeloxProfile)Activator.CreateInstance(profileType)!;
            AddProfile(profile);
        }
    }

    /// <summary>
    /// Verilen tür parametresinin bulunduğu assembly'yi tarar.
    /// <c>AddProfilesFromAssembly(typeof(T).Assembly)</c> için kısayol.
    /// </summary>
    /// <typeparam name="T">Assembly'deki herhangi bir tür</typeparam>
    public void AddProfilesFromAssemblyOf<T>()
    {
        AddProfilesFromAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Birden fazla assembly'yi tarayarak tüm profilleri kaydeder.
    /// </summary>
    public void AddProfilesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            AddProfilesFromAssembly(assembly);
        }
    }

    /// <summary>
    /// Profil kullanmadan doğrudan bir kaynak-hedef eşleştirmesi tanımlar (inline kullanım).
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <returns>ForMember/Ignore/ConvertUsing zincirleme arayüzü</returns>
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var expression = new MappingExpression<TSource, TDestination>();
        _inlineFactories.Add(profileName => expression.Build(profileName));
        return expression;
    }

    /// <summary>
    /// Tip nesneleri ile eşleştirme tanımlar. Özellikle Open Generic eşleştirmeleri için kullanılır (inline kullanım).
    /// </summary>
    /// <param name="sourceType">Kaynak türü</param>
    /// <param name="destinationType">Hedef türü</param>
    /// <returns>Fluent yapılandırma arayüzü</returns>
    public IMappingExpression CreateMap(Type sourceType, Type destinationType)
    {
        if (sourceType is null)
        {
            throw new ArgumentNullException(nameof(sourceType)); // Kaynak tür null olamaz.
        }
        if (destinationType is null)
        {
            throw new ArgumentNullException(nameof(destinationType)); // Hedef tür null olamaz.
        }

        // Açık generic (Open Generic) tipler için özel expression oluşturulur
        if (sourceType.IsGenericTypeDefinition || destinationType.IsGenericTypeDefinition)
        {
            var openExpr = new OpenGenericMappingExpression(sourceType, destinationType);
            _inlineFactories.Add(profileName => openExpr.Build(profileName));
            return openExpr;
        }

        var expressionType = typeof(MappingExpression<,>).MakeGenericType(sourceType, destinationType);
        var expression = (IMappingExpression)Activator.CreateInstance(expressionType)!;

        _inlineFactories.Add(profileName => {
            var buildMethod = expressionType.GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (MappingRegistration)buildMethod.Invoke(expression, [profileName])!;
        });

        return expression;
    }

    /// <summary>
    /// Inline CreateMap çağrılarını MappingRegistration'a dönüştürür.
    /// MapperConfiguration oluşturulurken çağrılır.
    /// </summary>
    internal void FinalizeInlineRegistrations()
    {
        foreach (var factory in _inlineFactories)
        {
            Registrations.Add(factory(null)); // inline kayıtların profil adı yok
        }
    }
}

/// <summary>
/// Tüm çalışma zamanı yapılandırmasını saklayan, dondurulmuş (immutable) kurallar kümesidir.
/// Oluşturulduktan sonra değiştirilemez — fail-fast prensibi.
/// <para>
/// Desteklenen kullanımlar:
/// <code>
/// // 1. Yapılandırma delegesi ile
/// var config = new MapperConfiguration(options =&gt; { ... });
///
/// // 2. Assembly scanning ile (kısayol)
/// var config = new MapperConfiguration(typeof(Program).Assembly);
///
/// // 3. Birden fazla assembly ile
/// var config = new MapperConfiguration(assembly1, assembly2);
/// </code>
/// </para>
/// </summary>
public sealed class MapperConfiguration
{
    private readonly bool _ignoreNullValues;
    private readonly IVeloxDiagnosticsSink? _diagnosticsSink;
    private readonly ICustomNamingConvention? _namingConvention;
    private readonly ICustomNamingConvention? _sourceMemberNamingConvention;
    private readonly ICustomNamingConvention? _destinationMemberNamingConvention;
    private readonly Dictionary<(Type, Type), object> _converters;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, Type), MappingRegistration> _registrations;
    private readonly HashSet<string> _globalIgnores;
    private readonly List<string> _prefixes;
    private readonly List<string> _destinationPrefixes;
    private readonly List<string> _postfixes;
    private readonly List<string> _destinationPostfixes;
    private readonly bool _allowNullCollections;
    private readonly Func<PropertyInfo, bool> _shouldMapProperty;
    private readonly Func<FieldInfo, bool> _shouldMapField;

    /// <summary>
    /// Önceden derlenmiş (precompiled) Source Generator eşleştirmelerini tutan thread-safe sözlük.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Source, Type Destination), Delegate> _precompiledMappers = new();

    /// <summary>Patch modunda null değerlerin atlanıp atlanmayacağını belirler.</summary>
    public bool IgnoreNullValues => _ignoreNullValues;

    /// <summary>Diagnostics/loglama havuzu (null olabilir).</summary>
    public IVeloxDiagnosticsSink? DiagnosticsSink => _diagnosticsSink;

    /// <summary>Özel isimlendirme kuralı (null olabilir).</summary>
    public ICustomNamingConvention? NamingConvention => _namingConvention;

    /// <summary>Kaynak özellik isimlendirme kuralı (null olabilir).</summary>
    public ICustomNamingConvention? SourceMemberNamingConvention => _sourceMemberNamingConvention;

    /// <summary>Hedef özellik isimlendirme kuralı (null olabilir).</summary>
    public ICustomNamingConvention? DestinationMemberNamingConvention => _destinationMemberNamingConvention;

    private readonly ValueTransformerCollection _globalValueTransformers;
    private readonly Dictionary<string, ValueTransformerCollection> _profileValueTransformers;

    /// <summary>Global value transformer koleksiyonu.</summary>
    public ValueTransformerCollection GlobalValueTransformers => _globalValueTransformers;

    /// <summary>Profil bazlı value transformer koleksiyonu.</summary>
    public IReadOnlyDictionary<string, ValueTransformerCollection> ProfileValueTransformers => _profileValueTransformers;

    /// <summary>Global düzeyde yoksayılacak property adları.</summary>
    public IReadOnlyCollection<string> GlobalIgnores => _globalIgnores;

    /// <summary>Kaynak property adlarında tanınacak ön ekler.</summary>
    public IReadOnlyList<string> Prefixes => _prefixes;

    /// <summary>Hedef property adlarında tanınacak ön ekler.</summary>
    public IReadOnlyList<string> DestinationPrefixes => _destinationPrefixes;

    /// <summary>Property adlarında tanınacak son ekler.</summary>
    public IReadOnlyList<string> Postfixes => _postfixes;

    /// <summary>Hedef property adlarında tanınacak son ekler.</summary>
    public IReadOnlyList<string> DestinationPostfixes => _destinationPostfixes;

    /// <summary>Null koleksiyonların boş koleksiyon yerine null olarak map edilmesini sağlar.</summary>
    public bool AllowNullCollections => _allowNullCollections;

    /// <summary>Hangi property'lerin map edileceğini filtreleyen global delege.</summary>
    public Func<PropertyInfo, bool> ShouldMapProperty => _shouldMapProperty;

    /// <summary>Hangi field'ların map edileceğini filtreleyen global delege.</summary>
    public Func<FieldInfo, bool> ShouldMapField => _shouldMapField;

    /// <summary>Kayıtlı eşleştirme sayısı (diagnostics için).</summary>
    public int RegistrationCount => _registrations.Count;

    /// <summary>
    /// Source Generator (Layer 1) tarafından üretilmiş önceden derlenmiş (precompiled) eşleştirme fonksiyonunu kaydeder.
    /// </summary>
    /// <typeparam name="TSource">Kaynak türü</typeparam>
    /// <typeparam name="TDestination">Hedef türü</typeparam>
    /// <param name="mapFunc">Eşleştirme fonksiyonu</param>
    public void RegisterPrecompiledMapper<TSource, TDestination>(Func<TSource, TDestination> mapFunc)
    {
        ArgumentNullException.ThrowIfNull(mapFunc);
        _precompiledMappers[(typeof(TSource), typeof(TDestination))] = mapFunc;
    }

    /// <summary>
    /// Belirtilen kaynak ve hedef türü için kayıtlı önceden derlenmiş (precompiled) mapper delegesini döndürür.
    /// Bulunamazsa null döner.
    /// </summary>
    internal Delegate? GetPrecompiledMapper(Type sourceType, Type destinationType)
    {
        sourceType = Mapper.GetUnproxiedType(sourceType);
        destinationType = Mapper.GetUnproxiedType(destinationType);
        return _precompiledMappers.TryGetValue((sourceType, destinationType), out var mapper) ? mapper : null;
    }

    /// <summary>
    /// Yapılandırma delegesi ile oluşturur.
    /// </summary>
    public MapperConfiguration(Action<VeloxMapperOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new VeloxMapperOptions();
        configure(options);

        // Inline CreateMap kayıtlarını sonlandır
        options.FinalizeInlineRegistrations();

        // Fail-Fast: Yapılandırma doğrulamaları
        _ignoreNullValues = options.PatchMapping.IgnoreNullValues;
        _diagnosticsSink = options.DiagnosticsSink;
        _namingConvention = options.NamingConvention;
        _sourceMemberNamingConvention = options.SourceMemberNamingConvention ?? options.NamingConvention;
        _destinationMemberNamingConvention = options.DestinationMemberNamingConvention ?? options.NamingConvention;
        _globalValueTransformers = options.ValueTransformers;
        _profileValueTransformers = new Dictionary<string, ValueTransformerCollection>(options.ProfileValueTransformers);
        _globalIgnores = new HashSet<string>(options.GlobalIgnores, StringComparer.OrdinalIgnoreCase);
        _prefixes = new List<string>(options.Prefixes);
        _destinationPrefixes = new List<string>(options.DestinationPrefixes);
        _postfixes = new List<string>(options.Postfixes);
        _destinationPostfixes = new List<string>(options.DestinationPostfixes);
        _allowNullCollections = options.AllowNullCollections;
        _shouldMapProperty = options.ShouldMapProperty ?? (p => p.GetMethod != null && p.GetMethod.IsPublic);
        _shouldMapField = options.ShouldMapField ?? (f => false);

        // Converter sözlüğünü dondur
        _converters = new Dictionary<(Type, Type), object>(options.Converters);

        // Global ForAllMaps kurallarını kayıtlara uygula
        foreach (var reg in options.Registrations)
        {
            foreach (var action in options.ForAllMapsActions)
            {
                action(reg);
            }
            reg.RecalculateRequiresContext();
        }

        // Kayıtları (profil + inline) dondur
        _registrations = new System.Collections.Concurrent.ConcurrentDictionary<(Type, Type), MappingRegistration>();
        foreach (var reg in options.Registrations)
        {
            var key = (reg.SourceType, reg.DestinationType);

            // Aynı kaynak-hedef çifti farklı profillerde tanımlanmışsa — fail-fast
            if (_registrations.ContainsKey(key))
            {
                var existing = _registrations[key];
                throw new VeloxConfigurationException(
                    $"'{reg.SourceType.FullName}' -> '{reg.DestinationType.FullName}' eşleştirmesi birden fazla yerde tanımlanmış. " +
                    $"Mevcut: [{existing.ProfileName ?? "Inline"}], Yeni: [{reg.ProfileName ?? "Inline"}]. " +
                    $"Her eşleştirme çifti yalnızca bir kez tanımlanabilir.");
            }

            _registrations[key] = reg;

            // Eğer MappingRegistration'da CustomConverter varsa, _converters'a da ekle
            if (reg.CustomConverter != null && !_converters.ContainsKey(key))
            {
                _converters[key] = reg.CustomConverter;
            }
        }

        // IsReverseMapRequested olan kayıtlar için ters yön kayıtlarını otomatik ekle
        var explicitRegistrations = _registrations.Values.ToList();
        foreach (var reg in explicitRegistrations)
        {
            if (reg.IsReverseMapRequested)
            {
                var reverseKey = (reg.DestinationType, reg.SourceType);
                if (!_registrations.ContainsKey(reverseKey))
                {
                    var reverseReg = MappingRegistration.CreateReverse(reg);
                    _registrations[reverseKey] = reverseReg;
                }
            }
        }

        // IncludeAllDerived kurallarını otomatik çözümle
        ApplyIncludeAllDerivedMappings();

        // IncludeBase miras kurallarını uygula
        ApplyBaseTypeMappings();

        // Performans Optimizasyonu: Kayıtların context (ResolutionContext) gereksinimlerini derinlemesine optimize et
        OptimizeRequiresContext();
    }

    /// <summary>
    /// Assembly scanning kısayolu — verilen assembly'lerdeki tüm VeloxProfile alt sınıflarını bulur.
    /// </summary>
    public MapperConfiguration(Assembly[] assemblies)
        : this(cfg =>
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            foreach (var assembly in assemblies)
            {
                cfg.AddProfilesFromAssembly(assembly);
            }
        })
    {
    }

    /// <summary>
    /// Belirtilen kaynak ve hedef tür çifti için eşleştirme planını çıkarır.
    /// </summary>
    /// <param name="source">Kaynak tür</param>
    /// <param name="destination">Hedef tür</param>
    /// <param name="mode">Haritalama modu (varsayılan: Map)</param>
    /// <returns>Detaylı haritalama planı</returns>
    public MappingPlanDef GetMappingPlan(Type source, Type destination, MappingMode mode = MappingMode.Map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var plan = new MappingPlanDef
        {
            SourceType = source.FullName ?? source.Name,
            DestinationType = destination.FullName ?? destination.Name,
            Mode = mode
        };

        var reg = GetRegistration(source, destination);
        if (reg != null)
        {
            plan.ProfileName = reg.ProfileName ?? "";
        }

        var properties = new List<MappedPropertyDesc>();
        var sourceProps = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(ShouldMapProperty).ToArray();
        var destProps = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.CanWrite && ShouldMapProperty(p))
            .ToArray();

        foreach (var destProp in destProps)
        {
            var desc = new MappedPropertyDesc
            {
                TargetProperty = destProp.Name
            };

            if (_globalIgnores.Contains(destProp.Name))
            {
                desc.SourceProperty = "Ignored";
                desc.ExecutionType = "Ignored";
            }
            else if (reg != null && reg.MemberRules.TryGetValue(destProp.Name, out var rule))
            {
                if (rule.IsIgnored || rule.IsDoNotValidate)
                {
                    desc.SourceProperty = "Ignored";
                    desc.ExecutionType = "Ignored";
                }
                else if (rule.MapFromExpression != null)
                {
                    desc.SourceProperty = rule.MapFromExpression.ToString();
                    desc.ExecutionType = "Complex";
                }
            }
            else
            {
                var srcProp = sourceProps.FirstOrDefault(s =>
                    Execution.NameMatchingHelper.IsMatch(s.Name, destProp.Name, this));

                if (srcProp != null)
                {
                    desc.SourceProperty = srcProp.Name;
                    desc.ExecutionType = "Assigned";
                }
                else if (CanFlatten(source, destProp.Name))
                {
                    desc.SourceProperty = destProp.Name;
                    desc.ExecutionType = "Flattened";
                }
                else
                {
                    desc.SourceProperty = "None";
                    desc.ExecutionType = "Unmapped";
                }
            }

            properties.Add(desc);
        }

        plan.Properties = properties.ToArray();
        return plan;
    }

    /// <summary>
    /// Tüm kayıtlı eşleştirmelerin geçerliliğini doğrular.
    /// Eşleştirilemeyen property'ler, uyumsuz türler veya eksik converter'lar tespit edilirse
    /// <see cref="VeloxValidationException"/> fırlatır.
    /// Startup/CI pipeline'da çağrılması önerilir.
    /// </summary>
    public void AssertConfigurationIsValid()
    {
        var errors = new List<string>();

        foreach (var kvp in _registrations)
        {
            var reg = kvp.Value;

            // Özel dönüştürücü varsa property eşleştirmesi kontrolü atlanır
            if (reg.CustomConverter != null) continue;

            // Kaynak tür Dictionary ise property doğrulaması atlanır
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(reg.SourceType)) continue;

            var sourceProps = reg.SourceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(ShouldMapProperty).ToArray();
            var destProps = reg.DestinationType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.CanWrite && ShouldMapProperty(p))
                .ToArray();

            foreach (var destProp in destProps)
            {
                // Global ignore edilmişse atla
                if (_globalIgnores.Contains(destProp.Name))
                    continue;

                // Ignore veya DoNotValidate edilmiş property'leri atla
                if (reg.MemberRules.TryGetValue(destProp.Name, out var rule) && (rule.IsIgnored || rule.IsDoNotValidate))
                    continue;

                // MapFrom ile özel kaynak belirtilmişse OK
                if (reg.MemberRules.TryGetValue(destProp.Name, out var mapRule) && mapRule.MapFromExpression != null)
                    continue;

                // ForPath kuralları tarafından kapsanan bir property ise OK
                if (reg.ForPathRules != null && reg.ForPathRules.Any(r => r.PathSegments != null && r.PathSegments.Length > 0 && string.Equals(r.PathSegments[0], destProp.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // İsimle eşleşen kaynak property ara
                var srcProp = sourceProps.FirstOrDefault(s =>
                    Execution.NameMatchingHelper.IsMatch(s.Name, destProp.Name, this));

                if (srcProp == null)
                {
                    // Flattening ile eşleşebilir mi kontrol et
                    if (!CanFlatten(reg.SourceType, destProp.Name))
                    {
                        errors.Add(
                            $"[{reg.ProfileName ?? "Inline"}] {reg.SourceType.Name} -> {reg.DestinationType.Name}: " +
                            $"Hedef property '{destProp.Name}' için kaynak bulunamadı. " +
                            $"ForMember ile MapFrom veya Ignore kullanın.");
                    }
                }
                else if (!destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                {
                    // Tür uyumluluğunu kontrol et (temel kontrol)
                    // Enum dönüşümü veya nullable wrapper'ı olabilir — bunları izin ver
                    if (!IsCompatibleType(srcProp.PropertyType, destProp.PropertyType))
                    {
                        errors.Add(
                            $"[{reg.ProfileName ?? "Inline"}] {reg.SourceType.Name} -> {reg.DestinationType.Name}: " +
                            $"'{destProp.Name}' property tipi uyumsuz: " +
                            $"{srcProp.PropertyType.Name} -> {destProp.PropertyType.Name}.");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new VeloxValidationException(
                $"VeloxMapper yapılandırma doğrulaması başarısız — {errors.Count} hata bulundu:\n" +
                string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}")));
        }
    }

    /// <summary>
    /// IsIncludeAllDerivedRequested bayrağı set edilmiş kayıtları tarar
    /// ve alt sınıf eşleştirmelerini otomatik polimorfik haritalamaya (Include) bağlar.
    /// </summary>
    private void ApplyIncludeAllDerivedMappings()
    {
        var allRegs = _registrations.Values.ToList();
        foreach (var reg in allRegs)
        {
            if (reg.IsIncludeAllDerivedRequested)
            {
                foreach (var otherReg in allRegs)
                {
                    if (otherReg == reg) continue;

                    // Kaynak ve hedef türlerin her ikisi de derived ise otomatik Include kapsamına alıyoruz.
                    bool isSourceDerived = reg.SourceType.IsAssignableFrom(otherReg.SourceType) && reg.SourceType != otherReg.SourceType;
                    bool isDestDerived = reg.DestinationType.IsAssignableFrom(otherReg.DestinationType) && reg.DestinationType != otherReg.DestinationType;

                    if (isSourceDerived && isDestDerived)
                    {
                        reg.AddIncludedDerivedType(otherReg.SourceType, otherReg.DestinationType);
                    }
                }
            }
        }
    }

    /// <summary>
    /// IncludeBase miras hiyerarşisini çözümler ve kuralları derived kayıtlarla birleştirir.
    /// </summary>
    private void ApplyBaseTypeMappings()
    {
        var visited = new HashSet<(Type, Type)>();
        var keys = _registrations.Keys.ToList();

        foreach (var key in keys)
        {
            ResolveInheritance(key, visited);
        }
    }

    private void ResolveInheritance((Type Source, Type Destination) key, HashSet<(Type, Type)> visited)
    {
        if (!visited.Add(key)) return;

        if (!_registrations.TryGetValue(key, out var reg)) return;

        if (reg.BaseTypeMapping == null) return;

        var baseKey = reg.BaseTypeMapping.Value;

        // Önce base mapping'in miras zincirini çöz
        ResolveInheritance(baseKey, visited);

        if (_registrations.TryGetValue(baseKey, out var baseReg))
        {
            // Base registration'daki kuralları bu registration ile birleştir
            var mergedReg = MergeRegistrations(baseReg, reg);
            _registrations[key] = mergedReg;
        }
    }

    private MappingRegistration MergeRegistrations(MappingRegistration baseReg, MappingRegistration derivedReg)
    {
        // 1. MemberRules birleştirme: derived kuralları base kurallarının üzerine yazar.
        var mergedMemberRules = new Dictionary<string, MemberMappingRule>(baseReg.MemberRules);
        foreach (var kvp in derivedReg.MemberRules)
        {
            mergedMemberRules[kvp.Key] = kvp.Value;
        }

        // 2. BeforeMap ve AfterMap eylemlerini birleştir
        var mergedBefore = new List<object>(baseReg.BeforeMapActions);
        mergedBefore.AddRange(derivedReg.BeforeMapActions);

        var mergedAfter = new List<object>(baseReg.AfterMapActions);
        mergedAfter.AddRange(derivedReg.AfterMapActions);

        // 3. Diğer özellikleri birleştir
        var customConverter = derivedReg.CustomConverter ?? baseReg.CustomConverter;
        var factory = derivedReg.FactoryDelegate ?? baseReg.FactoryDelegate;
        var condition = derivedReg.ForAllMembersCondition ?? baseReg.ForAllMembersCondition;
        var maxDepth = derivedReg.MaxDepth > 0 ? derivedReg.MaxDepth : baseReg.MaxDepth;
        var preserveReferences = derivedReg.PreserveReferences || baseReg.PreserveReferences;

        // 4. ForPath ve ForCtorParam kurallarını birleştir (derived olanlar öncelikli)
        var mergedForPath = new List<ForPathRule>(derivedReg.ForPathRules);
        var derivedPaths = new HashSet<string>(derivedReg.ForPathRules.Select(r => string.Join(".", r.PathSegments)), StringComparer.OrdinalIgnoreCase);
        foreach (var baseRule in baseReg.ForPathRules)
        {
            var basePathKey = string.Join(".", baseRule.PathSegments);
            if (!derivedPaths.Contains(basePathKey))
            {
                mergedForPath.Add(baseRule);
            }
        }

        var mergedCtorParam = new List<CtorParamRule>(derivedReg.CtorParamRules);
        var derivedParams = new HashSet<string>(derivedReg.CtorParamRules.Select(r => r.ParameterName), StringComparer.OrdinalIgnoreCase);
        foreach (var baseRule in baseReg.CtorParamRules)
        {
            if (!derivedParams.Contains(baseRule.ParameterName))
            {
                mergedCtorParam.Add(baseRule);
            }
        }

        return new MappingRegistration(
            derivedReg.SourceType,
            derivedReg.DestinationType,
            mergedMemberRules,
            customConverter,
            derivedReg.ProfileName,
            factory,
            condition,
            derivedReg.ForAllMembersIgnored || baseReg.ForAllMembersIgnored,
            derivedReg.IsReverseMapRequested,
            mergedBefore,
            mergedAfter,
            maxDepth,
            preserveReferences,
            derivedReg.IncludedDerivedTypes,
            derivedReg.BaseTypeMapping,
            mergedForPath,
            mergedCtorParam);
    }

    /// <summary>
    /// Tüm kayıtlı eşleştirmelerin (registration) bağımlılık grafiğini analiz ederek,
    /// gereksiz yere context (ResolutionContext) kullanımını önleyecek şekilde RequiresContext bayraklarını optimize eder.
    /// </summary>
    private void OptimizeRequiresContext()
    {
        var visited = new Dictionary<(Type, Type), bool>();
        var stack = new HashSet<(Type, Type)>();

        foreach (var kvp in _registrations)
        {
            CheckRequiresContext(kvp.Key.Item1, kvp.Key.Item2, visited, stack);
        }
    }

    /// <summary>
    /// Belirtilen kaynak ve hedef türü çifti için mapping işleminin context gerektirip gerektirmediğini rekürsif ve dairesel referans korumalı olarak kontrol eder.
    /// </summary>
    private bool CheckRequiresContext(Type source, Type destination, Dictionary<(Type, Type), bool> visited, HashSet<(Type, Type)> stack)
    {
        var key = (source, destination);
        if (visited.TryGetValue(key, out var cachedResult))
        {
            return cachedResult;
        }

        // Dairesel referans (döngü) tespiti
        if (stack.Contains(key))
        {
            // Döngü oluştuğunda döngüyü kırmak için false dönüyoruz.
            // Döngüdeki elemanlardan herhangi biri başka bir sebeple (örn. resolver, custom action) context gerektiriyorsa,
            // o eleman kendi dalından zaten true dönecek ve bu bilgi tüm zincire yayılacaktır.
            return false;
        }

        if (!_registrations.TryGetValue(key, out var reg))
        {
            return false;
        }

        // Eğer eşleştirme kendi kuralları gereği (before/after action vb.) doğrudan context gerektiriyorsa true'dur.
        if (reg.SelfRequiresContext())
        {
            visited[key] = true;
            reg.SetRequiresContext(true);
            return true;
        }

        stack.Add(key);

        bool childRequires = false;
        var destProps = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.CanWrite && ShouldMapProperty(p)).ToArray();
        var sourceProps = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(ShouldMapProperty).ToArray();

        foreach (var destProp in destProps)
        {
            // Global ignore edilenleri atla
            if (_globalIgnores.Contains(destProp.Name))
                continue;

            // Ignore edilen property'leri atla
            if (reg.MemberRules.TryGetValue(destProp.Name, out var rule) && rule.IsIgnored)
                continue;

            // Bu property için kaynak tipi belirle
            Type? srcPropType = null;
            if (rule != null && rule.MapFromExpression != null)
            {
                srcPropType = rule.MapFromExpression.ReturnType;
            }
            else
            {
                var srcProp = sourceProps.FirstOrDefault(s => Execution.NameMatchingHelper.IsMatch(s.Name, destProp.Name, this));
                if (srcProp != null)
                {
                    srcPropType = srcProp.PropertyType;
                }
            }

            if (srcPropType == null)
                continue;

            var destPropType = destProp.PropertyType;

            // Koleksiyon ise eleman tiplerini çözümle
            if (Execution.CollectionExpressionHelper.IsCollectionType(srcPropType) &&
                Execution.CollectionExpressionHelper.IsCollectionType(destPropType))
            {
                var srcElem = Execution.CollectionExpressionHelper.GetCollectionElementType(srcPropType);
                var dstElem = Execution.CollectionExpressionHelper.GetCollectionElementType(destPropType);
                if (srcElem != null && dstElem != null)
                {
                    srcPropType = srcElem;
                    destPropType = dstElem;
                }
            }

            // Karmaşık tip (primitive olmayan, string olmayan vb.) bağımlılığını kontrol et
            if (!destPropType.IsPrimitive && destPropType != typeof(string) && destPropType != typeof(decimal) &&
                destPropType != typeof(DateTime) && destPropType != typeof(Guid) && destPropType != typeof(TimeSpan) && !destPropType.IsEnum)
            {
                if (CheckRequiresContext(srcPropType, destPropType, visited, stack))
                {
                    childRequires = true;
                    break;
                }
            }
        }

        stack.Remove(key);

        visited[key] = childRequires;
        reg.SetRequiresContext(childRequires);
        return childRequires;
    }

    /// <summary>
    /// Belirtilen kaynak-hedef çifti için kayıtlı özel dönüştürücüyü döndürür.
    /// Bulunamazsa null döner.
    /// </summary>
    internal object? GetCustomConverter(Type source, Type destination)
    {
        return _converters.TryGetValue((source, destination), out var converter)
            ? converter
            : null;
    }

    /// <summary>
    /// Belirtilen kaynak-hedef çifti için kayıtlı MappingRegistration'ı döndürür.
    /// Bulunamazsa null döner.
    /// </summary>
    internal MappingRegistration? GetRegistration(Type source, Type destination)
    {
        source = Mapper.GetUnproxiedType(source);
        destination = Mapper.GetUnproxiedType(destination);
        if (_registrations.TryGetValue((source, destination), out var reg))
            return reg;

        // Open Generics desteği: Eğer kaynak ve hedef tipler generic ise ve kapalıysa (closed generic)
        if (source.IsGenericType && destination.IsGenericType && 
            !source.IsGenericTypeDefinition && !destination.IsGenericTypeDefinition)
        {
            var openSource = source.GetGenericTypeDefinition();
            var openDest = destination.GetGenericTypeDefinition();

            if (_registrations.TryGetValue((openSource, openDest), out var openReg))
            {
                var key = (source, destination);
                lock (_registrations)
                {
                    if (_registrations.TryGetValue(key, out var existing))
                        return existing;

                    var closedReg = CloseGenericRegistration(openReg, source, destination);
                    _registrations[key] = closedReg;
                    return closedReg;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Open generic bir kaydı belirli kaynak ve hedef tipleriyle kapatıp yeni bir kayıt üretir.
    /// </summary>
    private MappingRegistration CloseGenericRegistration(MappingRegistration openReg, Type closedSource, Type closedDest)
    {
        return new MappingRegistration(
            closedSource,
            closedDest,
            new Dictionary<string, MemberMappingRule>(openReg.MemberRules),
            openReg.CustomConverter,
            openReg.ProfileName + " [Closed Generic]",
            openReg.FactoryDelegate,
            openReg.ForAllMembersCondition,
            openReg.ForAllMembersIgnored,
            openReg.IsReverseMapRequested,
            openReg.BeforeMapActions,
            openReg.AfterMapActions,
            openReg.MaxDepth,
            openReg.PreserveReferences,
            openReg.IncludedDerivedTypes,
            openReg.BaseTypeMapping,
            openReg.ForPathRules,
            openReg.CtorParamRules,
            openReg.IsIncludeAllDerivedRequested
        );
    }

    /// <summary>
    /// Tüm kayıtlı eşleştirmeleri döndürür (diagnostics için).
    /// </summary>
    internal IReadOnlyCollection<MappingRegistration> GetAllRegistrations()
    {
        return (IReadOnlyCollection<MappingRegistration>)_registrations.Values;
    }

    /// <summary>
    /// Tüm kayıtlı eşleştirmeleri derler ve ön-derlenmiş (precompiled) cache'e ekler (Pre-compilation).
    /// </summary>
    public void CompileMappings()
    {
        foreach (var reg in _registrations.Values)
        {
            if (reg.SourceType.IsGenericTypeDefinition || reg.DestinationType.IsGenericTypeDefinition || reg.CustomConverter != null)
                continue;

            var key = (reg.SourceType, reg.DestinationType);
            if (!_precompiledMappers.ContainsKey(key))
            {
                var stronglyTypedDelegate = Execution.ExpressionBuilder.BuildStronglyTypedMapDelegate(reg.SourceType, reg.DestinationType, this);
                _precompiledMappers[key] = stronglyTypedDelegate;
            }
        }
    }

    /// <summary>
    /// Flattening ile eşleşme mümkün mü kontrol eder.
    /// Örn: "AddressCity" → source.Address.City
    /// </summary>
    private bool CanFlatten(Type sourceType, string destPropertyName)
    {
        // Hedef property adı için tüm temizlenmiş aday isimleri çıkar
        var candidates = Execution.NameMatchingHelper.GetNameCandidates(destPropertyName, DestinationPrefixes, Postfixes, NamingConvention);
        foreach (var candidate in candidates)
        {
            if (TryResolveFlattening(sourceType, candidate, 0))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Rekürsif flattening çözümleme — property adı parçalarını kaynak tipte arar.
    /// </summary>
    private bool TryResolveFlattening(Type type, string remaining, int depth)
    {
        // Sonsuz döngü koruması
        if (depth > 10 || string.IsNullOrEmpty(remaining)) return false;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(ShouldMapProperty).ToArray();

        // Greedy left-to-right matching (AutoMapper algoritması)
        for (int i = 1; i <= remaining.Length; i++)
        {
            var candidate = remaining.Substring(0, i);
            var matchProp = props.FirstOrDefault(p =>
                Execution.NameMatchingHelper.IsMatch(p.Name, candidate, this));

            if (matchProp != null)
            {
                var rest = remaining.Substring(i);

                // Kalan string boşsa — tam eşleşme
                if (rest.Length == 0) return true;

                // Alt property'de devam et
                if (TryResolveFlattening(matchProp.PropertyType, rest, depth + 1))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// İki tür arasında implicit uyumluluk kontrolü yapar.
    /// Enum, Nullable, koleksiyonlar ve kayıtlı özel haritaları dikkate alır.
    /// </summary>
    private bool IsCompatibleType(Type source, Type destination)
    {
        // Doğrudan atanabilir
        if (destination.IsAssignableFrom(source)) return true;

        // Kayıtlı bir eşleştirme var mı?
        if (_registrations.ContainsKey((source, destination))) return true;

        // Kayıtlı özel bir dönüştürücü var mı? (Reviewer bulgusu: DI veya inline olarak eklenen converter'lar)
        if (_converters.ContainsKey((source, destination))) return true;

        // Nullable unwrap
        var srcUnderlying = Nullable.GetUnderlyingType(source) ?? source;
        var dstUnderlying = Nullable.GetUnderlyingType(destination) ?? destination;

        // Aynı tür (nullable unwrap sonrası)
        if (srcUnderlying == dstUnderlying) return true;

        // Kayıtlı eşleştirme (underlying tipler arasında)
        if (_registrations.ContainsKey((srcUnderlying, dstUnderlying))) return true;

        // Kayıtlı özel dönüştürücü (underlying tipler arasında)
        if (_converters.ContainsKey((srcUnderlying, dstUnderlying))) return true;

        // Enum → Enum (farklı enum türleri)
        if (srcUnderlying.IsEnum && dstUnderlying.IsEnum) return true;

        // Enum → string veya string → Enum
        if ((srcUnderlying.IsEnum && dstUnderlying == typeof(string)) ||
            (srcUnderlying == typeof(string) && dstUnderlying.IsEnum))
            return true;

        // Enum → int / int → Enum (underlying type)
        if ((srcUnderlying.IsEnum && dstUnderlying == Enum.GetUnderlyingType(srcUnderlying)) ||
            (dstUnderlying.IsEnum && srcUnderlying == Enum.GetUnderlyingType(dstUnderlying)))
            return true;

        // Koleksiyon tipleri arası dönüşüm kontrolü
        if (Execution.CollectionExpressionHelper.IsCollectionType(source) &&
            Execution.CollectionExpressionHelper.IsCollectionType(destination))
        {
            var srcElement = Execution.CollectionExpressionHelper.GetCollectionElementType(source);
            var dstElement = Execution.CollectionExpressionHelper.GetCollectionElementType(destination);
            if (srcElement != null && dstElement != null)
            {
                return IsCompatibleType(srcElement, dstElement);
            }
        }

        return false;
    }
}
