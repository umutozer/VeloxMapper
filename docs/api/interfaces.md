---
title: "Genişletme Arayüzleri"
description: "IValueResolver, IMemberValueResolver, IValueConverter, ITypeConverter, IMappingAction, ResolutionContext, isimlendirme kuralları ve 5.x IVelox* arayüzlerinin referansı."
section: api
order: 60
---

# Genişletme Arayüzleri

Eşleme davranışını kendi kodunuzla genişletmek için bu arayüzleri uygularsınız. Arayüz adları, tür parametreleri ve metot imzaları AutoMapper ile aynıdır; uygulamalarınız yalnızca `using` satırı değiştirilerek derlenir.

| Arayüz | Amaç | Bağlandığı yer |
| --- | --- | --- |
| `IValueResolver<TSource, TDestination, TDestMember>` | Hedef üye değerini kaynak nesneden üretmek | `o.MapFrom<TResolver>()` |
| `IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember>` | Hedef üye değerini seçilen bir kaynak üyeden üretmek | `o.MapFrom<TResolver, TSourceMember>(s => s.X)` |
| `IValueConverter<TSourceMember, TDestinationMember>` | Tek bir üye değerini dönüştürmek | `o.ConvertUsing<TConverter, TSourceMember>()` |
| `ITypeConverter<TSource, TDestination>` | Bir tür çiftinin eşlemesini tamamen devralmak | `CreateMap<,>().ConvertUsing<TConverter>()` |
| `IMappingAction<TSource, TDestination>` | Eşleme öncesi/sonrası işlem | `.BeforeMap<TAction>()`, `.AfterMap<TAction>()` |

Tür olarak verilen uygulamalar her eşlemede çözülür: önce `ConstructServicesUsing` fabrikası, sonra DI konteyneri, DI'da kayıtlı değilse `ActivatorUtilities`, DI yoksa parametresiz kurucu. `AddVeloxMapper` ile taranan assembly'lerdeki uygulamalar transient olarak otomatik kaydedilir. Bkz. [Dependency Injection API](./dependency-injection.md).

## IValueResolver

```csharp
public interface IValueResolver<in TSource, in TDestination, TDestMember>
{
    TDestMember Resolve(TSource source, TDestination destination, TDestMember destMember, ResolutionContext context);
}
```

| Parametre | Açıklama |
| --- | --- |
| `source` | Kaynak nesne. |
| `destination` | Oluşturulmakta veya güncellenmekte olan hedef nesne. |
| `destMember` | Hedef üyenin mevcut değeri. |
| `context` | Çağrı bağlamı. |

```csharp
public sealed class FullNameResolver : IValueResolver<Customer, CustomerDto, string>
{
    public string Resolve(Customer source, CustomerDto destination, string destMember, ResolutionContext context)
        => $"{source.FirstName} {source.LastName}".Trim();
}
```

Kullanım ve örnekler: [Value Resolver](../value-resolvers.md). AutoMapper ile aynı.

## IMemberValueResolver

```csharp
public interface IMemberValueResolver<in TSource, in TDestination, in TSourceMember, TDestMember>
{
    TDestMember Resolve(TSource source, TDestination destination, TSourceMember sourceMember, TDestMember destMember, ResolutionContext context);
}
```

`sourceMember`, `MapFrom<TResolver, TSourceMember>(...)` ile seçilen kaynak üyenin değeridir. Aynı mantığı farklı üyelere uygulamak için kullanılır.

AutoMapper ile aynı.

## IValueConverter

```csharp
public interface IValueConverter<in TSourceMember, out TDestinationMember>
{
    TDestinationMember Convert(TSourceMember sourceMember, ResolutionContext context);
}
```

Yalnızca üye değerini alır; kaynak ve hedef nesneleri görmez. Kullanım: [Value Converter](../value-converters.md).

AutoMapper ile aynı.

## ITypeConverter

```csharp
public interface ITypeConverter<in TSource, TDestination>
{
    TDestination Convert(TSource source, TDestination destination, ResolutionContext context);
}
```

`destination`, `Map(source, destination)` çağrısında mevcut hedeftir; yeni nesne oluşturulurken `default`'tur. Tanımlandığı tür çifti her yerde (kök, iç içe üye, koleksiyon öğesi) bu dönüştürücüyle eşlenir. Open generic dönüştürücüler (`typeof(PageConverter<>)`) desteklenir. Kullanım: [Type Converter](../type-converters.md).

AutoMapper ile aynı.

## IMappingAction

```csharp
public interface IMappingAction<in TSource, in TDestination>
{
    void Process(TSource source, TDestination destination, ResolutionContext context);
}
```

`BeforeMap<TAction>()` ile üye atamalarından önce, `AfterMap<TAction>()` ile sonra çalışır. Kullanım: [Before/After Map](../before-after-map.md).

AutoMapper ile aynı.

## ResolutionContext

Resolver, converter, action, koşul ve fonksiyon tabanlı `MapFrom` çağrılarına aktarılan bağlam nesnesi.

```csharp
public class ResolutionContext
{
    public IMapper Mapper { get; }
    public IServiceProvider? ServiceProvider { get; }
    public IDictionary<string, object> Items { get; }
    public object? State { get; set; }
    public string? CurrentMember { get; set; }
    public bool TryGetItems(out IDictionary<string, object> items);
}
```

| Üye | Açıklama |
| --- | --- |
| `Mapper` | Eşlemeyi yürüten mapper. İç içe eşleme için kullanılabilir: `context.Mapper.Map<AddressDto>(src.Address)`. |
| `ServiceProvider` | Resolver ve converter'ların çözüldüğü servis sağlayıcı; DI dışında `null` olabilir. |
| `Items` | `opt.Items[...]` ile verilen çağrı parametreleri. Seçenek verilmemişse boş bir sözlük oluşturulur. |
| `State` | `opt.State` ile verilen nesne. |
| `CurrentMember` | O an eşlenen hedef üyenin adı (hata mesajları için). |
| `TryGetItems(out items)` | `Items` en az bir öğe içeriyorsa `true` döner; sözlüğü gereksiz yere oluşturmaz. |

> [!IMPORTANT]
> Bağlam örnekleri motor tarafından havuzlanır ve yeniden kullanılır. Bir `Map` çağrısı tamamlandıktan sonra bağlama veya `Items` sözlüğüne referans tutmayın.

`context.Mapper.Map(...)` ile resolver içinden başlatılan eşleme yeni bir bağlam kullanır; dış çağrının `Items` sözlüğü iç çağrıya aktarılmaz. Gerekiyorsa iç çağrıya `opt => opt.Items[...]` ile tekrar verin.

AutoMapper ile aynı ad ve üyeler. Fark: AutoMapper'da seçenek verilmeden `Items`'a erişmek hata fırlatır.

## İsimlendirme kuralları

```csharp
namespace VeloxMapper.Abstractions;

public interface ICustomNamingConvention
{
    string Normalize(string propertyName);
}
```

`Normalize`, karşılaştırmada kullanılacak aday adı döndürür. Hazır uygulamalar `VeloxMapper` namespace'indedir:

| Sınıf | Davranış |
| --- | --- |
| `PascalCaseNamingConvention` | Adı olduğu gibi kullanır (boşlukları atar). |
| `LowerUnderscoreNamingConvention` | `first_name` → `FirstName`. |
| `ExactMatchNamingConvention` | Adı değiştirmez. |

Her sınıfın `Instance` adında tekil bir örneği vardır. Kullanım: [İsimlendirme Kuralları](../naming-conventions.md).

AutoMapper'daki hazır sınıflarla aynı adlar. Fark: AutoMapper'da özel kurallar `INamingConvention` arayüzüyle yazılır.

## IVeloxDiagnosticsSink

```csharp
namespace VeloxMapper.Abstractions;

public interface IVeloxDiagnosticsSink
{
    void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null);
}
```

Teşhis olaylarını (ifade üretimi, `ProjectTo`'nun atladığı üyeler) almak için uygulanır ve `cfg.DiagnosticsSink` ile verilir. Bkz. [Teşhis](../diagnostics.md#diagnosticssink).

VeloxMapper'a özgü.

## Velox 5.x arayüzleri

VeloxMapper 5.x ile yazılmış kodun derlenmeye devam etmesi için aşağıdaki arayüzler korunur. Motor bunları AutoMapper uyumlu karşılıklarıyla aynı şekilde tanır; yeni kodda soldaki sütundaki arayüzleri kullanın.

| AutoMapper uyumlu (önerilen) | 5.x karşılığı | Fark |
| --- | --- | --- |
| `IMapper` | `IVeloxMapper` | `IVeloxMapper : IMapper`; ek üye yok. DI'da ikisi de kayıtlıdır. |
| `Profile` | `VeloxProfile` | `Profile : VeloxProfile`. |
| `ResolutionContext` | `VeloxResolutionContext` (`VeloxMapper.Abstractions`) | `VeloxResolutionContext : ResolutionContext`; motorun oluşturduğu tüm bağlamlar bu türdedir. |
| `IValueResolver<,,>` | `IVeloxValueResolver<,,>` | `Resolve` son parametresi `VeloxResolutionContext`. |
| `IMemberValueResolver<,,,>` | `IVeloxMemberValueResolver<,,,>` | Aynı fark. |
| `IValueConverter<,>` | `IVeloxValueConverter<,>` | Aynı fark. Üyeye `o.ConvertUsing(converter, s => s.Member)` ile bağlanır. |
| `ITypeConverter<,>` | `IVeloxTypeConverter<,>` | `TDestination Convert(TSource? source)`; bağlam ve mevcut hedef almaz. `ConvertUsing(converter)` veya `cfg.AddCustomConverter(converter)` ile bağlanır. |
| `IMappingAction<,>` | `IVeloxMappingAction<,>` | `Process` son parametresi `VeloxResolutionContext`. |
| `IMapperConfigurationExpression` | `VeloxMapperOptions` | `VeloxMapperOptions` bu arayüzü uygular ve yapılandırma delegesinin somut türüdür. |

`IVelox*` arayüzleri `VeloxMapper.Abstractions` namespace'indedir.
