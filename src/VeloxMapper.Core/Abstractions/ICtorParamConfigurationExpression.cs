using System;
using System.Linq.Expressions;

namespace VeloxMapper;

/// <summary>
/// <c>ForCtorParam</c> çağrısında bir kurucu (constructor) parametresinin değer kaynağını yapılandırır.
/// AutoMapper'ın <c>ICtorParamConfigurationExpression&lt;TSource&gt;</c> arayüzü ile uyumludur.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
public interface ICtorParamConfigurationExpression<TSource>
{
    /// <summary>
    /// Parametre değerini bir kaynak ifadesinden alır.
    /// </summary>
    /// <typeparam name="TMember">İfadenin döndürdüğü tür.</typeparam>
    /// <param name="sourceMember">Kaynak ifadesi.</param>
    void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember);

    /// <summary>
    /// Parametre değerini kaynak ve çalışma zamanı bağlamından hesaplar.
    /// </summary>
    /// <typeparam name="TMember">Fonksiyonun döndürdüğü tür.</typeparam>
    /// <param name="resolver">Değer üreten fonksiyon.</param>
    void MapFrom<TMember>(Func<TSource, ResolutionContext, TMember> resolver);

    /// <summary>
    /// Parametre değerini kaynak üye adından (veya noktalı yoldan) alır.
    /// </summary>
    /// <param name="sourceMembersPath">Kaynak üye adı veya yolu.</param>
    void MapFrom(string sourceMembersPath);
}
