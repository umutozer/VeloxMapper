using System;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// Tür bilgisi derleme zamanında bilinmeyen eşleştirmeler (<c>CreateMap(typeof(A&lt;&gt;), typeof(B&lt;&gt;))</c>) ve
/// <c>ForAllMaps</c> için fluent yapılandırma arayüzü. AutoMapper'ın non-generic <c>IMappingExpression</c> arayüzü ile uyumludur.
/// </summary>
public interface IMappingExpression
{
    /// <summary>Ters yönde (hedef → kaynak) bir eşleştirme oluşturur.</summary>
    IMappingExpression ReverseMap();

    /// <summary>Adı verilen hedef üyeyi yapılandırır.</summary>
    IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions);

    /// <summary>Tüm hedef üyelere aynı yapılandırmayı uygular.</summary>
    IMappingExpression ForAllMembers(Action<IMemberConfigurationExpression> memberOptions);

    /// <summary>Açıkça yapılandırılmamış hedef üyelere aynı yapılandırmayı uygular.</summary>
    IMappingExpression ForAllOtherMembers(Action<IMemberConfigurationExpression> memberOptions);

    /// <summary>Adı verilen kaynak üyeyi yapılandırır.</summary>
    IMappingExpression ForSourceMember(string sourceMemberName, Action<ISourceMemberConfigurationExpression> memberOptions);

    /// <summary>Eşleştirmeyi tamamen verilen tip dönüştürücüye devreder (open generic dönüştürücüler desteklenir).</summary>
    IMappingExpression ConvertUsing(Type typeConverterType);

    /// <summary>Türetilmiş tür çiftini polimorfik eşleştirmeye dahil eder.</summary>
    IMappingExpression Include(Type derivedSourceType, Type derivedDestinationType);

    /// <summary>Taban tür çiftinin yapılandırmasını devralır.</summary>
    IMappingExpression IncludeBase(Type sourceBase, Type destinationBase);

    /// <summary>Bu eşleştirmeden türeyen tüm eşleştirmeleri polimorfik eşleştirmeye dahil eder.</summary>
    IMappingExpression IncludeAllDerived();

    /// <summary>Sonucu başka bir hedef türe yönlendirir.</summary>
    IMappingExpression As(Type typeOverride);

    /// <summary>Rekürsif eşleştirme derinliğini sınırlar.</summary>
    IMappingExpression MaxDepth(int depth);

    /// <summary>Döngüsel referanslarda aynı kaynak için aynı hedef örneğini kullanır.</summary>
    IMappingExpression PreserveReferences();

    /// <summary>Doğrulanacak üye listesini belirler.</summary>
    IMappingExpression ValidateMemberList(MemberList memberList);

    /// <summary>Kurucu doğrulamasını devre dışı bırakır.</summary>
    IMappingExpression DisableCtorValidation();

    /// <summary>Setter'ı erişilemez olan hedef property'leri yok sayar.</summary>
    IMappingExpression IgnoreAllPropertiesWithAnInaccessibleSetter();

    /// <summary>Getter'ı erişilemez olan kaynak property'leri kaynak doğrulamasından muaf tutar.</summary>
    IMappingExpression IgnoreAllSourcePropertiesWithAnInaccessibleSetter();
}

/// <summary>
/// <c>CreateMap&lt;TSource, TDestination&gt;()</c> çağrısından dönen fluent yapılandırma arayüzü.
/// AutoMapper'ın <c>IMappingExpression&lt;TSource, TDestination&gt;</c> arayüzü ile aynı metot adlarına ve overload'lara sahiptir.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
public interface IMappingExpression<TSource, TDestination>
{
    // ─── Üye yapılandırması ─────────────────────────────────────────────────

    /// <summary>
    /// Bir hedef üyeyi (property veya field) yapılandırır: <c>.ForMember(d =&gt; d.FullName, o =&gt; o.MapFrom(s =&gt; s.First + " " + s.Last))</c>.
    /// İç içe hedef yolları için <see cref="ForPath{TMember}"/> kullanın.
    /// </summary>
    /// <typeparam name="TMember">Hedef üyenin türü.</typeparam>
    /// <param name="destinationMember">Hedef üye seçicisi.</param>
    /// <param name="memberOptions">Üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

    /// <summary>
    /// Adı verilen hedef üyeyi yapılandırır.
    /// </summary>
    /// <param name="name">Hedef üye adı.</param>
    /// <param name="memberOptions">Üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForMember(
        string name,
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

    /// <summary>
    /// İç içe bir hedef yolunu yapılandırır: <c>.ForPath(d =&gt; d.Customer.Name, o =&gt; o.MapFrom(s =&gt; s.CustomerName))</c>.
    /// Yoldaki ara nesneler <c>null</c> ise oluşturulur.
    /// </summary>
    /// <typeparam name="TMember">Yolun son üyesinin türü.</typeparam>
    /// <param name="destinationMember">Hedef yol seçicisi.</param>
    /// <param name="memberOptions">Üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForPath<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

    /// <summary>
    /// Bir kaynak üyeyi yapılandırır (<c>MemberList.Source</c> doğrulaması için).
    /// </summary>
    /// <param name="sourceMember">Kaynak üye seçicisi.</param>
    /// <param name="memberOptions">Kaynak üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForSourceMember(
        Expression<Func<TSource, object?>> sourceMember,
        Action<ISourceMemberConfigurationExpression> memberOptions);

    /// <summary>
    /// Adı verilen kaynak üyeyi yapılandırır.
    /// </summary>
    /// <param name="sourceMemberName">Kaynak üye adı.</param>
    /// <param name="memberOptions">Kaynak üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForSourceMember(
        string sourceMemberName,
        Action<ISourceMemberConfigurationExpression> memberOptions);

    /// <summary>
    /// Bir kurucu parametresinin değer kaynağını belirler (record ve immutable türler için).
    /// </summary>
    /// <param name="ctorParamName">Parametre adı.</param>
    /// <param name="paramOptions">Parametre yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForCtorParam(
        string ctorParamName,
        Action<ICtorParamConfigurationExpression<TSource>> paramOptions);

    /// <summary>
    /// Tüm hedef üyelere aynı yapılandırmayı uygular. Örnek (PATCH):
    /// <c>.ForAllMembers(o =&gt; o.Condition((src, dest, srcMember) =&gt; srcMember != null))</c>.
    /// </summary>
    /// <param name="memberOptions">Üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForAllMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

    /// <summary>
    /// <c>ForMember</c>/<c>ForPath</c> ile açıkça yapılandırılmamış hedef üyelere aynı yapılandırmayı uygular.
    /// </summary>
    /// <param name="memberOptions">Üye yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ForAllOtherMembers(
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

    /// <summary>
    /// Bir hedef üyeyi yok sayar. <c>ForMember(d =&gt; d.X, o =&gt; o.Ignore())</c> kısayoludur.
    /// </summary>
    /// <param name="destinationMember">Hedef üye seçicisi.</param>
    IMappingExpression<TSource, TDestination> Ignore(Expression<Func<TDestination, object?>> destinationMember);

    /// <summary>
    /// Bu eşleştirmeye özel bir value transformer ekler (ör. tüm string'leri kırpmak için).
    /// </summary>
    /// <typeparam name="TValue">Dönüştürülecek değer türü.</typeparam>
    /// <param name="transformer">Dönüşüm ifadesi.</param>
    IMappingExpression<TSource, TDestination> AddTransform<TValue>(Expression<Func<TValue, TValue>> transformer);

    // ─── Tür dönüştürme ─────────────────────────────────────────────────────

    /// <summary>
    /// Eşleştirmeyi tamamen bir ifadeye devreder. İfade <c>ProjectTo</c> içinde de kullanılır.
    /// </summary>
    /// <param name="mappingExpression">Dönüşüm ifadesi.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> mappingExpression);

    /// <summary>
    /// Eşleştirmeyi tamamen bir fonksiyona devreder: <c>(src, existingDest) =&gt; ...</c>.
    /// </summary>
    /// <param name="mappingFunction">Dönüşüm fonksiyonu.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, TDestination> mappingFunction);

    /// <summary>
    /// Eşleştirmeyi tamamen bir fonksiyona devreder: <c>(src, existingDest, context) =&gt; ...</c>.
    /// </summary>
    /// <param name="mappingFunction">Dönüşüm fonksiyonu.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, ResolutionContext, TDestination> mappingFunction);

    /// <summary>
    /// Eşleştirmeyi tamamen bir tip dönüştürücü örneğine devreder.
    /// </summary>
    /// <param name="converter">Dönüştürücü.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(ITypeConverter<TSource, TDestination> converter);

    /// <summary>
    /// Eşleştirmeyi VeloxMapper 5.x tarzı bir tip dönüştürücü örneğine devreder.
    /// </summary>
    /// <param name="converter">Dönüştürücü.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(IVeloxTypeConverter<TSource, TDestination> converter);

    /// <summary>
    /// Eşleştirmeyi DI konteynerinden çözülen bir tip dönüştürücüye devreder.
    /// </summary>
    /// <typeparam name="TTypeConverter"><see cref="ITypeConverter{TSource,TDestination}"/> uygulayan tür.</typeparam>
    IMappingExpression<TSource, TDestination> ConvertUsing<TTypeConverter>();

    /// <summary>
    /// Eşleştirmeyi türü çalışma zamanında verilen bir tip dönüştürücüye devreder.
    /// </summary>
    /// <param name="typeConverterType">Dönüştürücü türü.</param>
    IMappingExpression<TSource, TDestination> ConvertUsing(Type typeConverterType);

    /// <summary>
    /// Enum'dan enum'a eşleştirmeyi özelleştirir: <c>.ConvertUsingEnumMapping(o =&gt; o.MapByName().MapValue(A.X, B.Y))</c>.
    /// </summary>
    /// <param name="configure">Enum eşleştirme yapılandırması.</param>
    IMappingExpression<TSource, TDestination> ConvertUsingEnumMapping(Action<EnumMappingExpression<TSource, TDestination>> configure);

    // ─── Nesne oluşturma ────────────────────────────────────────────────────

    /// <summary>
    /// Hedef nesneyi verilen fabrika ile oluşturur; üye atamaları ardından uygulanır.
    /// </summary>
    /// <param name="ctor">Fabrika.</param>
    IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> ctor);

    /// <summary>
    /// Hedef nesneyi kaynak ve bağlamı kullanan fabrika ile oluşturur.
    /// </summary>
    /// <param name="ctor">Fabrika.</param>
    IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, ResolutionContext, TDestination> ctor);

    /// <summary>
    /// Hedef nesneyi DI konteynerinden çözer.
    /// </summary>
    IMappingExpression<TSource, TDestination> ConstructUsingServiceLocator();

    // ─── Before / After ─────────────────────────────────────────────────────

    /// <summary>Eşleştirme başlamadan önce çalışacak eylem.</summary>
    IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> beforeFunction);

    /// <summary>Eşleştirme başlamadan önce çalışacak, bağlam alan eylem.</summary>
    IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> beforeFunction);

    /// <summary>Eşleştirme başlamadan önce çalışacak, DI ile çözülen eylem (<see cref="IMappingAction{TSource,TDestination}"/>).</summary>
    IMappingExpression<TSource, TDestination> BeforeMap<TMappingAction>();

    /// <summary>Eşleştirme tamamlandıktan sonra çalışacak eylem.</summary>
    IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> afterFunction);

    /// <summary>Eşleştirme tamamlandıktan sonra çalışacak, bağlam alan eylem.</summary>
    IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> afterFunction);

    /// <summary>Eşleştirme tamamlandıktan sonra çalışacak, DI ile çözülen eylem (<see cref="IMappingAction{TSource,TDestination}"/>).</summary>
    IMappingExpression<TSource, TDestination> AfterMap<TMappingAction>();

    // ─── Kalıtım ────────────────────────────────────────────────────────────

    /// <summary>Türetilmiş tür çiftini polimorfik eşleştirmeye dahil eder.</summary>
    IMappingExpression<TSource, TDestination> Include<TOtherSource, TOtherDestination>()
        where TOtherSource : TSource
        where TOtherDestination : TDestination;

    /// <summary>Türetilmiş tür çiftini polimorfik eşleştirmeye dahil eder.</summary>
    IMappingExpression<TSource, TDestination> Include(Type derivedSourceType, Type derivedDestinationType);

    /// <summary>Taban tür çiftinin yapılandırmasını devralır.</summary>
    IMappingExpression<TSource, TDestination> IncludeBase<TSourceBase, TDestinationBase>();

    /// <summary>Taban tür çiftinin yapılandırmasını devralır.</summary>
    IMappingExpression<TSource, TDestination> IncludeBase(Type sourceBase, Type destinationBase);

    /// <summary>Bu eşleştirmeden türeyen tüm eşleştirmeleri polimorfik eşleştirmeye dahil eder.</summary>
    IMappingExpression<TSource, TDestination> IncludeAllDerived();

    /// <summary>
    /// Kaynağın alt nesnelerindeki üyeleri hedefe düzleştirir: <c>.IncludeMembers(s =&gt; s.Details)</c>.
    /// </summary>
    /// <param name="memberExpressions">Alt nesne seçicileri.</param>
    IMappingExpression<TSource, TDestination> IncludeMembers(params Expression<Func<TSource, object?>>[] memberExpressions);

    /// <summary>Sonucu türetilmiş bir hedef türe yönlendirir.</summary>
    IMappingExpression<TSource, TDestination> As<TOtherDestination>() where TOtherDestination : TDestination;

    // ─── Diğer ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ters yönde (<typeparamref name="TDestination"/> → <typeparamref name="TSource"/>) eşleştirme oluşturur ve onu döndürür.
    /// Basit <c>MapFrom</c> kuralları ve flattening otomatik olarak tersine çevrilir; ters yöne özel kurallar zincire eklenebilir:
    /// <c>.ReverseMap().ForMember(s =&gt; s.X, o =&gt; o.Ignore())</c>.
    /// </summary>
    IMappingExpression<TDestination, TSource> ReverseMap();

    /// <summary>Rekürsif eşleştirme derinliğini sınırlar.</summary>
    IMappingExpression<TSource, TDestination> MaxDepth(int depth);

    /// <summary>Döngüsel referanslarda aynı kaynak için aynı hedef örneğini kullanır.</summary>
    IMappingExpression<TSource, TDestination> PreserveReferences();

    /// <summary>Doğrulanacak üye listesini belirler (<see cref="MemberList"/>).</summary>
    IMappingExpression<TSource, TDestination> ValidateMemberList(MemberList memberList);

    /// <summary>Kurucu parametresi doğrulamasını devre dışı bırakır.</summary>
    IMappingExpression<TSource, TDestination> DisableCtorValidation();

    /// <summary>Setter'ı erişilemez (private/protected) olan hedef property'leri yok sayar.</summary>
    IMappingExpression<TSource, TDestination> IgnoreAllPropertiesWithAnInaccessibleSetter();

    /// <summary>Getter'ı erişilemez olan kaynak property'leri kaynak doğrulamasından muaf tutar.</summary>
    IMappingExpression<TSource, TDestination> IgnoreAllSourcePropertiesWithAnInaccessibleSetter();
}
