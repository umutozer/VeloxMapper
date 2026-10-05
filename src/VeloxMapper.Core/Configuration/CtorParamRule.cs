using System;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <c>ForCtorParam</c> ile tanımlanan kurucu parametresi kuralı.
/// </summary>
public sealed class CtorParamRule
{
    /// <summary>Kurucu parametresinin adı.</summary>
    public string ParameterName { get; }

    /// <summary>Kaynak ifadesi (<see cref="MapFromFunc"/> verilmişse <c>null</c>).</summary>
    public LambdaExpression? MapFromExpression { get; }

    /// <summary>Bağlamlı değer fonksiyonu: <c>(src, context) =&gt; ...</c>.</summary>
    public Delegate? MapFromFunc { get; }

    /// <summary>
    /// Kaynak ifadesine dayalı bir kural oluşturur.
    /// </summary>
    /// <param name="parameterName">Kurucu parametresinin adı.</param>
    /// <param name="mapFromExpression">Kaynak ifadesi.</param>
    public CtorParamRule(string parameterName, LambdaExpression mapFromExpression)
    {
        ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
        MapFromExpression = mapFromExpression ?? throw new ArgumentNullException(nameof(mapFromExpression));
    }

    /// <summary>
    /// Bağlamlı fonksiyona dayalı bir kural oluşturur.
    /// </summary>
    /// <param name="parameterName">Kurucu parametresinin adı.</param>
    /// <param name="mapFromFunc">Değer üreten fonksiyon.</param>
    public CtorParamRule(string parameterName, Delegate mapFromFunc)
    {
        ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
        MapFromFunc = mapFromFunc ?? throw new ArgumentNullException(nameof(mapFromFunc));
    }
}
