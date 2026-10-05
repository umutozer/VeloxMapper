using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;
using VeloxMapper.Configuration.Annotations;

namespace VeloxMapper;

/// <summary>
/// Mevcut nesneye eşleme (<c>mapper.Map(source, destination)</c>) için VeloxMapper'a özgü seçenekler.
/// </summary>
public sealed class PatchMappingOptions
{
    /// <summary>
    /// True ise mevcut nesneye eşlemede kaynak değeri <c>null</c> olan üyeler hedefe yazılmaz (kısmi güncelleme / PATCH).
    /// Varsayılan: <c>false</c> (AutoMapper davranışı: null değer hedefe yazılır).
    /// </summary>
    public bool IgnoreNullValues { get; set; }
}

/// <summary>
/// <c>new MapperConfiguration(cfg =&gt; ...)</c> ve <c>services.AddVeloxMapper(cfg =&gt; ...)</c> yapılandırma delegesinin parametresi.
/// AutoMapper'ın <c>IMapperConfigurationExpression</c> arayüzünü uygular; mevcut AutoMapper yapılandırma kodu değişmeden derlenir.
/// </summary>
public class VeloxMapperOptions : IMapperConfigurationExpression
{
    private readonly List<VeloxProfile> _profiles = new();
    private readonly HashSet<Type> _profileTypes = new();
    private readonly HashSet<Assembly> _scannedAssemblies = new();
    private readonly List<Action<MappingRegistration>> _registrationActions = new();

    /// <summary>Global (profil dışı) yapılandırma deposu.</summary>
    internal ProfileConfiguration Global { get; } = new(null);

    internal IReadOnlyList<VeloxProfile> Profiles => _profiles;

    internal IReadOnlyList<Action<MappingRegistration>> RegistrationActions => _registrationActions;

    internal Func<Type, object>? ServiceCtor { get; private set; }

    // ─── VeloxMapper'a özgü seçenekler ──────────────────────────────────────

    /// <summary>Mevcut nesneye eşleme seçenekleri.</summary>
    public PatchMappingOptions PatchMapping { get; } = new();

    /// <summary>İfade üretimi ve teşhis olayları için isteğe bağlı log hedefi.</summary>
    public IVeloxDiagnosticsSink? DiagnosticsSink { get; set; }

    /// <summary>Hem kaynak hem hedef üye adlarına uygulanacak isimlendirme kuralı (taraf bazlı ayar yoksa).</summary>
    public ICustomNamingConvention? NamingConvention { get; set; }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? LicenseKey { get; set; }

    /// <inheritdoc />
    public string ProfileName => "Global";

    // ─── Eşleştirme tanımları ───────────────────────────────────────────────

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>() => Global.CreateMap<TSource, TDestination>(MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList) => Global.CreateMap<TSource, TDestination>(memberList);

    /// <inheritdoc />
    public IMappingExpression CreateMap(Type sourceType, Type destinationType) => Global.CreateMap(sourceType, destinationType, MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList) => Global.CreateMap(sourceType, destinationType, memberList);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>() => Global.CreateMap<TSource, TDestination>(MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>(MemberList memberList) => Global.CreateMap<TSource, TDestination>(memberList);

    /// <summary>
    /// Bir tür çifti için özel tip dönüştürücü kaydeder (<c>CreateMap&lt;TSource, TDestination&gt;().ConvertUsing(converter)</c> kısayolu).
    /// </summary>
    public void AddCustomConverter<TSource, TDestination>(IVeloxTypeConverter<TSource, TDestination> converter)
        => CreateMap<TSource, TDestination>().ConvertUsing(converter ?? throw new ArgumentNullException(nameof(converter)));

    /// <summary>
    /// Bir tür çifti için özel tip dönüştürücü kaydeder.
    /// </summary>
    public void AddCustomConverter<TSource, TDestination>(ITypeConverter<TSource, TDestination> converter)
        => CreateMap<TSource, TDestination>().ConvertUsing(converter ?? throw new ArgumentNullException(nameof(converter)));

    // ─── Profiller ve assembly taraması ─────────────────────────────────────

    /// <inheritdoc />
    public void AddProfile(VeloxProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        profile.Configuration.Name = profile.ProfileName;
        _profiles.Add(profile);
    }

    /// <inheritdoc />
    public void AddProfile<TProfile>() where TProfile : VeloxProfile, new()
    {
        if (_profileTypes.Add(typeof(TProfile))) AddProfile(new TProfile());
    }

    /// <inheritdoc />
    public void AddProfile(Type profileType)
    {
        if (profileType == null) throw new ArgumentNullException(nameof(profileType));
        if (!typeof(VeloxProfile).IsAssignableFrom(profileType))
            throw new ArgumentException($"'{profileType.FullName}' bir Profile alt sınıfı değil.", nameof(profileType));
        if (_profileTypes.Add(profileType)) AddProfile((VeloxProfile)Activator.CreateInstance(profileType, nonPublic: true)!);
    }

    /// <inheritdoc />
    public void AddProfiles(IEnumerable<VeloxProfile> profiles)
    {
        if (profiles == null) throw new ArgumentNullException(nameof(profiles));
        foreach (var profile in profiles) AddProfile(profile);
    }

    /// <inheritdoc />
    public void AddMaps(params Assembly[] assembliesToScan) => AddMaps((IEnumerable<Assembly>)assembliesToScan);

    /// <inheritdoc />
    public void AddMaps(IEnumerable<Assembly> assembliesToScan)
    {
        if (assembliesToScan == null) throw new ArgumentNullException(nameof(assembliesToScan));
        foreach (var assembly in assembliesToScan) ScanAssembly(assembly);
    }

    /// <inheritdoc />
    public void AddMaps(params Type[] typesFromAssembliesContainingMappingDefinitions)
        => AddMaps((IEnumerable<Type>)typesFromAssembliesContainingMappingDefinitions);

    /// <inheritdoc />
    public void AddMaps(IEnumerable<Type> typesFromAssembliesContainingMappingDefinitions)
    {
        if (typesFromAssembliesContainingMappingDefinitions == null) throw new ArgumentNullException(nameof(typesFromAssembliesContainingMappingDefinitions));
        AddMaps(typesFromAssembliesContainingMappingDefinitions.Where(t => t != null).Select(t => t.Assembly));
    }

    /// <inheritdoc />
    public void AddMaps(params string[] assemblyNamesToScan) => AddMaps((IEnumerable<string>)assemblyNamesToScan);

    /// <inheritdoc />
    public void AddMaps(IEnumerable<string> assemblyNamesToScan)
    {
        if (assemblyNamesToScan == null) throw new ArgumentNullException(nameof(assemblyNamesToScan));
        AddMaps(assemblyNamesToScan.Select(Assembly.Load));
    }

    /// <summary>Assembly'deki profilleri ve <c>[AutoMap]</c> özniteliklerini tarar (<see cref="AddMaps(Assembly[])"/> ile aynı).</summary>
    public void AddProfilesFromAssembly(Assembly assembly) => ScanAssembly(assembly);

    /// <summary>Verilen türün bulunduğu assembly'yi tarar.</summary>
    public void AddProfilesFromAssemblyOf<T>() => ScanAssembly(typeof(T).Assembly);

    /// <summary>Birden fazla assembly'yi tarar.</summary>
    public void AddProfilesFromAssemblies(params Assembly[] assemblies) => AddMaps(assemblies);

    /// <inheritdoc />
    public void ConstructServicesUsing(Func<Type, object> constructor) => ServiceCtor = constructor ?? throw new ArgumentNullException(nameof(constructor));

    /// <inheritdoc />
    public void CreateProfile(string profileName, Action<IProfileExpression> config)
    {
        if (string.IsNullOrWhiteSpace(profileName)) throw new ArgumentNullException(nameof(profileName));
        AddProfile(new InlineProfile(profileName, config ?? throw new ArgumentNullException(nameof(config))));
    }

    private void ScanAssembly(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsDynamic || assembly == typeof(VeloxProfile).Assembly || !_scannedAssemblies.Add(assembly)) return;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (typeof(VeloxProfile).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters &&
                type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null) != null)
            {
                AddProfile(type);
            }
        }

        foreach (var type in types)
        {
            foreach (var attribute in type.GetCustomAttributes<AutoMapAttribute>(inherit: false))
            {
                RegisterAutoMap(attribute, type);
            }
        }
    }

    private void RegisterAutoMap(AutoMapAttribute attribute, Type destinationType)
    {
        var map = CreateMap(attribute.SourceType, destinationType);
        if (attribute.MaxDepth > 0) map.MaxDepth(attribute.MaxDepth);
        if (attribute.PreserveReferences) map.PreserveReferences();
        if (attribute.IncludeAllDerived) map.IncludeAllDerived();
        if (attribute.DisableCtorValidation) map.DisableCtorValidation();
        if (attribute.TypeConverter != null) map.ConvertUsing(attribute.TypeConverter);
        if (attribute.ConstructUsingServiceLocator)
            map.GetType().GetMethod(nameof(IMappingExpression<object, object>.ConstructUsingServiceLocator))?.Invoke(map, null);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var member in destinationType.GetProperties(flags).Cast<MemberInfo>().Concat(destinationType.GetFields(flags)))
        {
            var attributes = member.GetCustomAttributes(inherit: true).OfType<Attribute>().ToList();
            if (attributes.Count == 0) continue;

            map.ForMember(member.Name, opt =>
            {
                var rule = ((MemberConfigurationExpression)opt).Rule;
                foreach (var memberAttribute in attributes)
                {
                    switch (memberAttribute)
                    {
                        case IgnoreAttribute:
                            opt.Ignore();
                            break;
                        case SourceMemberAttribute sourceMember:
                            if (!rule.HasValueConverter) opt.MapFrom(sourceMember.Name);
                            else rule.ValueConverterSourceMemberName = sourceMember.Name;
                            break;
                        case NullSubstituteAttribute nullSubstitute:
                            opt.NullSubstitute(nullSubstitute.Value);
                            break;
                        case ValueResolverAttribute resolver:
                            opt.MapFrom(resolver.Type);
                            break;
                        case ValueConverterAttribute converter:
                            var sourceName = rule.SourceMemberPath;
                            rule.ClearValueSource();
                            rule.ValueConverterType = converter.Type;
                            rule.ValueConverterSourceMemberName = sourceName;
                            break;
                        case UseExistingValueAttribute:
                            opt.UseDestinationValue();
                            break;
                        case MappingOrderAttribute order:
                            opt.SetMappingOrder(order.Value);
                            break;
                    }
                }
            });
        }

        if (attribute.ReverseMap) map.ReverseMap();
    }

    // ─── Konvansiyonlar (IProfileExpression) ────────────────────────────────

    /// <inheritdoc />
    public void ClearPrefixes()
    {
        Global.Prefixes.Clear();
        Global.DestinationPrefixes.Clear();
        Global.PrefixesCleared = true;
    }

    /// <inheritdoc />
    public void RecognizePrefixes(params string[] prefixes) => Global.AddPrefixes(Global.Prefixes, prefixes);

    /// <inheritdoc />
    public void RecognizePostfixes(params string[] postfixes) => Global.AddPrefixes(Global.Postfixes, postfixes);

    /// <inheritdoc />
    public void RecognizeDestinationPrefixes(params string[] prefixes) => Global.AddPrefixes(Global.DestinationPrefixes, prefixes);

    /// <inheritdoc />
    public void RecognizeDestinationPostfixes(params string[] postfixes) => Global.AddPrefixes(Global.DestinationPostfixes, postfixes);

    /// <inheritdoc />
    public void ReplaceMemberName(string original, string newValue)
    {
        if (string.IsNullOrEmpty(original)) throw new ArgumentNullException(nameof(original));
        Global.MemberNameReplacers.Add(new(original, newValue ?? string.Empty));
    }

    /// <inheritdoc />
    public void AddGlobalIgnore(string propertyNameStartingWith)
    {
        if (!string.IsNullOrWhiteSpace(propertyNameStartingWith)) Global.GlobalIgnores.Add(propertyNameStartingWith);
    }

    /// <summary>Verilen adlarla başlayan hedef üyeleri tüm eşleştirmelerde yok sayar.</summary>
    public void AddGlobalIgnore(params string[] propertyNamesStartingWith)
    {
        if (propertyNamesStartingWith == null) return;
        foreach (var name in propertyNamesStartingWith) AddGlobalIgnore(name);
    }

    /// <inheritdoc />
    public bool? AllowNullDestinationValues
    {
        get => Global.AllowNullDestinationValues;
        set => Global.AllowNullDestinationValues = value;
    }

    /// <inheritdoc />
    public bool? AllowNullCollections
    {
        get => Global.AllowNullCollections;
        set => Global.AllowNullCollections = value;
    }

    /// <inheritdoc />
    public bool? EnableNullPropagationForQueryMapping
    {
        get => Global.EnableNullPropagationForQueryMapping;
        set => Global.EnableNullPropagationForQueryMapping = value;
    }

    /// <inheritdoc />
    public ICustomNamingConvention? SourceMemberNamingConvention
    {
        get => Global.SourceMemberNamingConvention;
        set => Global.SourceMemberNamingConvention = value;
    }

    /// <inheritdoc />
    public ICustomNamingConvention? DestinationMemberNamingConvention
    {
        get => Global.DestinationMemberNamingConvention;
        set => Global.DestinationMemberNamingConvention = value;
    }

    /// <inheritdoc />
    public Func<PropertyInfo, bool>? ShouldMapProperty
    {
        get => Global.ShouldMapProperty;
        set => Global.ShouldMapProperty = value;
    }

    /// <inheritdoc />
    public Func<FieldInfo, bool>? ShouldMapField
    {
        get => Global.ShouldMapField;
        set => Global.ShouldMapField = value;
    }

    /// <inheritdoc />
    public Func<MethodInfo, bool>? ShouldMapMethod
    {
        get => Global.ShouldMapMethod;
        set => Global.ShouldMapMethod = value;
    }

    /// <inheritdoc />
    public Func<ConstructorInfo, bool>? ShouldUseConstructor
    {
        get => Global.ShouldUseConstructor;
        set => Global.ShouldUseConstructor = value;
    }

    /// <inheritdoc />
    public void DisableConstructorMapping() => Global.ConstructorMappingDisabled = true;

    /// <inheritdoc />
    public void IncludeSourceExtensionMethods(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!Global.SourceExtensionMethodTypes.Contains(type)) Global.SourceExtensionMethodTypes.Add(type);
    }

    /// <inheritdoc />
    public ValueTransformerCollection ValueTransformers => Global.ValueTransformers;

    /// <inheritdoc />
    public void ForAllMaps(Action<TypeMap, IMappingExpression> configuration)
        => Global.ForAllMapsActions.Add(configuration ?? throw new ArgumentNullException(nameof(configuration)));

    /// <summary>
    /// Tüm dondurulmuş kayıtlar üzerinde çalışan VeloxMapper 5.x tarzı toplu yapılandırma.
    /// Yeni kodda <see cref="ForAllMaps(Action{TypeMap, IMappingExpression})"/> kullanın.
    /// </summary>
    public void ForAllMaps(Action<MappingRegistration> configure)
        => _registrationActions.Add(configure ?? throw new ArgumentNullException(nameof(configure)));

    /// <inheritdoc />
    public void ForAllPropertyMaps(Func<PropertyMap, bool> condition, Action<PropertyMap, IMemberConfigurationExpression> memberOptions)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        Global.ForAllPropertyMapsActions.Add(new(condition, memberOptions));
    }

    private sealed class InlineProfile : VeloxProfile
    {
        public InlineProfile(string profileName, Action<IProfileExpression> configurationAction) : base(profileName, configurationAction)
        {
        }
    }
}

/// <summary>
/// AutoMapper'daki <c>MapperConfigurationExpression</c> sınıfının karşılığı:
/// <c>var cfg = new MapperConfigurationExpression(); cfg.CreateMap&lt;A, B&gt;(); new MapperConfiguration(cfg);</c>
/// </summary>
public class MapperConfigurationExpression : VeloxMapperOptions
{
}
