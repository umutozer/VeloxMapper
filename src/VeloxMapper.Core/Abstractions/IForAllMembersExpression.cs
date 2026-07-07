using System;

namespace VeloxMapper;

/// <summary>
/// <see cref="IMappingExpression{TSource,TDestination}.ForAllMembers"/> çağrısından dönen
/// toplu üye yapılandırma arayüzü.
/// AutoMapper'ın <c>IMemberConfigurationExpression</c> sınırlı karşılığıdır.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
/// <typeparam name="TDestination">Hedef tür</typeparam>
public interface IForAllMembersExpression<TSource, TDestination>
{
    /// <summary>
    /// Tüm property atamaları için koşullu filtre uygular.
    /// Koşul <c>false</c> döndürdüğünde ilgili property hedefte değiştirilmez.
    /// <para>
    /// En yaygın kullanım — null-ignore (PATCH pattern'ı):
    /// <code>
    /// .ForAllMembers(opt => opt.Condition((src, dest, srcValue) => srcValue != null))
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="predicate">
    /// (kaynak nesne, hedef nesne, atanacak kaynak değer) → bool
    /// </param>
    void Condition(Func<TSource, TDestination, object?, bool> predicate);

    /// <summary>
    /// Tüm üyeleri eşleştirme dışında bırakır.
    /// </summary>
    void Ignore();
}
