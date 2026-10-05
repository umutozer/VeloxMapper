using System;
using System.Collections.Generic;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// Tek bir <c>Map</c> çağrısı boyunca resolver, converter, koşul ve action delegelerine aktarılan bağlam nesnesi.
/// AutoMapper'ın <c>ResolutionContext</c> sınıfı ile aynı isim ve üyelere sahiptir
/// (<see cref="Items"/>, <see cref="Mapper"/>, <see cref="State"/>, <see cref="TryGetItems"/>).
/// </summary>
/// <remarks>
/// Bağlam örnekleri motor tarafından oluşturulur ve havuzlanır; bir <c>Map</c> çağrısı tamamlandıktan sonra
/// bağlama referans tutmayın.
/// </remarks>
public class ResolutionContext
{
    private IDictionary<string, object>? _items;

    /// <summary>
    /// Bağlamı oluşturur. Uygulama kodunun bu kurucuyu çağırması gerekmez; bağlam her <c>Map</c> çağrısında motor tarafından sağlanır.
    /// </summary>
    /// <param name="mapper">Eşleştirmeyi yürüten mapper.</param>
    /// <param name="serviceProvider">Resolver/converter örneklerini çözmek için kullanılan servis sağlayıcı (isteğe bağlı).</param>
    /// <param name="items">Başlangıç <see cref="Items"/> sözlüğü (isteğe bağlı).</param>
    public ResolutionContext(IMapper mapper, IServiceProvider? serviceProvider = null, IDictionary<string, object>? items = null)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        ServiceProvider = serviceProvider;
        _items = items;
    }

    /// <summary>
    /// Eşleştirmeyi yürüten mapper. İç içe eşleştirmeler için kullanılabilir:
    /// <c>context.Mapper.Map&lt;AddressDto&gt;(source.Address)</c>.
    /// </summary>
    public IMapper Mapper { get; internal set; }

    /// <summary>
    /// Resolver, converter ve mapping action örneklerinin çözüldüğü servis sağlayıcı. DI dışında kullanımda <c>null</c> olabilir.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; internal set; }

    /// <summary>
    /// Çağrıya özel parametreler. <c>mapper.Map&lt;Dto&gt;(src, opt =&gt; opt.Items["Key"] = value)</c> ile doldurulur.
    /// AutoMapper'ın <c>ResolutionContext.Items</c> karşılığıdır.
    /// </summary>
    public IDictionary<string, object> Items => _items ??= new Dictionary<string, object>();

    /// <summary>
    /// <c>opt.State = ...</c> ile verilen, çağrıya özel durum nesnesi.
    /// </summary>
    public object? State { get; set; }

    /// <summary>
    /// Şu anda eşleştirilen hedef üyenin adı. Hata mesajlarında konum bilgisi için kullanılır.
    /// </summary>
    public string? CurrentMember { get; set; }

    /// <summary>
    /// <see cref="Items"/> sözlüğü oluşturulmuş ve en az bir öğe içeriyorsa <c>true</c> döner; sözlüğü gereksiz yere oluşturmaz.
    /// </summary>
    /// <param name="items">Varsa öğe sözlüğü.</param>
    public bool TryGetItems(out IDictionary<string, object> items)
    {
        if (_items != null && _items.Count > 0)
        {
            items = _items;
            return true;
        }

        items = null!;
        return false;
    }

    /// <summary>Mevcut rekürsif eşleştirme derinliği (MaxDepth kontrolü için).</summary>
    internal int CurrentDepth { get; set; }

    /// <summary>PreserveReferences için eşleştirilmiş nesnelerin önbelleği.</summary>
    internal VeloxReferenceCache? ReferenceCache { get; set; }

    /// <summary><c>ConstructServicesUsing</c> ile verilen servis oluşturucu (isteğe bağlı).</summary>
    internal Func<Type, object>? ServiceCtor { get; set; }

    /// <summary>Çağrı seçeneklerinden gelen öğe sözlüğünü bağlama atar.</summary>
    internal void SetItems(IDictionary<string, object>? items) => _items = items;

    /// <summary>
    /// Bağlamı yeniden kullanım için sıfırlar.
    /// </summary>
    internal void Reset(IMapper mapper, IServiceProvider? serviceProvider)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        ServiceProvider = serviceProvider;
        _items = null;
        State = null;
        CurrentDepth = 0;
        ReferenceCache?.Clear();
        ReferenceCache = null;
        CurrentMember = null;
        ServiceCtor = null;
    }

    /// <summary>
    /// Verilen tipi önce <c>ConstructServicesUsing</c> ile, sonra <see cref="ServiceProvider"/> üzerinden çözer.
    /// Bulunamazsa <c>null</c> döner.
    /// </summary>
    internal object? GetServiceOrNull(Type serviceType)
    {
        if (ServiceCtor != null)
        {
            var created = ServiceCtor(serviceType);
            if (created != null) return created;
        }

        return ServiceProvider?.GetService(serviceType);
    }
}
