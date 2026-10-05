using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Bir <c>CreateMap</c> çağrısının dondurulmuş hali: tür çifti, üye kuralları ve tüm eşleştirme seçenekleri.
/// <c>MapperConfiguration</c> oluşturulduktan sonra değiştirilmez.
/// </summary>
public sealed class MappingRegistration
{
    private readonly Dictionary<string, MemberMappingRule> _memberRules;
    private readonly List<(Type DerivedSource, Type DerivedDestination)> _includedDerivedTypes = new();

    internal MappingRegistration(Type sourceType, Type destinationType, string? profileName)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
        ProfileName = profileName;
        _memberRules = new Dictionary<string, MemberMappingRule>(StringComparer.Ordinal);
    }

    /// <summary>Kaynak tür.</summary>
    public Type SourceType { get; internal set; }

    /// <summary>Hedef tür.</summary>
    public Type DestinationType { get; internal set; }

    /// <summary>Eşleştirmenin tanımlandığı profil (inline tanımlar için <c>null</c>).</summary>
    public string? ProfileName { get; internal set; }

    /// <summary>Hedef üye adına göre üye kuralları.</summary>
    public IReadOnlyDictionary<string, MemberMappingRule> MemberRules => _memberRules;

    internal Dictionary<string, MemberMappingRule> MutableMemberRules => _memberRules;

    // ─── Tür dönüştürme ─────────────────────────────────────────────────────

    /// <summary><c>ConvertUsing</c> ile verilen tip dönüştürücü örneği (<see cref="ITypeConverter{TSource,TDestination}"/> veya <c>IVeloxTypeConverter</c>).</summary>
    public object? CustomConverter { get; internal set; }

    /// <summary><c>ConvertUsing&lt;T&gt;()</c> / <c>ConvertUsing(Type)</c> ile verilen, DI'dan çözülecek dönüştürücü türü.</summary>
    public Type? CustomConverterType { get; internal set; }

    /// <summary><c>ConvertUsing(src =&gt; ...)</c> ifadesi (ProjectTo'da da kullanılır).</summary>
    public LambdaExpression? ConvertUsingExpression { get; internal set; }

    /// <summary><c>ConvertUsing((src, dest[, ctx]) =&gt; ...)</c> fonksiyonu.</summary>
    public Delegate? ConvertUsingFunc { get; internal set; }

    /// <summary>Herhangi bir tür dönüştürücü tanımlıysa <c>true</c>.</summary>
    public bool HasTypeConverter => CustomConverter != null || CustomConverterType != null || ConvertUsingExpression != null || ConvertUsingFunc != null;

    // ─── Nesne oluşturma ────────────────────────────────────────────────────

    /// <summary><c>ConstructUsing(src =&gt; ...)</c> fabrikası (<c>object → object</c>).</summary>
    public Func<object, object>? FactoryDelegate { get; internal set; }

    /// <summary><c>ConstructUsing((src, ctx) =&gt; ...)</c> fabrikası (<c>Func&lt;object, ResolutionContext, object&gt;</c>).</summary>
    public Delegate? FactoryDelegateWithContext { get; internal set; }

    /// <summary><c>ConstructUsingServiceLocator()</c> çağrıldıysa <c>true</c>.</summary>
    public bool ConstructUsingServiceLocator { get; internal set; }

    // ─── Eylemler ve seçenekler ─────────────────────────────────────────────

    /// <summary>BeforeMap eylemleri (delege veya DI ile çözülecek tür).</summary>
    public IReadOnlyList<object> BeforeMapActions { get; internal set; } = Array.Empty<object>();

    /// <summary>AfterMap eylemleri (delege veya DI ile çözülecek tür).</summary>
    public IReadOnlyList<object> AfterMapActions { get; internal set; } = Array.Empty<object>();

    /// <summary>Rekürsif eşleştirme derinliği sınırı (0 = sınırsız).</summary>
    public int MaxDepth { get; internal set; }

    /// <summary>Referans koruması etkinse <c>true</c>.</summary>
    public bool PreserveReferences { get; internal set; }

    /// <summary><c>Include</c> ile eklenen türetilmiş tür çiftleri.</summary>
    public IReadOnlyList<(Type DerivedSource, Type DerivedDestination)> IncludedDerivedTypes => _includedDerivedTypes;

    /// <summary><c>IncludeBase</c> ile belirtilen taban tür çifti.</summary>
    public (Type BaseSource, Type BaseDestination)? BaseTypeMapping { get; internal set; }

    /// <summary><c>IncludeAllDerived()</c> çağrıldıysa <c>true</c>.</summary>
    public bool IsIncludeAllDerivedRequested { get; internal set; }

    /// <summary><c>ForPath</c> kuralları.</summary>
    public IReadOnlyList<ForPathRule> ForPathRules { get; internal set; } = Array.Empty<ForPathRule>();

    /// <summary><c>ForCtorParam</c> kuralları.</summary>
    public IReadOnlyList<CtorParamRule> CtorParamRules { get; internal set; } = Array.Empty<CtorParamRule>();

    /// <summary><c>IncludeMembers</c> ile verilen alt nesne seçicileri.</summary>
    public IReadOnlyList<LambdaExpression>? IncludeMembers { get; internal set; }

    /// <summary><c>As&lt;T&gt;()</c> ile verilen yönlendirme türü.</summary>
    public Type? RedirectDestinationType { get; internal set; }

    /// <summary>Doğrulanacak üye listesi.</summary>
    public MemberList MemberList { get; internal set; } = MemberList.Destination;

    /// <summary><c>MemberList.None</c> ile doğrulama tamamen kapatıldıysa <c>true</c>.</summary>
    public bool ValidationDisabled => MemberList == MemberList.None;

    /// <summary><c>DisableCtorValidation()</c> çağrıldıysa <c>true</c>.</summary>
    public bool CtorValidationDisabled { get; internal set; }

    /// <summary>Kaynak doğrulamasından muaf tutulan kaynak üye adları.</summary>
    public IReadOnlyCollection<string> SourceMembersNotValidated { get; internal set; } = Array.Empty<string>();

    /// <summary>Getter'ı erişilemez kaynak property'ler kaynak doğrulamasından muafsa <c>true</c>.</summary>
    public bool IgnoreSourceMembersWithInaccessibleGetter { get; internal set; }

    /// <summary>Bu eşleştirmeye özel value transformer'lar.</summary>
    public IReadOnlyList<LambdaExpression> ValueTransformers { get; internal set; } = Array.Empty<LambdaExpression>();

    /// <summary><c>ConvertUsingEnumMapping</c> yapılandırması (ReverseMap için saklanır).</summary>
    internal EnumMappingSnapshot? EnumMapping { get; set; }

    /// <summary>Kaydın ait olduğu profilin etkin ayarları (MapperConfiguration tarafından atanır).</summary>
    internal ProfileMap? Profile { get; set; }

    /// <summary>Open generic <c>ForAllMembers</c> eylemleri (tür kapatılırken uygulanır).</summary>
    internal IReadOnlyList<Action<MemberConfigurationExpression>>? OpenGenericForAllMembers { get; set; }

    /// <summary>Open generic <c>ForAllOtherMembers</c> eylemleri (tür kapatılırken uygulanır).</summary>
    internal IReadOnlyList<Action<MemberConfigurationExpression>>? OpenGenericForAllOtherMembers { get; set; }

    /// <summary>ReverseMap ile otomatik üretildiyse <c>true</c>.</summary>
    internal bool IsGeneratedReverse { get; set; }

    internal void AddIncludedDerivedType(Type source, Type destination)
    {
        if (!_includedDerivedTypes.Contains((source, destination))) _includedDerivedTypes.Add((source, destination));
    }

    /// <summary>Tüm alanları kopyalanmış bağımsız bir kayıt üretir (üye kuralları dahil).</summary>
    internal MappingRegistration Clone(Type? sourceType = null, Type? destinationType = null)
    {
        var copy = new MappingRegistration(sourceType ?? SourceType, destinationType ?? DestinationType, ProfileName)
        {
            CustomConverter = CustomConverter,
            CustomConverterType = CustomConverterType,
            ConvertUsingExpression = ConvertUsingExpression,
            ConvertUsingFunc = ConvertUsingFunc,
            FactoryDelegate = FactoryDelegate,
            FactoryDelegateWithContext = FactoryDelegateWithContext,
            ConstructUsingServiceLocator = ConstructUsingServiceLocator,
            BeforeMapActions = BeforeMapActions,
            AfterMapActions = AfterMapActions,
            MaxDepth = MaxDepth,
            PreserveReferences = PreserveReferences,
            BaseTypeMapping = BaseTypeMapping,
            IsIncludeAllDerivedRequested = IsIncludeAllDerivedRequested,
            ForPathRules = ForPathRules,
            CtorParamRules = CtorParamRules,
            IncludeMembers = IncludeMembers,
            RedirectDestinationType = RedirectDestinationType,
            MemberList = MemberList,
            CtorValidationDisabled = CtorValidationDisabled,
            SourceMembersNotValidated = SourceMembersNotValidated,
            IgnoreSourceMembersWithInaccessibleGetter = IgnoreSourceMembersWithInaccessibleGetter,
            ValueTransformers = ValueTransformers,
            EnumMapping = EnumMapping,
            Profile = Profile,
            IsGeneratedReverse = IsGeneratedReverse,
            OpenGenericForAllMembers = OpenGenericForAllMembers,
            OpenGenericForAllOtherMembers = OpenGenericForAllOtherMembers,
        };

        foreach (var kvp in _memberRules) copy._memberRules[kvp.Key] = kvp.Value.Clone();
        foreach (var included in _includedDerivedTypes) copy._includedDerivedTypes.Add(included);
        return copy;
    }

    /// <inheritdoc />
    public override string ToString() => $"{SourceType.Name} -> {DestinationType.Name}" + (ProfileName != null ? $" [{ProfileName}]" : string.Empty);
}
