using System;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// <see cref="ICtorParamConfigurationExpression{TSource}"/> implementasyonu.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
internal sealed class CtorParamConfigurationExpression<TSource> : ICtorParamConfigurationExpression<TSource>
{
    /// <summary>Kaynak ifadesi.</summary>
    public LambdaExpression? MapFromExpression { get; private set; }

    /// <summary>Bağlamlı değer fonksiyonu.</summary>
    public Delegate? MapFromFunc { get; private set; }

    /// <inheritdoc />
    public void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember)
    {
        MapFromExpression = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));
        MapFromFunc = null;
    }

    /// <inheritdoc />
    public void MapFrom<TMember>(Func<TSource, ResolutionContext, TMember> resolver)
    {
        MapFromFunc = resolver ?? throw new ArgumentNullException(nameof(resolver));
        MapFromExpression = null;
    }

    /// <inheritdoc />
    public void MapFrom(string sourceMembersPath)
    {
        if (string.IsNullOrWhiteSpace(sourceMembersPath)) throw new ArgumentNullException(nameof(sourceMembersPath));
        MapFromExpression = MemberPath.BuildAccessor(typeof(TSource), sourceMembersPath);
        MapFromFunc = null;
    }
}
