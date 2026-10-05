---
title: "Koleksiyonlar"
description: "Liste, dizi, küme, sözlük ve özel koleksiyon türlerinin nasıl eşlendiğini; null koleksiyon kurallarını ve mevcut koleksiyonların yerinde güncellenmesini öğrenin."
section: core-concepts
order: 40
---

# Koleksiyonlar

VeloxMapper koleksiyonlar için ayrı bir yapılandırma istemez. Eleman türleri için bir eşleme (veya konvansiyonla eşlenebilir türler) yeterlidir; koleksiyonun kendisi otomatik olarak eşlenir.

## Temel kullanım

```csharp
cfg.CreateMap<OrderLine, OrderLineDto>();
```

Yalnızca eleman türü için tanım yaptınız. Koleksiyonları doğrudan veya bir nesnenin üyesi olarak eşleyebilirsiniz:

```csharp
List<OrderLineDto> list = mapper.Map<List<OrderLineDto>>(order.Lines);
OrderLineDto[] array = mapper.Map<OrderLineDto[]>(order.Lines);
IEnumerable<OrderLineDto> sequence = mapper.Map<IEnumerable<OrderLineDto>>(order.Lines);
IReadOnlyList<OrderLineDto> readOnly = mapper.Map<IReadOnlyList<OrderLineDto>>(order.Lines);
```

Kaynak koleksiyonun türü önemli değildir: `IEnumerable<T>` uygulayan her tür kaynak olabilir.

## Desteklenen hedef türleri

| Hedef tür | Oluşturulan örnek |
| --- | --- |
| `T[]` | Dizi |
| `List<T>` | `List<T>` |
| `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>` | `List<T>` |
| `HashSet<T>`, `ISet<T>` | `HashSet<T>` |
| `ObservableCollection<T>` ve parametresiz kurucusu olan her `ICollection<T>` uygulaması | İlgili tür (elemanlar `Add` ile eklenir) |
| `Dictionary<TKey, TValue>`, `IDictionary<TKey, TValue>`, `IReadOnlyDictionary<TKey, TValue>` | `Dictionary<TKey, TValue>` |

Desteklenmeyen bir koleksiyon türü (ör. parametresiz kurucusu olmayan özel bir koleksiyon) hedef olarak kullanılırsa eşleme kodu üretilirken (ilk `Map` çağrısında veya `CompileMappings()` sırasında) `VeloxConfigurationException` fırlatılır; mesaj desteklenen türleri listeler.

## Sözlükler

Sözlük eşlemesinde anahtarlar ve değerler ayrı ayrı eşlenir. Her ikisi de tür dönüşümünden geçebilir:

```csharp
public class PriceList
{
    public Dictionary<string, decimal> Prices { get; set; } = new();
    public Dictionary<int, OrderLine> LinesById { get; set; } = new();
}

public class PriceListDto
{
    public IReadOnlyDictionary<string, double> Prices { get; set; } = new Dictionary<string, double>();
    public Dictionary<int, OrderLineDto> LinesById { get; set; } = new();
}
```

```csharp
cfg.CreateMap<OrderLine, OrderLineDto>();
cfg.CreateMap<PriceList, PriceListDto>();
```

## Null koleksiyonlar

| Durum | Sonuç |
| --- | --- |
| Kaynak koleksiyon `null`, `AllowNullCollections = false` (varsayılan) | Boş koleksiyon |
| Kaynak koleksiyon `null`, `AllowNullCollections = true` | `null` |
| Kök çağrıda kaynak `null` (`mapper.Map<List<OrderLineDto>>(null)`) | Boş liste; `AllowNullCollections = true` ise `null` |
| Koleksiyon içindeki `null` eleman | `null` eleman (sınıf türleri için) |

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.AllowNullCollections = true;
    cfg.CreateMap<Order, OrderDto>();
});
```

`AllowNullCollections` profil düzeyinde de ayarlanabilir.

## Setter'ı olmayan koleksiyon property'leri

Hedefte yalnızca getter'ı olan ve başlatılmış bir koleksiyon property'si varsa VeloxMapper mevcut örneği temizleyip yeniden doldurur:

```csharp
public class CartDto
{
    public ObservableCollection<string> Tags { get; } = new();  // setter yok
}
```

```csharp
cfg.CreateMap<Cart, CartDto>(); // Tags yerinde doldurulur
```

Bu davranış, koleksiyonu kurucuda oluşturan ve dışarıya setter açmayan alan modelleri (domain model) için kullanışlıdır.

## Mevcut koleksiyona eşleme

`mapper.Map(source, destination)` çağrısında hedefteki koleksiyon örneği korunur: koleksiyon `Clear()` ile temizlenir ve eşlenmiş elemanlar `Add()` ile eklenir. Bu, EF Core'un takip ettiği navigasyon koleksiyonlarının referansının değişmemesini sağlar.

```csharp
var lines = order.Lines;
mapper.Map(updateDto, order);
// ReferenceEquals(lines, order.Lines) == true
```

Hedef koleksiyon salt okunursa (ör. dizi veya `IsReadOnly` olan bir koleksiyon) yeni bir koleksiyon oluşturulur ve atanır.

> [!NOTE]
> Clear + Add, hedef elemanları kimliklerine göre eşleştirmez; tüm elemanlar yeniden oluşturulur. EF Core'da bu, mevcut alt varlıkların silinip yenilerinin eklenmesi anlamına gelir. Elemanları anahtarlarına göre güncellemek için [Mevcut Nesneye Eşleme](./map-to-existing.md#koleksiyonları-anahtara-göre-güncelleme) sayfasındaki deseni kullanın.

## Polimorfik elemanlar

Eleman eşlemesinde `Include` veya `IncludeAllDerived` tanımlıysa her eleman çalışma zamanı türüne göre eşlenir:

```csharp
cfg.CreateMap<Payment, PaymentDto>()
    .Include<CardPayment, CardPaymentDto>()
    .Include<BankTransfer, BankTransferDto>();
cfg.CreateMap<CardPayment, CardPaymentDto>();
cfg.CreateMap<BankTransfer, BankTransferDto>();

List<PaymentDto> dtos = mapper.Map<List<PaymentDto>>(payments); // elemanlar CardPaymentDto / BankTransferDto
```

Bkz. [Kalıtım](./inheritance.md).

## ProjectTo ile koleksiyonlar

`ProjectTo` sorgularında koleksiyon üyeleri hedef türüne göre `ToList()`, `ToArray()` veya `HashSet` olarak projekte edilir ve EF Core bunları alt sorgulara çevirir. Bkz. [Projection](./projection.md).

## İlgili sayfalar

- [Mevcut Nesneye Eşleme](./map-to-existing.md)
- [Dictionary Mapping](./dictionary-mapping.md)
- [Null Handling](./null-handling.md)
