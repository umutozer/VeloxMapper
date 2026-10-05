using System;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// <c>ForMember</c>, <c>ForPath</c>, <c>ForAllMembers</c> ve <c>ForAllOtherMembers</c> çağrılarında tek bir hedef üyeyi yapılandırır.
/// AutoMapper'ın <c>IMemberConfigurationExpression&lt;TSource, TDestination, TMember&gt;</c> arayüzü ile aynı overload'lara sahiptir.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
/// <typeparam name="TMember">Hedef üyenin türü.</typeparam>
public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    /// <summary>
    /// Yapılandırılan hedef üye (ForAllMembers içinde üyeye göre karar vermek için kullanılabilir).
    /// </summary>
    MemberInfo DestinationMember { get; }

    /// <summary>
    /// Hedef üyenin değerini bir kaynak ifadesinden alır. İfadenin türü hedef üyeden farklı olabilir; değer
    /// eşleştirme kurallarıyla (iç içe eşleştirme, koleksiyon, enum, sayısal/string dönüşüm) hedef türe çevrilir.
    /// İfadedeki ara üyeler otomatik olarak null-güvenlidir (<c>s.Customer.Name</c> → Customer null ise null).
    /// </summary>
    /// <typeparam name="TSourceMember">İfadenin döndürdüğü tür.</typeparam>
    /// <param name="mapExpression">Kaynak ifadesi, ör. <c>src =&gt; src.Customer.Name</c>.</param>
    void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> mapExpression);

    /// <summary>
    /// Hedef üyenin değerini kaynak üye adından (veya <c>"Address.City"</c> gibi noktalı yoldan) alır.
    /// </summary>
    /// <param name="sourceMembersPath">Kaynak üye adı veya yolu.</param>
    void MapFrom(string sourceMembersPath);

    /// <summary>
    /// Hedef üyenin değerini kaynak ve hedef nesneden hesaplar.
    /// </summary>
    /// <typeparam name="TResult">Fonksiyonun döndürdüğü tür.</typeparam>
    /// <param name="mappingFunction">Değer üreten fonksiyon: <c>(src, dest) =&gt; ...</c>.</param>
    void MapFrom<TResult>(Func<TSource, TDestination, TResult> mappingFunction);

    /// <summary>
    /// Hedef üyenin değerini kaynak, hedef ve hedef üyenin mevcut değerinden hesaplar.
    /// </summary>
    /// <typeparam name="TResult">Fonksiyonun döndürdüğü tür.</typeparam>
    /// <param name="mappingFunction">Değer üreten fonksiyon: <c>(src, dest, destMember) =&gt; ...</c>.</param>
    void MapFrom<TResult>(Func<TSource, TDestination, TMember, TResult> mappingFunction);

    /// <summary>
    /// Hedef üyenin değerini kaynak, hedef, hedef üyenin mevcut değeri ve çalışma zamanı bağlamından hesaplar.
    /// </summary>
    /// <typeparam name="TResult">Fonksiyonun döndürdüğü tür.</typeparam>
    /// <param name="mappingFunction">Değer üreten fonksiyon: <c>(src, dest, destMember, context) =&gt; ...</c>.</param>
    void MapFrom<TResult>(Func<TSource, TDestination, TMember, ResolutionContext, TResult> mappingFunction);

    /// <summary>
    /// Hedef üyenin değerini bir value resolver ile üretir. Resolver; <see cref="IValueResolver{TSource,TDestination,TDestMember}"/>
    /// (veya <see cref="IVeloxValueResolver{TSource,TDestination,TDestMember}"/>) uygulamalıdır ve DI konteynerinden çözülür.
    /// </summary>
    /// <typeparam name="TValueResolver">Resolver türü.</typeparam>
    void MapFrom<TValueResolver>();

    /// <summary>
    /// Hedef üyenin değerini, bir kaynak üyeyi girdi olarak alan member value resolver ile üretir.
    /// Resolver; <see cref="IMemberValueResolver{TSource,TDestination,TSourceMember,TDestMember}"/> uygulamalıdır.
    /// </summary>
    /// <typeparam name="TValueResolver">Resolver türü.</typeparam>
    /// <typeparam name="TSourceMember">Girdi kaynak üyenin türü.</typeparam>
    /// <param name="sourceMember">Girdi kaynak üye seçicisi.</param>
    void MapFrom<TValueResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Hedef üyenin değerini, adı verilen kaynak üyeyi girdi olarak alan member value resolver ile üretir.
    /// </summary>
    /// <typeparam name="TValueResolver">Resolver türü.</typeparam>
    /// <typeparam name="TSourceMember">Girdi kaynak üyenin türü.</typeparam>
    /// <param name="sourceMemberName">Girdi kaynak üyenin adı.</param>
    void MapFrom<TValueResolver, TSourceMember>(string sourceMemberName);

    /// <summary>
    /// Hedef üyenin değerini verilen resolver örneği ile üretir.
    /// </summary>
    /// <param name="valueResolver">Resolver örneği.</param>
    void MapFrom(IValueResolver<TSource, TDestination, TMember> valueResolver);

    /// <summary>
    /// Hedef üyenin değerini verilen member value resolver örneği ile üretir.
    /// </summary>
    /// <typeparam name="TSourceMember">Girdi kaynak üyenin türü.</typeparam>
    /// <param name="valueResolver">Resolver örneği.</param>
    /// <param name="sourceMember">Girdi kaynak üye seçicisi.</param>
    void MapFrom<TSourceMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> valueResolver, Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Hedef üyenin değerini, türü çalışma zamanında verilen bir value resolver ile üretir.
    /// </summary>
    /// <param name="valueResolverType">Resolver türü.</param>
    void MapFrom(Type valueResolverType);

    /// <summary>
    /// Aynı adlı kaynak üyeyi bir value converter ile dönüştürür.
    /// </summary>
    /// <typeparam name="TValueConverter"><see cref="IValueConverter{TSourceMember,TDestinationMember}"/> uygulayan tür.</typeparam>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    void ConvertUsing<TValueConverter, TSourceMember>();

    /// <summary>
    /// Seçilen kaynak üyeyi bir value converter ile dönüştürür.
    /// </summary>
    /// <typeparam name="TValueConverter"><see cref="IValueConverter{TSourceMember,TDestinationMember}"/> uygulayan tür.</typeparam>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="sourceMember">Kaynak üye seçicisi.</param>
    void ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Adı verilen kaynak üyeyi bir value converter ile dönüştürür.
    /// </summary>
    /// <typeparam name="TValueConverter"><see cref="IValueConverter{TSourceMember,TDestinationMember}"/> uygulayan tür.</typeparam>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="sourceMemberName">Kaynak üye adı.</param>
    void ConvertUsing<TValueConverter, TSourceMember>(string sourceMemberName);

    /// <summary>
    /// Aynı adlı kaynak üyeyi verilen value converter örneği ile dönüştürür.
    /// </summary>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="valueConverter">Converter örneği.</param>
    void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter);

    /// <summary>
    /// Seçilen kaynak üyeyi verilen value converter örneği ile dönüştürür.
    /// </summary>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="valueConverter">Converter örneği.</param>
    /// <param name="sourceMember">Kaynak üye seçicisi.</param>
    void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Adı verilen kaynak üyeyi verilen value converter örneği ile dönüştürür.
    /// </summary>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="valueConverter">Converter örneği.</param>
    /// <param name="sourceMemberName">Kaynak üye adı.</param>
    void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, string sourceMemberName);

    /// <summary>
    /// Seçilen kaynak üyeyi VeloxMapper 5.x tarzı bir converter örneği ile dönüştürür.
    /// </summary>
    /// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
    /// <param name="converter">Converter örneği.</param>
    /// <param name="sourceMember">Kaynak üye seçicisi.</param>
    void ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember, TMember> converter, Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Hedef üyeyi eşleştirme ve doğrulama dışında bırakır.
    /// </summary>
    void Ignore();

    /// <summary>
    /// Hedef üyeyi yapılandırma doğrulamasından (<c>AssertConfigurationIsValid</c>) muaf tutar; üye konvansiyonla eşlenebiliyorsa eşlenmeye devam eder.
    /// </summary>
    void DoNotValidate();

    /// <summary>
    /// Değer çözümlendikten sonra değerlendirilen koşul. <c>false</c> ise üye atanmaz.
    /// </summary>
    /// <param name="condition">Kaynak nesneye göre koşul.</param>
    void Condition(Func<TSource, bool> condition);

    /// <summary>
    /// Değer çözümlendikten sonra değerlendirilen koşul. <c>false</c> ise üye atanmaz.
    /// </summary>
    /// <param name="condition">Kaynak ve hedef nesneye göre koşul.</param>
    void Condition(Func<TSource, TDestination, bool> condition);

    /// <summary>
    /// Değer çözümlendikten sonra değerlendirilen koşul. Üçüncü parametre <b>çözümlenen kaynak değerdir</b>
    /// (PATCH senaryosu: <c>(src, dest, srcMember) =&gt; srcMember != null</c>).
    /// </summary>
    /// <param name="condition">Koşul.</param>
    void Condition(Func<TSource, TDestination, TMember, bool> condition);

    /// <summary>
    /// Değer çözümlendikten sonra değerlendirilen koşul: <c>(src, dest, srcMember, destMember) =&gt; ...</c>.
    /// </summary>
    /// <param name="condition">Koşul.</param>
    void Condition(Func<TSource, TDestination, TMember, TMember, bool> condition);

    /// <summary>
    /// Değer çözümlendikten sonra değerlendirilen koşul: <c>(src, dest, srcMember, destMember, context) =&gt; ...</c>.
    /// </summary>
    /// <param name="condition">Koşul.</param>
    void Condition(Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool> condition);

    /// <summary>
    /// Değer çözümlenmeden önce değerlendirilen koşul. <c>false</c> ise değer hiç hesaplanmaz.
    /// </summary>
    /// <param name="condition">Kaynak nesneye göre koşul.</param>
    void PreCondition(Func<TSource, bool> condition);

    /// <summary>
    /// Değer çözümlenmeden önce, bağlama göre değerlendirilen koşul.
    /// </summary>
    /// <param name="condition">Bağlama göre koşul.</param>
    void PreCondition(Func<ResolutionContext, bool> condition);

    /// <summary>
    /// Değer çözümlenmeden önce, kaynak ve bağlama göre değerlendirilen koşul.
    /// </summary>
    /// <param name="condition">Koşul.</param>
    void PreCondition(Func<TSource, ResolutionContext, bool> condition);

    /// <summary>
    /// Değer çözümlenmeden önce, kaynak, hedef ve bağlama göre değerlendirilen koşul.
    /// </summary>
    /// <param name="condition">Koşul.</param>
    void PreCondition(Func<TSource, TDestination, ResolutionContext, bool> condition);

    /// <summary>
    /// Çözümlenen kaynak değer <c>null</c> ise hedefe yazılacak değeri belirler. Değer hedef üye türüne dönüştürülür.
    /// </summary>
    /// <param name="nullSubstitute">Yerine geçecek değer.</param>
    void NullSubstitute(object? nullSubstitute);

    /// <summary>
    /// Hedef üyenin mevcut değerini (iç içe nesne veya koleksiyon örneğini) korur ve kaynağı bu örneğe eşler.
    /// </summary>
    void UseDestinationValue();

    /// <summary>
    /// Hedef üyenin mevcut değerini kullanmaz; her eşlemede yeni değer atanır.
    /// </summary>
    void DoNotUseDestinationValue();

    /// <summary>
    /// Üyenin atanma sırasını belirler; küçük değerler önce atanır.
    /// </summary>
    /// <param name="mappingOrder">Sıra değeri.</param>
    void SetMappingOrder(int mappingOrder);

    /// <summary>
    /// Bu üyenin değerine, profil/global value transformer'lardan önce uygulanacak bir dönüşüm ekler.
    /// </summary>
    /// <param name="transformer">Dönüşüm ifadesi, ör. <c>v =&gt; v.Trim()</c>.</param>
    void AddTransform(Expression<Func<TMember, TMember>> transformer);

    /// <summary>
    /// <c>ProjectTo</c> içinde bu üyenin yalnızca <c>membersToExpand</c> ile açıkça istendiğinde sorguya dahil edilmesini sağlar.
    /// </summary>
    void ExplicitExpansion();

    /// <summary>
    /// Kaynak <c>null</c> olduğunda hedefe <c>null</c> yazılmasına izin verir (koleksiyonlar için boş koleksiyon oluşturulmaz).
    /// Global/profil <c>AllowNullCollections</c> ve <c>AllowNullDestinationValues</c> ayarlarını bu üye için ezer.
    /// </summary>
    void AllowNull();

    /// <summary>
    /// Kaynak <c>null</c> olduğunda hedefe <c>null</c> yazılmasına izin vermez (koleksiyonlar için boş koleksiyon oluşturulur).
    /// </summary>
    void DoNotAllowNull();

    /// <summary>
    /// AutoMapper uyumluluğu için kabul edilir; VeloxMapper çalışma zamanı eşleştirmesini zaten çağrı anında üretir.
    /// </summary>
    void MapAtRuntime();
}

/// <summary>
/// Tür bilgisi olmayan (open generic veya <c>ForAllMaps</c>) eşleştirmelerde kullanılan üye yapılandırması.
/// AutoMapper'ın non-generic <c>IMemberConfigurationExpression</c> arayüzü ile uyumludur.
/// </summary>
public interface IMemberConfigurationExpression : IMemberConfigurationExpression<object, object, object>
{
}
