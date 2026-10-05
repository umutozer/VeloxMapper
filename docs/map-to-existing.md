---
title: "Mevcut Nesneye Eşleme"
description: "Map(source, destination) ile var olan bir nesneyi yerinde güncelleyin; EF Core takip edilen varlıklarını, PATCH senaryolarını ve koleksiyon güncellemelerini doğru yönetin."
section: core-concepts
order: 70
---

# Mevcut Nesneye Eşleme

`mapper.Map(source, destination)` yeni bir nesne oluşturmak yerine var olan hedef nesnenin üyelerini günceller. Bu, özellikle EF Core'dan yüklenmiş bir varlığı bir istek modeliyle güncellerken kullanılır.

## Temel kullanım

```csharp
var customer = await db.Customers.SingleAsync(c => c.Id == id);

mapper.Map(request, customer);   // customer yerinde güncellenir

await db.SaveChangesAsync();
```

Metot hedef nesneyi döndürür; dönüş değeri, verdiğiniz örnekle aynıdır:

```csharp
Customer updated = mapper.Map(request, customer);
// ReferenceEquals(updated, customer) == true
```

Çalışma zamanında türleri bilinen nesneler için `mapper.Map(source, destination, sourceType, destinationType)` overload'u vardır.

## Kurallar

| Durum | Davranış |
| --- | --- |
| Hedef `null` | Yeni bir nesne oluşturulur ve döndürülür (`Map<TDestination>(source)` ile aynı). |
| Kök kaynak `null` | Hedef **değiştirilmeden** döndürülür. |
| İç içe nesne üyesi (hedefte dolu) | Mevcut iç nesne yerinde güncellenir; yeni örnek oluşturulmaz. |
| İç içe nesne üyesi (hedefte `null`) | Yeni bir iç nesne oluşturulur. |
| Koleksiyon üyesi | Mevcut koleksiyon örneği korunur; `Clear()` ve ardından `Add()` ile yeniden doldurulur. |
| Dizi veya salt okunur koleksiyon | Yeni koleksiyon oluşturulup atanır. |
| Kurucu | Çağrılmaz; yalnızca yazılabilir üyeler güncellenir. |
| Eşleme kuralları | `CreateMap` ile tanımlanan tüm kurallar (`Ignore`, `MapFrom`, `Condition`...) aynen uygulanır. |

İç nesnelerin ve koleksiyonların örneklerinin korunması, EF Core'un değişiklik takibi açısından önemlidir: takip edilen bir navigasyon nesnesinin yerine yeni bir örnek atamak, EF Core'un onu yeni bir varlık olarak görmesine yol açabilir.

## EF Core ile güncelleme

```csharp title="UpdateOrderRequest.cs"
public class UpdateOrderRequest
{
    public OrderStatus Status { get; set; }
    public int CustomerId { get; set; }
}
```

```csharp title="OrderProfile.cs"
CreateMap<UpdateOrderRequest, Order>(MemberList.Source);
```

```csharp title="OrderEndpoints.cs"
app.MapPut("/orders/{id:int}", async (int id, UpdateOrderRequest request, ShopDbContext db, IMapper mapper) =>
{
    var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id);
    if (order is null) return Results.NotFound();

    mapper.Map(request, order);
    await db.SaveChangesAsync();

    return Results.NoContent();
});
```

`MemberList.Source`, istek modelindeki her üyenin varlıkta bir karşılığı olmasını doğrular; varlıkta istekte olmayan üyeler (ör. `Id`, `CreatedAt`) doğrulama hatası üretmez ve dokunulmadan kalır.

> [!WARNING]
> İstek modelinde olmayan ama hedefte aynı adla bulunan üyeler de güncellenir. Kullanıcıdan gelen bir modeli varlığa eşlerken yalnızca güncellenmesine izin verdiğiniz üyeleri içeren ayrı bir istek modeli kullanın (over-posting).

## PATCH: null değerleri atlama

Kısmi güncellemelerde (HTTP PATCH) istekte gönderilmeyen alanlar genellikle `null` gelir ve hedefteki değerin korunması gerekir. İki yöntem vardır.

### Condition ile (AutoMapper ile aynı)

```csharp
public class PatchCustomerRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
}
```

```csharp
CreateMap<PatchCustomerRequest, Customer>(MemberList.Source)
    .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
```

`Condition` delegesinin üçüncü parametresi, üye için çözülmüş kaynak değeridir. Değer `null` ise üye atanmaz.

### PatchMapping.IgnoreNullValues ile (VeloxMapper'a özgü)

Tüm eşlemelerde, yalnızca mevcut nesneye eşleme sırasında `null` kaynak değerlerini atlamak için:

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.PatchMapping.IgnoreNullValues = true;
    cfg.CreateMap<PatchCustomerRequest, Customer>(MemberList.Source);
});
```

```csharp
var customer = new Customer { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

mapper.Map(new PatchCustomerRequest { Email = "ada@newmail.com" }, customer);
// FirstName: "Ada", LastName: "Lovelace", Email: "ada@newmail.com"
```

Bu ayar `mapper.Map<TDestination>(source)` ile yeni nesne oluştururken etkili değildir. `NullSubstitute` tanımlı üyelerde `NullSubstitute` değeri kullanılır.

> [!NOTE]
> Nullable olmayan değer türleri (`int`, `bool`, `DateTime`) asla `null` olmadığı için her iki yöntemde de atlanmaz. PATCH modellerinde bu üyeleri nullable (`int?`) tanımlayın.

## Hedef değerini kullanma

Belirli bir üye için davranışı değiştirmek üzere:

| Seçenek | Etki |
| --- | --- |
| `opt.UseDestinationValue()` | Üyeyi mevcut hedef değeri üzerine eşler; yeni nesne oluşturmaz. `Map<T>(source)` ile oluşturmada da, hedef türün kurucusunda başlatılmış iç nesneyi kullanır. |
| `opt.DoNotUseDestinationValue()` | Mevcut nesneye eşlemede bile bu üye için yeni bir nesne veya koleksiyon oluşturur ve atar. |
| `[UseExistingValue]` | `UseDestinationValue()` ile aynı, öznitelik olarak. |

```csharp
CreateMap<OrderDto, Order>()
    .ForMember(d => d.Lines, opt => opt.DoNotUseDestinationValue());
```

## Koleksiyonları anahtara göre güncelleme

Varsayılan Clear + Add davranışı, koleksiyon elemanlarını kimliklerine göre eşleştirmez: EF Core'da mevcut alt varlıklar silinir ve yenileri eklenir. Elemanları anahtarlarına göre güncellemek (AutoMapper.Collection'daki `EqualityComparison` davranışı) için koleksiyonu kurallardan çıkarıp `AfterMap` içinde senkronize edin:

```csharp title="OrderProfile.cs"
CreateMap<UpdateOrderLineRequest, OrderLine>();

CreateMap<UpdateOrderWithLinesRequest, Order>(MemberList.Source)
    .ForSourceMember(s => s.Lines, opt => opt.DoNotValidate())
    .ForMember(d => d.Lines, opt => opt.Ignore())
    .AfterMap((src, dest, context) => SyncLines(src.Lines, dest.Lines, context.Mapper));
```

```csharp title="OrderProfile.cs"
private static void SyncLines(List<UpdateOrderLineRequest> source, List<OrderLine> destination, IMapper mapper)
{
    // Kaynakta olmayan satırları kaldır
    destination.RemoveAll(line => source.All(s => s.Id != line.Id));

    foreach (var item in source)
    {
        var existing = destination.SingleOrDefault(line => line.Id == item.Id && item.Id != 0);
        if (existing is not null)
        {
            mapper.Map(item, existing);                 // var olan satırı yerinde güncelle
        }
        else
        {
            destination.Add(mapper.Map<OrderLine>(item)); // yeni satır ekle
        }
    }
}
```

Bu desen mevcut `OrderLine` varlıklarının referansını korur; EF Core yalnızca değişen kolonlar için `UPDATE`, yeni satırlar için `INSERT` ve kaldırılanlar için `DELETE` üretir.

## İlgili sayfalar

- [Koleksiyonlar](./collections.md)
- [Conditional Mapping](./conditional-mapping.md)
- [Null Handling](./null-handling.md)
- [IMapper API referansı](./api/imapper.md)
