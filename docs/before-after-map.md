---
title: "Before/After Map"
description: "Eşleme başlamadan önce veya tamamlandıktan sonra özel kod çalıştırın."
section: customization
order: 70
---

# Before/After Map

`BeforeMap` ve `AfterMap`, üye atamalarının çevresinde kod çalıştırmanızı sağlar. Hesaplanmış alanları doldurmak, denetim bilgisi yazmak veya kaynak verisini normalize etmek için kullanılır.

## Çalışma sırası

Bir nesne eşlenirken adımlar şu sırayla uygulanır:

1. Hedef nesne oluşturulur (veya `Map(source, destination)` çağrısında mevcut nesne kullanılır).
2. `BeforeMap` eylemleri çalışır.
3. Üyeler atanır (`SetMappingOrder` sırasıyla).
4. `ForPath` kuralları uygulanır.
5. `AfterMap` eylemleri çalışır.

`BeforeMap` çalıştığında hedef nesne oluşturulmuştur ama üyeleri henüz atanmamıştır. Kurucuyla oluşturulan türlerde (record'lar) kurucu parametreleri `BeforeMap`'ten önce verilmiş olur.

## Eşleme tanımında

```csharp title="OrderProfile.cs"
CreateMap<Order, OrderDto>()
    .BeforeMap((src, dest) => src.Lines.RemoveAll(l => l.Quantity == 0))
    .AfterMap((src, dest) => dest.Total = dest.Items.Sum(i => i.Price * i.Quantity))
    .AfterMap((src, dest, ctx) => dest.ViewedBy = ctx.Items.TryGetValue("User", out var user) ? (string)user : null);
```

| Overload | Açıklama |
| --- | --- |
| `BeforeMap(Action<TSource, TDestination>)` / `AfterMap(...)` | Satır içi eylem. |
| `BeforeMap(Action<TSource, TDestination, ResolutionContext>)` / `AfterMap(...)` | Çağrı bağlamına (`Items`, `State`, `Mapper`) erişen eylem. |
| `BeforeMap<TMappingAction>()` / `AfterMap<TMappingAction>()` | DI ile çözülen `IMappingAction<TSource, TDestination>` sınıfı. |

Aynı eşlemeye birden çok eylem ekleyebilirsiniz; tanımlanma sırasıyla çalışırlar. Kalıtımda taban eşlemenin eylemleri türetilmiş eşlemenin eylemlerinden önce çalışır.

## IMappingAction ile

Servis gerektiren eylemleri bir sınıfa taşıyın. Sınıf, [resolver'larla aynı kurallarla](./value-resolvers.md#resolver-örnekleri-nasıl-oluşturulur) DI'dan çözülür:

```csharp title="AuditAction.cs"
using VeloxMapper;

public sealed class AuditAction : IMappingAction<Order, OrderDto>
{
    private readonly TimeProvider _time;

    public AuditAction(TimeProvider time) => _time = time;

    public void Process(Order source, OrderDto destination, ResolutionContext context)
        => destination.MappedAt = _time.GetUtcNow();
}
```

```csharp
CreateMap<Order, OrderDto>().AfterMap<AuditAction>();
```

`AddVeloxMapper` ile taranan assembly'lerdeki `IMappingAction` uygulamaları transient olarak otomatik kaydedilir; `TimeProvider` gibi bağımlılıkları sizin kaydetmeniz gerekir.

## Çağrı düzeyinde

Tek bir `Map` çağrısına özel eylemleri işlem seçenekleriyle verebilirsiniz. Bu eylemler kök nesne için, eşleme tanımındaki eylemlerden bağımsız olarak çalışır:

```csharp
var dto = mapper.Map<Order, OrderDto>(order, opt =>
{
    opt.BeforeMap((src, dest) => logger.LogDebug("Sipariş {Id} eşleniyor", src.Id));
    opt.AfterMap((src, dest) => dest.IsPreview = true);
});
```

Çağrı düzeyindeki `BeforeMap`, kök hedef nesne oluşturulmadan önce çalışır; yeni nesne oluşturan çağrılarda `dest` parametresi `null`'dır.

## Sınırlamalar

`BeforeMap` ve `AfterMap` [ProjectTo](./projection.md) sorgularında çalışmaz; projeksiyon tek bir sorgu ifadesidir ve yan etkili kod içeremez. Sorgudan sonra işlem gerekiyorsa sonucu belleğe aldıktan sonra uygulayın.

## AutoMapper uyumluluğu

`BeforeMap`, `AfterMap`, `IMappingAction` ve işlem seçeneklerindeki `opt.BeforeMap`/`opt.AfterMap` AutoMapper ile aynıdır. VeloxMapper 5.x'ten gelen `IVeloxMappingAction` sınıfları da `BeforeMap<T>()`/`AfterMap<T>()` ile kullanılabilir.
