using System;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// ForCtorParam ile tanımlanan constructor parametre eşleştirme kuralı.
/// </summary>
public sealed class CtorParamRule
{
    /// <summary>Constructor parametre adı.</summary>
    public string ParameterName { get; }

    /// <summary>Kaynak eşleştirme lambda ifadesi.</summary>
    public LambdaExpression MapFromExpression { get; }

    /// <summary>
    /// Yeni bir CtorParamRule örneği oluşturur.
    /// </summary>
    public CtorParamRule(string parameterName, LambdaExpression mapFromExpression)
    {
        ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
        MapFromExpression = mapFromExpression ?? throw new ArgumentNullException(nameof(mapFromExpression));
    }
}
