---
title: "Open Generic"
description: "Page<T>, Result<T> gibi generic türler için tek bir açık eşleme tanımlayın; her kapalı tür için ayrı CreateMap yazmayın."
section: advanced
order: 40
---

# Open Generic

`Page<T>`, `ApiResponse<T>` veya `Envelope<T>` gibi generic sarmalayıcı türleriniz varsa her `T` için ayrı `CreateMap` yazmak yerine tür tanımları arasında tek bir open generic eşleme tanımlarsınız. İlk kullanımda eşleme, çağrıdaki kapalı türler için otomatik olarak üretilir.

## Tanımlama

Open generic eşlemeler `Type` alan `CreateMap` overload'u ile tanımlanır:

```csharp title="Models.cs"
public class Page<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
}

public class PageDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
}
```

```csharp title="CommonProfile.cs"
public class CommonProfile : Profile
{
    public CommonProfile()
    {
        CreateMap(typeof(Page<>), typeof(PageDto<>));
        CreateMap<Product, ProductDto>();
    }
}
```

```csharp
Page<Product> page = await repository.GetPageAsync(pageNumber: 1);
PageDto<ProductDto> dto = mapper.Map<PageDto<ProductDto>>(page);
```

`Page<Product>` → `PageDto<ProductDto>` eşlemesi ilk çağrıda kapatılır. `Items` listesi için öğe türü eşlemesi (`Product → ProductDto`) normal kurallarla bulunur.

## Üye kuralları

Non-generic `IMappingExpression` arayüzü üye adlarını string olarak alır:

```csharp
CreateMap(typeof(Page<>), typeof(PageDto<>))
    .ForMember("PageNumber", o => o.MapFrom("Index"))
    .ForMember("Debug", o => o.Ignore());
```

`MapFrom("Path")` yolu, eşleme kapatılırken gerçek kaynak türü üzerinde çözülür. `ForAllMembers`, `ForAllOtherMembers`, `MaxDepth` ve `ReverseMap` gibi non-generic `IMappingExpression` yöntemleri de open generic eşlemelerde kullanılabilir. Tüm yöntemler için [Mapping Expression API](./api/mapping-expression.md#non-generic-imappingexpression) sayfasına bakın.

## Generic olmayan tarafla

Yalnızca bir taraf generic olabilir:

```csharp
CreateMap(typeof(ApiResponse<>), typeof(ResponseEnvelope)); // ApiResponse<T> → ResponseEnvelope
```

## Open generic tür dönüştürücü

Tür dönüştürücünün kendisi de open generic olabilir. Dönüştürücünün tür parametreleri, eşlenen kapalı türlerin generic argümanlarından doldurulur:

```csharp
public sealed class PageConverter<T> : ITypeConverter<Page<T>, PageDto<T>>
{
    public PageDto<T> Convert(Page<T> source, PageDto<T> destination, ResolutionContext context)
        => new() { Items = source.Items, TotalCount = source.TotalCount, PageNumber = source.PageNumber };
}

CreateMap(typeof(Page<>), typeof(PageDto<>)).ConvertUsing(typeof(PageConverter<>));
```

Dönüştürücünün generic parametre sayısı; kaynak ve hedefin toplam generic argüman sayısına, yalnızca kaynağınkine veya yalnızca hedefinkine eşit olmalıdır. Kapatılamazsa `VeloxConfigurationException` fırlatılır.

## ReverseMap

```csharp
CreateMap(typeof(Page<>), typeof(PageDto<>)).ReverseMap();

var back = mapper.Map<Page<string>>(new PageDto<string> { Items = { "a" }, TotalCount = 1 });
```

## Doğrulama ve teşhis

Open generic tanımlar [AssertConfigurationIsValid](./configuration-validation.md) tarafından doğrulanmaz, çünkü üyeler ancak kapalı türlerle bilinir. Kapalı türlerle kullanılan bir eşlemeyi doğrulamak için o kapalı tür çiftine bir test yazın veya [GetMappingPlan](./diagnostics.md) ile planını inceleyin.

`MapperConfiguration.GetAllTypeMaps()` ve `RegistrationCount` open generic tanımları içermez; çalışma zamanında kapatılan eşlemeler ilk kullanımdan sonra listeye eklenir.

## AutoMapper uyumluluğu

`CreateMap(typeof(A<>), typeof(B<>))`, string tabanlı `ForMember`, `ConvertUsing(typeof(Converter<>))` ve `ReverseMap` AutoMapper ile aynıdır.
