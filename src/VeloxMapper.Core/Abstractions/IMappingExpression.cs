using System;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// Non-generic IMappingExpression arayüzü. Open generic kayıtlar için fluent zincir desteği sunar.
/// </summary>
public interface IMappingExpression
{
}

/// <summary>
/// <c>CreateMap&lt;TSource, TDest&gt;()</c> çağrısından dönen fluent API arayüzü.
/// ForMember, Ignore, ConvertUsing, ReverseMap, ConstructUsing ve ForAllMembers
/// yapılandırma metotlarını sunar.
/// AutoMapper'ın <c>IMappingExpression&lt;TSource, TDestination&gt;</c> ile API uyumludur.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
/// <typeparam name="TDestination">Hedef tür</typeparam>
public interface IMappingExpression<TSource, TDestination> : IMappingExpression
{
    /// <summary>
    /// Belirli bir hedef property için özel eşleştirme kuralı tanımlar.
    /// AutoMapper uyumlu ForMember pattern'i kullanır.
    /// </summary>
    /// <typeparam name="TMember">Hedef property'nin tipi</typeparam>
    /// <param name="destinationMember">Hedef property seçici (dest =&gt; dest.PropertyName)</param>
    /// <param name="memberOptions">Üye yapılandırma eylemi (opt =&gt; opt.MapFrom(...) veya opt.Ignore())</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

    /// <summary>
    /// Tüm otomatik eşleştirmeyi devre dışı bırakarak bu çift için özel dönüştürücü kullanır.
    /// </summary>
    /// <param name="converter">Özel tip dönüştürücü implementasyonu</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ConvertUsing(
        IVeloxTypeConverter<TSource, TDestination> converter);

    /// <summary>
    /// Tüm otomatik eşleştirmeyi devre dışı bırakarak bu çift için verilen lambda/fonksiyon dönüştürücüsünü kullanır.
    /// </summary>
    /// <param name="mappingFunction">Özel dönüşüm fonksiyonu</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ConvertUsing(
        Func<TSource?, TDestination> mappingFunction);

    /// <summary>
    /// Belirli bir hedef property'yi eşleştirme dışında bırakır.
    /// <c>ForMember(dest =&gt; dest.X, opt =&gt; opt.Ignore())</c> için kısayol.
    /// </summary>
    /// <param name="destinationMember">Yok sayılacak hedef property seçici</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> Ignore(
        Expression<Func<TDestination, object?>> destinationMember);

    // ─────────────────────────────────────────────────────────────────────────
    // AutoMapper Geçiş Uyumluluk API'si
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ters yönde (<typeparamref name="TDestination"/> → <typeparamref name="TSource"/>)
    /// otomatik isim eşleşmeli haritalama kaydı oluşturur.
    /// AutoMapper'ın <c>.ReverseMap()</c> ile birebir uyumludur.
    /// <para>
    /// Ters yöne özel ForMember kuralları tanımlamak için
    /// ayrıca <c>CreateMap&lt;TDestination, TSource&gt;()</c> kullanın.
    /// </para>
    /// </summary>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ReverseMap();

    /// <summary>
    /// Hedef nesneyi varsayılan constructor yerine verilen factory delegate ile oluşturur.
    /// AutoMapper'ın <c>.ConstructUsing()</c> ile birebir uyumludur.
    /// <para>
    /// Parametreli constructor, private setter veya DI bağımlılığı gerektiren
    /// Entity sınıfları için kullanışlıdır.
    /// Property atamaları factory sonrasında da uygulanmaya devam eder.
    /// </para>
    /// </summary>
    /// <param name="factory">Kaynak nesneden hedef nesne üreten factory</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ConstructUsing(
        Func<TSource, TDestination> factory);

    /// <summary>
    /// Tüm property atamaları için toplu kural uygular.
    /// AutoMapper'ın <c>.ForAllMembers()</c> ile birebir uyumludur.
    /// <para>
    /// En yaygın kullanım — null-ignore (PATCH pattern'ı):
    /// <code>
    /// CreateMap&lt;UpdateDto, Entity&gt;()
    ///     .ForAllMembers(opt => opt.Condition((src, dest, v) => v != null));
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="memberOptions">Toplu kural yapılandırma eylemi</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ForAllMembers(
        Action<IForAllMembersExpression<TSource, TDestination>> memberOptions);

    /// <summary>
    /// Açıkça konfigüre edilmemiş tüm property atamaları için toplu kural uygular.
    /// AutoMapper'ın <c>.ForAllOtherMembers()</c> ile birebir uyumludur.
    /// </summary>
    IMappingExpression<TSource, TDestination> ForAllOtherMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

    /// <summary>
    /// Eşleştirme işlemi başlamadan önce çalıştırılacak DI-destekli bir eylem tanımlar.
    /// </summary>
    /// <typeparam name="TAction">IVeloxMappingAction türü</typeparam>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> BeforeMap<TAction>()
        where TAction : IVeloxMappingAction<TSource, TDestination>;

    /// <summary>
    /// Eşleştirme işlemi başlamadan önce çalıştırılacak inline (satır içi) bir eylem tanımlar.
    /// </summary>
    /// <param name="action">Eylem delegesi</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action);

    /// <summary>
    /// Eşleştirme işlemi tamamlandıktan sonra çalıştırılacak DI-destekli bir eylem tanımlar.
    /// </summary>
    /// <typeparam name="TAction">IVeloxMappingAction türü</typeparam>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> AfterMap<TAction>()
        where TAction : IVeloxMappingAction<TSource, TDestination>;

    /// <summary>
    /// Eşleştirme işlemi tamamlandıktan sonra çalıştırılacak inline (satır içi) bir eylem tanımlar.
    /// </summary>
    /// <param name="action">Eylem delegesi</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action);

    /// <summary>
    /// Rekürsif mapping derinliğini sınırlar. Self-referencing türlerde StackOverflow'u önler.
    /// </summary>
    IMappingExpression<TSource, TDestination> MaxDepth(int depth);

    /// <summary>
    /// Döngüsel referanslarda aynı kaynak nesnesini tekrar map etmek yerine önceki sonucu döndürür.
    /// </summary>
    IMappingExpression<TSource, TDestination> PreserveReferences();

    /// <summary>
    /// Derived tür çifti için polimorfik haritalama kaydı tanımlar. Runtime'da kaynak nesne
    /// TDerivedSource türündeyse otomatik olarak TDerivedDestination'a map edilir.
    /// </summary>
    /// <typeparam name="TDerivedSource">Alt kaynak tür</typeparam>
    /// <typeparam name="TDerivedDestination">Alt hedef tür</typeparam>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination;

    /// <summary>
    /// Base tür çiftinin kurallarını bu haritalamaya miras alır.
    /// </summary>
    /// <typeparam name="TBaseSource">Üst kaynak tür</typeparam>
    /// <typeparam name="TBaseDestination">Üst hedef tür</typeparam>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>();

    /// <summary>
    /// Bu eşleştirmenin tüm alt (derived) sınıf eşleştirmelerini otomatik olarak
    /// polimorfik haritalamaya (Include) dahil eder.
    /// AutoMapper'ın <c>.IncludeAllDerived()</c> ile birebir uyumludur.
    /// </summary>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> IncludeAllDerived();

    /// <summary>
    /// Bu eşleştirmenin sonucunu başka bir hedef tipe yönlendirir.
    /// AutoMapper'ın <c>.As&lt;TDestinationRedirect&gt;()</c> ile birebir uyumludur.
    /// </summary>
    /// <typeparam name="TDestinationRedirect">Yönlendirilecek hedef tür</typeparam>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> As<TDestinationRedirect>()
        where TDestinationRedirect : TDestination;

    /// <summary>
    /// Derin yol üzerinden property eşleştirme kuralı tanımlar. Nested nesnelere mapping için kullanılır.
    /// Örnek: ForPath(d => d.Customer.Name, opt => opt.MapFrom(s => s.CustomerName))
    /// </summary>
    /// <typeparam name="TMember">Hedef property'nin tipi</typeparam>
    /// <param name="destinationPath">Hedef property yol seçici (dest => dest.Nested.Property)</param>
    /// <param name="memberOptions">Üye yapılandırma eylemi (opt => opt.MapFrom(...) veya opt.Ignore())</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationPath,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

    /// <summary>
    /// Constructor parametresine özel kaynak eşleştirmesi tanımlar. Record ve immutable sınıflar için kullanılır.
    /// </summary>
    /// <param name="ctorParamName">Constructor parametresinin adı</param>
    /// <param name="paramOptions">Parametre yapılandırma eylemi</param>
    /// <returns>Fluent zincirleme için aynı ifade örneği</returns>
    IMappingExpression<TSource, TDestination> ForCtorParam(
        string ctorParamName,
        Action<ICtorParamConfigurationExpression<TSource>> paramOptions);

    /// <summary>
    /// Enum'lar arası eşleştirmeyi özelleştirmek için kullanılır.
    /// AutoMapper'ın <c>.ConvertUsingEnumMapping()</c> ile birebir uyumludur.
    /// </summary>
    IMappingExpression<TSource, TDestination> ConvertUsingEnumMapping(
        Action<EnumMappingExpression<TSource, TDestination>> configure);
}
