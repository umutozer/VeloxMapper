using System;
using System.Collections.Generic;

namespace VeloxMapper.Caching;

/// <summary>
/// Nesnelerin sadece referans bazında (ReferenceEquals) eşitliğini kontrol eden 
/// ve RuntimeHelpers.GetHashCode kullanarak hash kodunu alan karşılaştırıcı.
/// Döngüsel referans (Circular Reference) cache yapılarında kullanılır.
/// </summary>
internal sealed class ReferenceComparer : IEqualityComparer<object>
{
    /// <summary>
    /// İki nesnenin referanslarının aynı olup olmadığını kontrol eder.
    /// </summary>
    /// <param name="x">İlk nesne</param>
    /// <param name="y">İkinci nesne</param>
    /// <returns>Referanslar eşitse true, aksi halde false.</returns>
    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    /// <summary>
    /// Nesnenin referansına göre özgün hash kodunu döner.
    /// </summary>
    /// <param name="obj">Hash kodu alınacak nesne</param>
    /// <returns>Nesnenin sistem düzeyindeki benzersiz hash kodu.</returns>
    public int GetHashCode(object obj) => obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
