using System;
using System.Linq.Expressions;

namespace VeloxMapper;

/// <summary>
/// ForCtorParam çağrısında constructor parametresi yapılandırması için kullanılan arayüz.
/// Türkçe XML summary açıklaması içermektedir.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
public interface ICtorParamConfigurationExpression<TSource>
{
    /// <summary>
    /// Constructor parametresi için kaynak ifadesi belirler.
    /// </summary>
    /// <typeparam name="TMember">Kaynak üye tipi</typeparam>
    /// <param name="sourceMember">Kaynak üye seçici lambda ifadesi</param>
    void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember);
}
