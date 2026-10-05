using System;
using System.Collections.Generic;

namespace VeloxMapper;

/// <summary>
/// Tek bir <c>Map</c> çağrısına özel seçenekler. AutoMapper'ın <c>IMappingOperationOptions</c> arayüzü ile uyumludur:
/// <c>mapper.Map&lt;Dto&gt;(source, opt =&gt; opt.Items["TenantId"] = tenantId)</c>.
/// </summary>
public interface IMappingOperationOptions
{
    /// <summary>
    /// Resolver, converter ve koşullara <see cref="ResolutionContext.Items"/> üzerinden aktarılan çağrı parametreleri.
    /// </summary>
    IDictionary<string, object> Items { get; }

    /// <summary>
    /// Çağrıya özel durum nesnesi; <see cref="ResolutionContext.State"/> üzerinden okunur.
    /// </summary>
    object? State { get; set; }

    /// <summary>
    /// Bu çağrı için resolver/converter/action örneklerinin nasıl oluşturulacağını belirler.
    /// </summary>
    /// <param name="constructor">Tip alıp örnek döndüren fabrika.</param>
    void ConstructServicesUsing(Func<Type, object> constructor);
}

/// <summary>
/// Kaynak ve hedef türleri bilinen bir <c>Map</c> çağrısına özel seçenekler.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
public interface IMappingOperationOptions<TSource, TDestination> : IMappingOperationOptions
{
    /// <summary>
    /// Bu çağrıda, eşleştirme başlamadan önce çalışacak eylem.
    /// </summary>
    /// <param name="beforeFunction">Eylem.</param>
    void BeforeMap(Action<TSource, TDestination> beforeFunction);

    /// <summary>
    /// Bu çağrıda, eşleştirme tamamlandıktan sonra çalışacak eylem.
    /// </summary>
    /// <param name="afterFunction">Eylem.</param>
    void AfterMap(Action<TSource, TDestination> afterFunction);
}
