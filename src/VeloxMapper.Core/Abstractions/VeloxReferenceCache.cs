using System;
using System.Collections.Generic;
using VeloxMapper.Caching;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Nesne referanslarını saklayan thread-safe bellek önbellek yapısı.
/// Nesne döngülerini (circular references) önlemek amacıyla PreserveReferences özelliğinde kullanılır.
/// </summary>
public sealed class VeloxReferenceCache
{
    private readonly Dictionary<object, object> _cache = new Dictionary<object, object>(new ReferenceComparer());
    private readonly object _lock = new object();

    /// <summary>
    /// Belirtilen kaynak nesne referansının önbellekte olup olmadığını kontrol eder.
    /// </summary>
    /// <param name="source">Kaynak nesne referansı</param>
    /// <param name="destination">Önbellekte bulunan hedef nesne referansı</param>
    /// <returns>Bulunursa true, aksi takdirde false döner.</returns>
    public bool TryGetValue(object source, out object? destination)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(source, out destination);
        }
    }

    /// <summary>
    /// Kaynak nesne referansı ile hedef nesne referansı arasındaki eşleşmeyi önbelleğe ekler.
    /// </summary>
    /// <param name="source">Kaynak nesne referansı</param>
    /// <param name="destination">Hedef nesne referansı</param>
    public void Set(object source, object destination)
    {
        lock (_lock)
        {
            _cache[source] = destination;
        }
    }

    /// <summary>
    /// Önbellekteki tüm kayıtları temizler.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}
