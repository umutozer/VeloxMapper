using System;
using System.Linq.Expressions;

namespace VeloxMapper;

/// <summary>
/// Dondurulmuş eşleştirme yapılandırması. AutoMapper'ın <c>IConfigurationProvider</c> arayüzü ile uyumludur:
/// <c>mapper.ConfigurationProvider.AssertConfigurationIsValid()</c>, <c>query.ProjectTo&lt;Dto&gt;(mapper.ConfigurationProvider)</c>.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Tüm eşleştirmeleri doğrular. Eşlenmemiş hedef üyeler, çözülemeyen kurucu parametreleri veya uyumsuz türler varsa
    /// tüm hataları listeleyen <see cref="VeloxMapper.Exceptions.VeloxValidationException"/> fırlatır.
    /// </summary>
    void AssertConfigurationIsValid();

    /// <summary>
    /// Yalnızca belirtilen profildeki eşleştirmeleri doğrular.
    /// </summary>
    /// <typeparam name="TProfile">Doğrulanacak profil.</typeparam>
    void AssertConfigurationIsValid<TProfile>() where TProfile : VeloxProfile;

    /// <summary>
    /// Yalnızca verilen adlı profildeki eşleştirmeleri doğrular.
    /// </summary>
    /// <param name="profileName">Profil adı (<see cref="VeloxProfile.ProfileName"/>).</param>
    void AssertConfigurationIsValid(string profileName);

    /// <summary>
    /// Tüm eşleştirmeleri başlangıçta derler; ilk çağrıdaki derleme gecikmesini ortadan kaldırır.
    /// </summary>
    void CompileMappings();

    /// <summary>
    /// Bu yapılandırmadan yeni bir mapper oluşturur.
    /// </summary>
    /// <returns>Yeni mapper.</returns>
    IMapper CreateMapper();

    /// <summary>
    /// Resolver/converter/action örneklerini verilen fabrika ile oluşturan yeni bir mapper oluşturur.
    /// </summary>
    /// <param name="serviceCtor">Tip alıp örnek döndüren fabrika.</param>
    /// <returns>Yeni mapper.</returns>
    IMapper CreateMapper(Func<Type, object> serviceCtor);

    /// <summary>
    /// Bir tür çifti için üretilen eşleştirme ifadesini (derlenmeden önce) döndürür. Hata ayıklama ve inceleme içindir.
    /// </summary>
    /// <param name="sourceType">Kaynak tür.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <returns>Eşleştirme lambda ifadesi.</returns>
    LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType);
}
