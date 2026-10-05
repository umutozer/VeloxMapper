using System;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;

namespace VeloxMapper;

/// <summary>
/// Profil düzeyinde (veya global yapılandırmada) eşleştirme tanımlama ve konvansiyon ayarlarını sunar.
/// AutoMapper'ın <c>IProfileExpression</c> arayüzü ile uyumludur; <see cref="Profile"/> ve <c>MapperConfiguration</c>
/// yapılandırma delegesi bu arayüzü uygular.
/// </summary>
public interface IProfileExpression
{
    /// <summary>Profil adı.</summary>
    string ProfileName { get; }

    /// <summary>Bir tür çifti için eşleştirme tanımlar.</summary>
    IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();

    /// <summary>Bir tür çifti için, doğrulanacak üye listesini belirterek eşleştirme tanımlar.</summary>
    IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList);

    /// <summary>Türleri çalışma zamanında verilen (open generic dahil) bir eşleştirme tanımlar.</summary>
    IMappingExpression CreateMap(Type sourceType, Type destinationType);

    /// <summary>Türleri çalışma zamanında verilen, doğrulanacak üye listesi belirtilmiş bir eşleştirme tanımlar.</summary>
    IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList);

    /// <summary>Yalnızca <c>ProjectTo</c> için kullanılacak bir eşleştirme tanımlar (VeloxMapper'da <c>Map</c> için de kullanılabilir).</summary>
    IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>();

    /// <summary>Doğrulanacak üye listesiyle projeksiyon eşleştirmesi tanımlar.</summary>
    IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>(MemberList memberList);

    /// <summary>Kaynak üye adlarında tanınacak ön ekleri temizler (varsayılan <c>Get</c> ön eki dahil).</summary>
    void ClearPrefixes();

    /// <summary>Kaynak üye adlarından atılacak ön ekleri ekler (ör. <c>"Str"</c> → <c>StrName</c> ↔ <c>Name</c>).</summary>
    void RecognizePrefixes(params string[] prefixes);

    /// <summary>Kaynak üye adlarından atılacak son ekleri ekler.</summary>
    void RecognizePostfixes(params string[] postfixes);

    /// <summary>Hedef üye adlarından atılacak ön ekleri ekler.</summary>
    void RecognizeDestinationPrefixes(params string[] prefixes);

    /// <summary>Hedef üye adlarından atılacak son ekleri ekler.</summary>
    void RecognizeDestinationPostfixes(params string[] postfixes);

    /// <summary>Kaynak üye adlarında eşleştirmeden önce metin değiştirme yapar (ör. <c>"Ä"</c> → <c>"A"</c>).</summary>
    void ReplaceMemberName(string original, string newValue);

    /// <summary>Adı verilen metinle başlayan hedef üyeleri tüm eşleştirmelerde yok sayar.</summary>
    void AddGlobalIgnore(string propertyNameStartingWith);

    /// <summary>Kaynak <c>null</c> olduğunda hedefe <c>null</c> yazılmasına izin verir (varsayılan: <c>true</c>).</summary>
    bool? AllowNullDestinationValues { get; set; }

    /// <summary>Kaynak koleksiyon <c>null</c> olduğunda hedefe boş koleksiyon yerine <c>null</c> yazar (varsayılan: <c>false</c>).</summary>
    bool? AllowNullCollections { get; set; }

    /// <summary><c>ProjectTo</c> ifadelerinde ara üyeler için null kontrolü üretir (VeloxMapper varsayılanı: <c>true</c>).</summary>
    bool? EnableNullPropagationForQueryMapping { get; set; }

    /// <summary>Kaynak üye adlarının isimlendirme kuralı (ör. <see cref="LowerUnderscoreNamingConvention"/>).</summary>
    ICustomNamingConvention? SourceMemberNamingConvention { get; set; }

    /// <summary>Hedef üye adlarının isimlendirme kuralı.</summary>
    ICustomNamingConvention? DestinationMemberNamingConvention { get; set; }

    /// <summary>Hangi property'lerin eşleştirmeye katılacağını belirler (varsayılan: getter veya setter'ı public olanlar).</summary>
    Func<PropertyInfo, bool>? ShouldMapProperty { get; set; }

    /// <summary>Hangi field'ların eşleştirmeye katılacağını belirler (varsayılan: public field'lar).</summary>
    Func<FieldInfo, bool>? ShouldMapField { get; set; }

    /// <summary>Hangi parametresiz kaynak metotlarının (ör. <c>GetTotal()</c>) kaynak üye olarak kullanılacağını belirler.</summary>
    Func<MethodInfo, bool>? ShouldMapMethod { get; set; }

    /// <summary>Hedef türün hangi kurucularının kullanılabileceğini belirler.</summary>
    Func<ConstructorInfo, bool>? ShouldUseConstructor { get; set; }

    /// <summary>Kurucu parametresi eşleştirmesini kapatır; hedef nesneler parametresiz kurucu ile oluşturulur.</summary>
    void DisableConstructorMapping();

    /// <summary>Verilen statik sınıftaki extension metotlarını kaynak üye olarak kullanır (ör. <c>GetFullName(this Customer c)</c> → <c>FullName</c>).</summary>
    void IncludeSourceExtensionMethods(Type type);

    /// <summary>Bu profildeki tüm değerlere uygulanacak value transformer'lar.</summary>
    ValueTransformerCollection ValueTransformers { get; }

    /// <summary>Bu profildeki tüm eşleştirmelere ortak yapılandırma uygular.</summary>
    void ForAllMaps(Action<TypeMap, IMappingExpression> configuration);

    /// <summary>Koşulu sağlayan tüm üye eşleştirmelerine ortak yapılandırma uygular.</summary>
    void ForAllPropertyMaps(Func<PropertyMap, bool> condition, Action<PropertyMap, IMemberConfigurationExpression> memberOptions);
}

/// <summary>
/// <c>MapperConfiguration</c> yapılandırma delegesinin arayüzü. AutoMapper'ın <c>IMapperConfigurationExpression</c> arayüzü ile uyumludur.
/// </summary>
public interface IMapperConfigurationExpression : IProfileExpression
{
    /// <summary>Profil örneği ekler.</summary>
    void AddProfile(VeloxProfile profile);

    /// <summary>Profil türü ekler.</summary>
    void AddProfile<TProfile>() where TProfile : VeloxProfile, new();

    /// <summary>Profil türü ekler.</summary>
    void AddProfile(Type profileType);

    /// <summary>Birden fazla profil örneği ekler.</summary>
    void AddProfiles(System.Collections.Generic.IEnumerable<VeloxProfile> profiles);

    /// <summary>Assembly'lerdeki profilleri ve <c>[AutoMap]</c> özniteliklerini tarar.</summary>
    void AddMaps(params Assembly[] assembliesToScan);

    /// <summary>Assembly'lerdeki profilleri ve <c>[AutoMap]</c> özniteliklerini tarar.</summary>
    void AddMaps(System.Collections.Generic.IEnumerable<Assembly> assembliesToScan);

    /// <summary>Verilen türlerin bulunduğu assembly'leri tarar.</summary>
    void AddMaps(params Type[] typesFromAssembliesContainingMappingDefinitions);

    /// <summary>Verilen türlerin bulunduğu assembly'leri tarar.</summary>
    void AddMaps(System.Collections.Generic.IEnumerable<Type> typesFromAssembliesContainingMappingDefinitions);

    /// <summary>Adı verilen assembly'leri yükleyip tarar.</summary>
    void AddMaps(params string[] assemblyNamesToScan);

    /// <summary>Adı verilen assembly'leri yükleyip tarar.</summary>
    void AddMaps(System.Collections.Generic.IEnumerable<string> assemblyNamesToScan);

    /// <summary>Resolver, converter ve action örneklerinin nasıl oluşturulacağını belirler.</summary>
    void ConstructServicesUsing(Func<Type, object> constructor);

    /// <summary>Satır içi adlandırılmış bir profil oluşturur.</summary>
    void CreateProfile(string profileName, Action<IProfileExpression> config);

    /// <summary>
    /// AutoMapper 15+ uyumluluğu için kabul edilir ve yok sayılır. VeloxMapper MIT lisanslıdır; lisans anahtarı gerekmez.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    string? LicenseKey { get; set; }
}
