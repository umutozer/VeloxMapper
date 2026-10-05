---
title: "IMapper"
description: "Nesne eşleme ve sorgu projeksiyonu için kullanılan IMapper arayüzünün tüm üyeleri ve çağrı seçenekleri."
section: api
order: 10
---

# IMapper

`IMapper`, eşleme çağrılarını yaptığınız ana arayüzdür. AutoMapper'daki `IMapper` ile aynı ada, overload'lara ve davranışa sahiptir. Thread-safe'dir; DI ile kullanıldığında transient olarak kaydedilir ve derlenmiş eşlemeleri `MapperConfiguration` ile paylaşır.

```csharp
namespace VeloxMapper;

public interface IMapper
```

**Uygulayan tür:** `VeloxMapper.Mapper`. **Takma ad:** `IVeloxMapper` (`IMapper`'dan türer, ek üyesi yoktur; 5.x uyumluluğu için).

## Mapper örneği oluşturmak

| Yol | Örnek |
| --- | --- |
| DI | `public OrdersController(IMapper mapper)` (bkz. [Dependency Injection API](./dependency-injection.md)) |
| Yapılandırmadan | `IMapper mapper = config.CreateMapper();` |
| Yapılandırmadan, servis fabrikasıyla | `config.CreateMapper(type => serviceProvider.GetRequiredService(type))` |
| Doğrudan | `new Mapper(config)` veya `new Mapper(config, serviceProvider)` |

```csharp
public Mapper(IConfigurationProvider configurationProvider, IServiceProvider? serviceProvider = null);
public Mapper(IConfigurationProvider configurationProvider, Func<Type, object> serviceCtor);
```

`configurationProvider` bir `MapperConfiguration` olmalıdır; değilse `ArgumentException` fırlatılır. `serviceProvider`, resolver, converter ve mapping action türlerini çözmek için kullanılır.

## ConfigurationProvider

```csharp
IConfigurationProvider ConfigurationProvider { get; }
```

Mapper'ın dayandığı yapılandırma. `ProjectTo`, doğrulama ve ön derleme için kullanılır:

```csharp
query.ProjectTo<OrderDto>(mapper.ConfigurationProvider);
mapper.ConfigurationProvider.AssertConfigurationIsValid();
```

AutoMapper ile aynı. Bkz. [MapperConfiguration](./mapper-configuration.md).

## Yeni nesneye eşleme

### Map&lt;TDestination&gt;(object)

```csharp
TDestination Map<TDestination>(object? source);
TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions<object, TDestination>> opts);
```

Kaynağı yeni bir `TDestination` örneğine eşler. Kaynak türü, nesnenin çalışma zamanı türünden belirlenir (EF Core/Castle proxy türleri gerçek varlık türüne çözülür).

| Kaynak | Dönüş |
| --- | --- |
| `null`, hedef referans türü | `null` |
| `null`, hedef koleksiyon | Boş koleksiyon (`AllowNullCollections = true` ise `null`) |
| `null`, hedef değer türü | `default(TDestination)` |

```csharp
var dto = mapper.Map<OrderDto>(order);
var list = mapper.Map<List<OrderDto>>(orders);
```

AutoMapper ile aynı.

### Map&lt;TSource, TDestination&gt;(TSource)

```csharp
TDestination Map<TSource, TDestination>(TSource source);
TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts);
```

Kaynak türünü açıkça verir. Önbellekteki tipli delegeyi doğrudan çağırdığı için sık çağrılan kod yollarında tercih edilir. Kaynak, `TSource`'tan türeyen bir nesneyse `Include`/`IncludeBase` ile tanımlanan türetilmiş eşleme seçilir.

```csharp
var dto = mapper.Map<Order, OrderDto>(order);
var dtos = mapper.Map<List<Order>, List<OrderDto>>(orders);
```

AutoMapper ile aynı.

## Mevcut nesneye eşleme

```csharp
TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts);
```

Kaynağı var olan bir hedef nesneye eşler ve **hedefi döndürür**.

- İç içe nesneler mevcut örneklerine, koleksiyonlar mevcut koleksiyon örneklerine eşlenir. EF Core tarafından izlenen varlıklar ve navigation koleksiyonları korunur.
- `destination` `null` ise yeni bir nesne oluşturulur.
- `source` `null` ise `destination` değiştirilmeden döner.

```csharp
var customer = await db.Customers.Include(c => c.Addresses).SingleAsync(c => c.Id == id);
mapper.Map(request, customer);
await db.SaveChangesAsync();
```

AutoMapper ile aynı. Ayrıntılar için [Mevcut Nesneye Eşleme](../map-to-existing.md) sayfasına bakın. `null` değerleri atlamak için [PATCH seçenekleri](../null-handling.md#patch-için-null-değerleri-atlamak).

## Çalışma zamanı türleriyle eşleme

```csharp
object Map(object? source, Type sourceType, Type destinationType);
object Map(object? source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts);
object Map(object? source, object? destination, Type sourceType, Type destinationType);
object Map(object? source, object? destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts);
```

Türlerin derleme zamanında bilinmediği altyapı kodu (generic repository, mesaj işleyiciler) içindir. İlk iki overload yeni nesne oluşturur; son ikisi `destination`'a eşler ve onu döndürür.

```csharp
object dto = mapper.Map(entity, entity.GetType(), dtoType);
```

AutoMapper ile aynı.

## ProjectTo

```csharp
IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object? parameters = null,
    params Expression<Func<TDestination, object?>>[] membersToExpand);

IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters,
    params string[] membersToExpand);

IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object>? parameters = null,
    params string[] membersToExpand);
```

Sorguya, eşleme yapılandırmasından üretilen bir `Select` ifadesi ekler. `query.ProjectTo<TDto>(mapper.ConfigurationProvider, ...)` genişletme metotlarıyla aynı sonucu verir.

| Parametre | Açıklama |
| --- | --- |
| `source` | Projekte edilecek sorgu (ör. `DbSet<T>`). |
| `parameters` | Parametreli `MapFrom` ifadeleri için değerler: anonim nesne (`new { currentUserId }`) veya sözlük. |
| `membersToExpand` | `ExplicitExpansion` ile işaretli, sorguya eklenecek üyeler: ifade (`d => d.Orders`) veya yol (`"Orders.Lines"`). |

```csharp
var page = await mapper.ProjectTo<OrderDto>(db.Orders.Where(o => o.CustomerId == id))
    .OrderByDescending(o => o.CreatedAt)
    .Take(20)
    .ToListAsync();
```

AutoMapper ile aynı. Kurallar ve sınırlamalar için [ProjectTo & EF Core](../projection.md) sayfasına bakın.

## Çağrı seçenekleri

`Map` overload'larının `opts` parametresi, yalnızca o çağrıya özel ayarlar verir.

```csharp
public interface IMappingOperationOptions
{
    IDictionary<string, object> Items { get; }
    object? State { get; set; }
    void ConstructServicesUsing(Func<Type, object> constructor);
}

public interface IMappingOperationOptions<TSource, TDestination> : IMappingOperationOptions
{
    void BeforeMap(Action<TSource, TDestination> beforeFunction);
    void AfterMap(Action<TSource, TDestination> afterFunction);
}
```

| Üye | Açıklama |
| --- | --- |
| `Items` | Resolver, converter, koşul ve action'lara `ResolutionContext.Items` ile aktarılan anahtar–değer çiftleri. |
| `State` | Tek bir nesne aktarmak için; `ResolutionContext.State` ile okunur. |
| `ConstructServicesUsing` | Bu çağrıda resolver/converter/action örneklerini oluşturacak fabrika. |
| `BeforeMap` | Kök nesne eşlenmeden önce çalışır. Yeni nesne oluşturan çağrılarda `destination` `null`'dır. |
| `AfterMap` | Kök nesne eşlendikten sonra çalışır. |

```csharp
var dto = mapper.Map<Order, OrderDto>(order, opt =>
{
    opt.Items["Culture"] = "tr-TR";
    opt.Items["CurrentUserId"] = userId;
    opt.AfterMap((src, dest) => dest.IsEditable = src.OwnerId == userId);
});
```

Seçenek verilmeden yapılan çağrılarda resolver içinde `context.Items`'a erişmek boş bir sözlük döndürür; AutoMapper bu durumda hata fırlatır.

Seçenek lambdaları AutoMapper ile aynı şekilde derlenir.

## Thread güvenliği ve ömür

- `IMapper` örnekleri thread-safe'dir; aynı örneği eşzamanlı çağrılarda kullanabilirsiniz.
- `Mapper` yalnızca yapılandırmaya ve servis sağlayıcıya referans tutar; oluşturmak ucuzdur. DI'da transient kaydedilir, böylece scoped servisler resolver'lara doğru scope'tan gelir.
- Derlenmiş eşlemeler `MapperConfiguration` üzerinde tutulur ve aynı yapılandırmadan oluşturulan tüm mapper'lar tarafından paylaşılır.

## Hata durumları

| Durum | İstisna |
| --- | --- |
| Bir üye eşlenirken resolver, `MapFrom` fonksiyonu veya dönüşüm hata fırlattı | `VeloxMappingException` (asıl hata `InnerException`'da; mesajda tür çifti ve üye adı) |
| Tür çifti eşlenemiyor (ör. `Order` → `Guid`) | `VeloxConfigurationException` |
| Hedef türün kurucusu çözülemiyor | `VeloxAmbiguousConstructorException` |

Bkz. [İstisnalar](./exceptions.md).

## AutoMapper uyumluluğu

Tüm üyeler AutoMapper 13–15 `IMapper` imzalarıyla aynıdır. Farklar:

- `IVeloxMapper` takma adı ve `query.ProjectTo<T>(mapper)` kısayolu VeloxMapper'a özgüdür.
- `context.Items` seçenek verilmeden de kullanılabilir.
- İstisna türleri farklıdır (`AutoMapperMappingException` → `VeloxMappingException`).
