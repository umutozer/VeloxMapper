using System;
using System.Collections.Generic;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Mapping işlemi sırasında resolver, action ve koşul delegate'lerine aktarılan bağlam nesnesi.
/// Çalışma zamanı parametreleri (Items) ve DI servis çözümleme (ServiceProvider) desteği sunar.
/// </summary>
public sealed class VeloxResolutionContext : IDisposable
{
    /// <summary>
    /// Mapper referansı - iç içe (nested) mapping işlemleri için.
    /// </summary>
    public IVeloxMapper Mapper { get; internal set; }

    /// <summary>
    /// DI servis sağlayıcısı (opsiyonel, DI dışı kullanımda null).
    /// </summary>
    public IServiceProvider? ServiceProvider { get; internal set; }

    /// <summary>
    /// Çalışma zamanı parametreleri. Map çağrısında kullanıcı tarafından doldurulabilir.
    /// AutoMapper'ın ResolutionContext.Items karşılığıdır.
    /// </summary>
    private IDictionary<string, object>? _items;

    /// <summary>
    /// Çalışma zamanı parametreleri. Map çağrısında kullanıcı tarafından doldurulabilir.
    /// AutoMapper'ın ResolutionContext.Items karşılığıdır.
    /// </summary>
    public IDictionary<string, object> Items => _items ??= new Dictionary<string, object>();

    /// <summary>
    /// Mevcut rekürsif mapping derinliği (MaxDepth kontrolü için çalışma zamanında izlenir).
    /// </summary>
    internal int CurrentDepth { get; set; }

    /// <summary>
    /// PreserveReferences özelliği için zaten map edilmiş nesnelerin önbelleği.
    /// </summary>
    internal VeloxReferenceCache? ReferenceCache { get; set; }

    /// <summary>
    /// Şu anda eşleştirilmekte (map) olan üyenin adı. Hata durumunda detaylı konum bilgisi sağlamak için kullanılır.
    /// </summary>
    public string? CurrentMember { get; set; }

    /// <summary>
    /// Yeni bir <see cref="VeloxResolutionContext"/> örneği oluşturur.
    /// </summary>
    /// <param name="mapper">Mapper örneği</param>
    /// <param name="serviceProvider">Servis sağlayıcı</param>
    /// <param name="items">Çalışma zamanı parametreleri</param>
    public VeloxResolutionContext(
        IVeloxMapper mapper,
        IServiceProvider? serviceProvider = null,
        IDictionary<string, object>? items = null)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        ServiceProvider = serviceProvider;
        _items = items;
    }

    /// <summary>
    /// Bağlam nesnesinin durumunu sıfırlayarak yeniden kullanılabilir hale getirir.
    /// </summary>
    internal void Reset(IVeloxMapper mapper, IServiceProvider? serviceProvider)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        ServiceProvider = serviceProvider;
        
        // Sadece içi doluysa temizleme yaparak CPU döngülerini koru
        if (_items != null && _items.Count > 0)
        {
            _items.Clear();
        }
        
        CurrentDepth = 0;
        
        if (ReferenceCache != null)
        {
            ReferenceCache.Clear();
            ReferenceCache = null;
        }
        
        CurrentMember = null;
    }

    /// <summary>
    /// Bağlam nesnesinin kaynaklarını serbest bırakır ve önbelleği temizler.
    /// </summary>
    public void Dispose()
    {
        ReferenceCache?.Clear();
        ReferenceCache = null;
    }
}
