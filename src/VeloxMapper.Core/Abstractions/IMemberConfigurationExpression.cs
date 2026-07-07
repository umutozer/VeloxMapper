using System;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// ForMember çağrısında property yapılandırması için kullanılan arayüz.
/// <c>MapFrom</c> veya <c>Ignore</c> seçeneklerini sunar.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
/// <typeparam name="TDestination">Hedef tür</typeparam>
/// <typeparam name="TMember">Hedef property'nin tipi</typeparam>
public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    /// <summary>
    /// Hedef property için özel kaynak ifadesi (lambda) belirler.
    /// </summary>
    /// <param name="sourceMember">Kaynak değer ifadesi (src => src.PropertyName veya src => src.A + src.B)</param>
    void MapFrom(Expression<Func<TSource, TMember>> sourceMember);

    /// <summary>
    /// Hedef property'yi mapping dışında bırakır.
    /// </summary>
    void Ignore();

    /// <summary>
    /// Hedef property'yi mapping dışında bırakır ve doğrulama (validation) sırasında hata vermemesini sağlar.
    /// </summary>
    void DoNotValidate();

    /// <summary>
    /// DI-destekli bir IVeloxValueResolver kullanarak hedef property değerini çözümler.
    /// </summary>
    /// <typeparam name="TResolver">Resolver tipi</typeparam>
    void MapFrom<TResolver>()
        where TResolver : class, IVeloxValueResolver<TSource, TDestination, TMember>;

    /// <summary>
    /// Kaynak nesnenin belirli bir property değerini parametre olarak alan DI-destekli bir IMemberValueResolver kullanarak hedef property değerini çözümler.
    /// </summary>
    /// <typeparam name="TResolver">Resolver tipi</typeparam>
    /// <typeparam name="TSourceMember">Kaynak property tipi</typeparam>
    /// <param name="sourceMember">Kaynak property seçici</param>
    void MapFrom<TResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TResolver : class, IVeloxMemberValueResolver<TSource, TDestination, TSourceMember, TMember>;

    /// <summary>
    /// Bir IVeloxValueConverter tipi kullanarak kaynak property değerini hedef property değerine dönüştürür.
    /// </summary>
    /// <typeparam name="TConverter">Converter tipi</typeparam>
    /// <typeparam name="TSourceMember">Kaynak property tipi</typeparam>
    /// <param name="sourceMember">Kaynak property seçici</param>
    void ConvertUsing<TConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TConverter : class, IVeloxValueConverter<TSourceMember, TMember>;

    /// <summary>
    /// Bir IVeloxValueConverter örneği kullanarak kaynak property değerini hedef property değerine dönüştürür.
    /// </summary>
    /// <typeparam name="TSourceMember">Kaynak property tipi</typeparam>
    /// <param name="converter">Converter örneği</param>
    /// <param name="sourceMember">Kaynak property seçici</param>
    void ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember, TMember> converter, Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Kaynak değer çözümlendikten sonra çalışır. False dönerse atama yapılmaz.
    /// </summary>
    /// <param name="predicate">Koşul fonksiyonu (src, dest, destMemberVal => bool)</param>
    void Condition(Func<TSource, TDestination, TMember, bool> predicate);

    /// <summary>
    /// Kaynak değer çözümlenmeden önce çalışır. False dönerse kaynak değer çözümlenmez ve atama yapılmaz.
    /// </summary>
    /// <param name="predicate">Koşul fonksiyonu (src => bool)</param>
    void PreCondition(Func<TSource, bool> predicate);

    /// <summary>
    /// Çözümlenen kaynak değer null ise hedef property'e atanacak varsayılan değeri belirler.
    /// </summary>
    /// <param name="substituteValue">Boş değer yerine geçecek varsayılan değer</param>
    void NullSubstitute(TMember substituteValue);

    /// <summary>
    /// Eşleştirme sırasında hedef property'nin mevcut değerini korur (örn: mevcut koleksiyon referansının korunması).
    /// </summary>
    void UseDestinationValue();

    /// <summary>
    /// Özelliğin eşleştirilme sırasını belirler. Düşük değerli özellikler daha önce eşleştirilir.
    /// </summary>
    /// <param name="mappingOrder">Sıralama değeri</param>
    void SetMappingOrder(int mappingOrder);
}
