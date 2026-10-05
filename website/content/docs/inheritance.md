---
title: "Kalıtım & Polimorfizm"
description: "Türetilmiş türleri Include, IncludeBase, IncludeAllDerived ve As ile polimorfik olarak eşleyin."
section: advanced
order: 10
---

# Kalıtım & Polimorfizm

Kaynak ve hedef modelleriniz bir kalıtım hiyerarşisi oluşturuyorsa, taban eşlemedeki kuralları her türetilmiş eşlemede tekrar yazmanız gerekmez. VeloxMapper taban kuralları türetilmiş eşlemelere devreder ve `Map<BaseDto>(derived)` çağrısında nesnenin çalışma zamanı türüne uygun eşlemeyi seçer.

## Örnek model

```csharp title="Models.cs"
public abstract class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class CardPayment : Payment { public string MaskedPan { get; set; } = ""; }
public class BankTransfer : Payment { public string Iban { get; set; } = ""; }

public abstract class PaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "";
}

public class CardPaymentDto : PaymentDto { public string MaskedPan { get; set; } = ""; }
public class BankTransferDto : PaymentDto { public string Iban { get; set; } = ""; }
```

## Include: tabandan türetilmişlere

Taban eşlemede türetilmiş tür çiftlerini `Include` ile bildirin. Türetilmiş eşlemeler ayrıca `CreateMap` ile tanımlanmalıdır:

```csharp title="PaymentProfile.cs"
public class PaymentProfile : Profile
{
    public PaymentProfile()
    {
        CreateMap<Payment, PaymentDto>()
            .ForMember(d => d.Kind, o => o.MapFrom(s => s.GetType().Name))
            .Include<CardPayment, CardPaymentDto>()
            .Include<BankTransfer, BankTransferDto>();

        CreateMap<CardPayment, CardPaymentDto>();
        CreateMap<BankTransfer, BankTransferDto>();
    }
}
```

```csharp
var payments = new List<Payment>
{
    new CardPayment { Id = 1, Amount = 100, MaskedPan = "**** 4242" },
    new BankTransfer { Id = 2, Amount = 250, Iban = "TR00 0000" }
};

var dtos = mapper.Map<List<PaymentDto>>(payments);
// dtos[0] → CardPaymentDto { Kind = "CardPayment", MaskedPan = "**** 4242" }
// dtos[1] → BankTransferDto { Kind = "BankTransfer", Iban = "TR00 0000" }
```

`Kind` kuralı yalnızca taban eşlemede tanımlıdır ama türetilmiş sonuçlarda da uygulanır.

## IncludeBase: türetilmişten tabana

Aynı ilişkiyi türetilmiş eşleme tarafından da kurabilirsiniz. Bu yazım, türetilmiş eşlemeler farklı dosyalarda veya profillerde tanımlandığında kullanışlıdır:

```csharp
CreateMap<Payment, PaymentDto>()
    .ForMember(d => d.Kind, o => o.MapFrom(s => s.GetType().Name));

CreateMap<CardPayment, CardPaymentDto>().IncludeBase<Payment, PaymentDto>();
CreateMap<BankTransfer, BankTransferDto>().IncludeBase<Payment, PaymentDto>();
```

`IncludeBase` ile tanımlanan türetilmiş eşleme, taban üzerinden yapılan polimorfik çağrılarda da (`Map<Payment, PaymentDto>(cardPayment)`) otomatik olarak seçilir; ayrıca `Include` yazmanız gerekmez.

## IncludeAllDerived

Tüm türetilmiş eşlemeleri tek tek yazmak yerine taban eşlemede `IncludeAllDerived()` çağırın. Yapılandırmadaki, kaynak ve hedef türü tabandan türeyen tüm eşlemeler dahil edilir:

```csharp
CreateMap<Payment, PaymentDto>()
    .ForMember(d => d.Kind, o => o.MapFrom(s => s.GetType().Name))
    .IncludeAllDerived();

CreateMap<CardPayment, CardPaymentDto>();
CreateMap<BankTransfer, BankTransferDto>();
```

## Neler devralınır

Türetilmiş eşleme, taban eşlemeden şunları devralır:

| Devralınan | Kural |
| --- | --- |
| `ForMember` kuralları | Türetilmiş eşlemede aynı üye için kural yoksa |
| `ForPath` kuralları | Aynı yol türetilmiş eşlemede tanımlı değilse |
| `ForCtorParam` kuralları | Aynı parametre türetilmiş eşlemede tanımlı değilse |
| `BeforeMap` / `AfterMap` | Taban eylemleri önce çalışır |
| `AddTransform` | Taban transformer'ları önce uygulanır |
| `MaxDepth`, `PreserveReferences`, `IncludeMembers` | Türetilmiş eşlemede verilmemişse |

Türetilmiş eşlemede aynı üye için yazdığınız kural her zaman önceliklidir.

## Çalışma zamanı türüne göre seçim

`Map<Payment, PaymentDto>(payment)` veya `Map<PaymentDto>(payment)` çağrısında `payment` bir `CardPayment` ise `CardPayment → CardPaymentDto` eşlemesi kullanılır. Koleksiyon öğeleri (`Map<List<PaymentDto>>(payments)`) ve iç içe üyeler de aynı şekilde öğenin çalışma zamanı türüne göre eşlenir. Birden çok seviyeli hiyerarşilerde en özel (en derin) tür önce denenir.

Hedef tür soyutsa (`abstract class PaymentDto`) ve kaynağın çalışma zamanı türü için bir `Include` eşlemesi bulunamazsa `VeloxMappingException` fırlatılır:

```text
'PaymentDto' soyut hedef türü için 'CashPayment' kaynağına uygun bir Include<,>() eşleştirmesi bulunamadı.
```

> [!NOTE]
> Polimorfik seçim yalnızca yeni nesne oluşturulurken yapılır. `Map(source, destination)` çağrısında mevcut hedef nesnenin türü kullanılır.

## As: hedefi yönlendirmek

`As<T>()`, bir eşlemenin sonucunu her zaman türetilmiş bir hedef türe yönlendirir. Taban hedef türü istenen ama somut bir tür üretmesi gereken eşlemelerde kullanılır:

```csharp
CreateMap<Payment, PaymentDto>().As<CardPaymentDto>();
CreateMap<Payment, CardPaymentDto>()
    .ForMember(d => d.Kind, o => o.MapFrom(_ => "Kart"))
    .ForMember(d => d.MaskedPan, o => o.Ignore());

PaymentDto dto = mapper.Map<PaymentDto>(new BankTransfer { Id = 3 }); // CardPaymentDto örneği
```

`As<T>()` kullandığınızda kaynak türden yönlendirilen türe ayrı bir eşleme (`CreateMap<Payment, CardPaymentDto>()`) tanımlamanız gerekir; üye kuralları bu eşlemeden alınır.

## Arayüz kaynak türleri

Kaynak tür bir arayüz olabilir. `CreateMap<IHasName, NameDto>()` tanımı, `IHasName` uygulayan her nesne için kullanılır. Kaynak tür için doğrudan bir eşleme bulunamazsa VeloxMapper önce taban sınıfları, ardından uygulanan arayüzleri arar.

## AutoMapper uyumluluğu

`Include`, `IncludeBase`, `IncludeAllDerived`, `As` ve tür parametreli (`Include(Type, Type)`, `IncludeBase(Type, Type)`) overload'lar AutoMapper ile aynıdır. Fark: AutoMapper'ın `AsProxy` ile arayüz hedeflerine proxy üretmesi desteklenmez; hedef soyut bir tür veya arayüzse somut bir tür eşlemesi gerekir.
