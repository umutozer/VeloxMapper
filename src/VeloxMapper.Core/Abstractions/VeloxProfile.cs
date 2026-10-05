using System;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;

namespace VeloxMapper;

/// <summary>
/// Eşleştirme tanımlarını ve profil düzeyindeki konvansiyonları gruplayan taban sınıf.
/// Yeni kodda AutoMapper ile aynı isme sahip <see cref="Profile"/> sınıfından türetin; <see cref="VeloxProfile"/>
/// 5.x sürümleriyle geriye dönük uyumluluk için korunur.
/// </summary>
/// <example>
/// <code>
/// public class OrderProfile : Profile
/// {
///     public OrderProfile()
///     {
///         CreateMap&lt;Order, OrderDto&gt;()
///             .ForMember(d =&gt; d.CustomerName, o =&gt; o.MapFrom(s =&gt; s.Customer.Name));
///     }
/// }
/// </code>
/// </example>
public abstract class VeloxProfile : IProfileExpression
{
    private readonly string? _profileName;

    /// <summary>Profili tür adıyla oluşturur.</summary>
    protected VeloxProfile()
    {
        Configuration = new ProfileConfiguration(null);
    }

    /// <summary>Profili verilen adla oluşturur.</summary>
    /// <param name="profileName">Profil adı.</param>
    protected VeloxProfile(string profileName) : this()
    {
        _profileName = profileName;
    }

    /// <summary>Profili verilen adla oluşturur ve yapılandırma eylemini uygular.</summary>
    /// <param name="profileName">Profil adı.</param>
    /// <param name="configurationAction">Profil yapılandırması.</param>
    protected VeloxProfile(string profileName, Action<IProfileExpression> configurationAction) : this(profileName)
    {
        if (configurationAction == null) throw new ArgumentNullException(nameof(configurationAction));
        configurationAction(this);
    }

    /// <summary>İç depo.</summary>
    internal ProfileConfiguration Configuration { get; }

    /// <inheritdoc />
    public virtual string ProfileName => _profileName ?? GetType().FullName ?? GetType().Name;

    // ─── Eşleştirme tanımları ───────────────────────────────────────────────

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        => Configuration.CreateMap<TSource, TDestination>(MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList)
        => Configuration.CreateMap<TSource, TDestination>(memberList);

    /// <inheritdoc />
    public IMappingExpression CreateMap(Type sourceType, Type destinationType)
        => Configuration.CreateMap(sourceType, destinationType, MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList)
        => Configuration.CreateMap(sourceType, destinationType, memberList);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>()
        => Configuration.CreateMap<TSource, TDestination>(MemberList.Destination);

    /// <inheritdoc />
    public IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>(MemberList memberList)
        => Configuration.CreateMap<TSource, TDestination>(memberList);

    // ─── Konvansiyonlar ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ClearPrefixes()
    {
        Configuration.Prefixes.Clear();
        Configuration.PrefixesCleared = true;
    }

    /// <inheritdoc />
    public void RecognizePrefixes(params string[] prefixes) => Configuration.AddPrefixes(Configuration.Prefixes, prefixes);

    /// <inheritdoc />
    public void RecognizePostfixes(params string[] postfixes) => Configuration.AddPrefixes(Configuration.Postfixes, postfixes);

    /// <inheritdoc />
    public void RecognizeDestinationPrefixes(params string[] prefixes) => Configuration.AddPrefixes(Configuration.DestinationPrefixes, prefixes);

    /// <inheritdoc />
    public void RecognizeDestinationPostfixes(params string[] postfixes) => Configuration.AddPrefixes(Configuration.DestinationPostfixes, postfixes);

    /// <inheritdoc />
    public void ReplaceMemberName(string original, string newValue)
    {
        if (string.IsNullOrEmpty(original)) throw new ArgumentNullException(nameof(original));
        Configuration.MemberNameReplacers.Add(new(original, newValue ?? string.Empty));
    }

    /// <inheritdoc />
    public void AddGlobalIgnore(string propertyNameStartingWith)
    {
        if (!string.IsNullOrWhiteSpace(propertyNameStartingWith)) Configuration.GlobalIgnores.Add(propertyNameStartingWith);
    }

    /// <inheritdoc />
    public bool? AllowNullDestinationValues
    {
        get => Configuration.AllowNullDestinationValues;
        set => Configuration.AllowNullDestinationValues = value;
    }

    /// <inheritdoc />
    public bool? AllowNullCollections
    {
        get => Configuration.AllowNullCollections;
        set => Configuration.AllowNullCollections = value;
    }

    /// <inheritdoc />
    public bool? EnableNullPropagationForQueryMapping
    {
        get => Configuration.EnableNullPropagationForQueryMapping;
        set => Configuration.EnableNullPropagationForQueryMapping = value;
    }

    /// <inheritdoc />
    public ICustomNamingConvention? SourceMemberNamingConvention
    {
        get => Configuration.SourceMemberNamingConvention;
        set => Configuration.SourceMemberNamingConvention = value;
    }

    /// <inheritdoc />
    public ICustomNamingConvention? DestinationMemberNamingConvention
    {
        get => Configuration.DestinationMemberNamingConvention;
        set => Configuration.DestinationMemberNamingConvention = value;
    }

    /// <inheritdoc />
    public Func<PropertyInfo, bool>? ShouldMapProperty
    {
        get => Configuration.ShouldMapProperty;
        set => Configuration.ShouldMapProperty = value;
    }

    /// <inheritdoc />
    public Func<FieldInfo, bool>? ShouldMapField
    {
        get => Configuration.ShouldMapField;
        set => Configuration.ShouldMapField = value;
    }

    /// <inheritdoc />
    public Func<MethodInfo, bool>? ShouldMapMethod
    {
        get => Configuration.ShouldMapMethod;
        set => Configuration.ShouldMapMethod = value;
    }

    /// <inheritdoc />
    public Func<ConstructorInfo, bool>? ShouldUseConstructor
    {
        get => Configuration.ShouldUseConstructor;
        set => Configuration.ShouldUseConstructor = value;
    }

    /// <inheritdoc />
    public void DisableConstructorMapping() => Configuration.ConstructorMappingDisabled = true;

    /// <inheritdoc />
    public void IncludeSourceExtensionMethods(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!Configuration.SourceExtensionMethodTypes.Contains(type)) Configuration.SourceExtensionMethodTypes.Add(type);
    }

    /// <inheritdoc />
    public ValueTransformerCollection ValueTransformers => Configuration.ValueTransformers;

    /// <inheritdoc />
    public void ForAllMaps(Action<TypeMap, IMappingExpression> configuration)
        => Configuration.ForAllMapsActions.Add(configuration ?? throw new ArgumentNullException(nameof(configuration)));

    /// <inheritdoc />
    public void ForAllPropertyMaps(Func<PropertyMap, bool> condition, Action<PropertyMap, IMemberConfigurationExpression> memberOptions)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
        Configuration.ForAllPropertyMapsActions.Add(new(condition, memberOptions));
    }
}

/// <summary>
/// Eşleştirme tanımlarını gruplayan profil taban sınıfı. AutoMapper'ın <c>Profile</c> sınıfı ile aynı isim ve üyelere sahiptir;
/// mevcut AutoMapper profilleri yalnızca <c>using</c> satırı değiştirilerek derlenir.
/// </summary>
public abstract class Profile : VeloxProfile
{
    /// <summary>Profili tür adıyla oluşturur.</summary>
    protected Profile()
    {
    }

    /// <summary>Profili verilen adla oluşturur.</summary>
    /// <param name="profileName">Profil adı.</param>
    protected Profile(string profileName) : base(profileName)
    {
    }

    /// <summary>Profili verilen adla oluşturur ve yapılandırma eylemini uygular.</summary>
    /// <param name="profileName">Profil adı.</param>
    /// <param name="configurationAction">Profil yapılandırması.</param>
    protected Profile(string profileName, Action<IProfileExpression> configurationAction) : base(profileName, configurationAction)
    {
    }
}
