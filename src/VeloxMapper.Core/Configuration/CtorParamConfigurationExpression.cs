using System;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="ICtorParamConfigurationExpression{TSource}"/> arayüzünün implementasyonu.
/// Constructor parametresinin hangi kaynak üye üzerinden besleneceğini saklar.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
internal sealed class CtorParamConfigurationExpression<TSource> : ICtorParamConfigurationExpression<TSource>
{
    /// <summary>
    /// MapFrom ile atanmış kaynak lambda ifadesi.
    /// </summary>
    public LambdaExpression? MapFromExpression { get; private set; }

    /// <inheritdoc />
    public void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember)
    {
        MapFromExpression = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));
    }
}
