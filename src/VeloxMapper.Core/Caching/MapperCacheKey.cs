using System;

namespace VeloxMapper.Caching;

/// <summary>
/// Eşleştirme yapılandırmalarını ve çalışma zamanı expression ağaçlarını önbelleklemek
/// için kullanılan deterministik allocation-free (readonly record struct) anahtardır.
/// <para>Belirtilen zorunlu düzeltmeye istinaden MappingMode bilgisini içerir.</para>
/// </summary>
public readonly record struct MapperCacheKey(
    Type Source,
    Type Destination,
    MappingMode Mode // Map | Patch | ProjectTo
);
