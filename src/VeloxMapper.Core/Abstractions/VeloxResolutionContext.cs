using System;
using System.Collections.Generic;

namespace VeloxMapper.Abstractions;

/// <summary>
/// VeloxMapper 5.x API'si ile geriye dönük uyumluluk için korunan bağlam tipi.
/// Motorun oluşturduğu tüm bağlamlar bu tiptedir; yeni kodda <see cref="ResolutionContext"/> kullanın.
/// </summary>
public sealed class VeloxResolutionContext : ResolutionContext, IDisposable
{
    /// <summary>
    /// Yeni bir bağlam örneği oluşturur.
    /// </summary>
    /// <param name="mapper">Mapper örneği.</param>
    /// <param name="serviceProvider">Servis sağlayıcı (isteğe bağlı).</param>
    /// <param name="items">Başlangıç öğeleri (isteğe bağlı).</param>
    public VeloxResolutionContext(IMapper mapper, IServiceProvider? serviceProvider = null, IDictionary<string, object>? items = null)
        : base(mapper, serviceProvider, items)
    {
    }

    /// <summary>
    /// Referans önbelleğini temizler.
    /// </summary>
    public void Dispose()
    {
        ReferenceCache?.Clear();
        ReferenceCache = null;
    }
}
