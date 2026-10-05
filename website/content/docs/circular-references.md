---
title: "Döngüsel Referanslar"
description: "Kendine veya birbirine referans veren nesne grafiklerini StackOverflow olmadan eşleyin; derinliği MaxDepth ile sınırlayın."
section: advanced
order: 70
---

# Döngüsel Referanslar

Ebeveyn–çocuk ilişkileri, ağaç yapıları ve EF Core navigation property'leri çoğu zaman döngüsel nesne grafikleri oluşturur. VeloxMapper bu grafikleri otomatik olarak algılar ve aynı kaynak nesneyi tekrar eşlemek yerine daha önce oluşturduğu hedef örneğini kullanır.

## Otomatik referans koruması

```csharp title="Models.cs"
public class Category
{
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
}

public class CategoryDto
{
    public string Name { get; set; } = "";
    public CategoryDto? Parent { get; set; }
    public List<CategoryDto> Children { get; set; } = new();
}
```

```csharp
var config = new MapperConfiguration(cfg => cfg.CreateMap<Category, CategoryDto>());

var root = new Category { Name = "Elektronik" };
var child = new Category { Name = "Telefon", Parent = root };
root.Children.Add(child);

var dto = config.CreateMapper().Map<CategoryDto>(root);
// dto.Children[0].Parent, dto ile aynı örnektir; StackOverflowException oluşmaz.
```

Yapılandırma oluşturulurken eşlemeler arasındaki tür ilişkileri incelenir. Bir eşleme, üyeleri aracılığıyla (doğrudan veya başka eşlemeler üzerinden) kendisine geri dönüyorsa o eşleme için `PreserveReferences` otomatik olarak açılır. `MaxDepth` tanımlı eşlemelerde otomatik açılmaz.

## PreserveReferences

Döngü algılaması yalnızca açık `CreateMap` tanımları arasında yapılır. Grafikte aynı nesneye birden çok yoldan ulaşılıyorsa ve hedefte de aynı örneğin paylaşılmasını istiyorsanız referans korumasını açıkça etkinleştirin:

```csharp
cfg.CreateMap<Employee, EmployeeDto>().PreserveReferences();
```

Referans koruması tek bir `Map` çağrısı içinde geçerlidir; kaynak nesne referans eşitliğiyle (`ReferenceEquals`) izlenir. Ayrı `Map` çağrıları arasında hedef örnekleri paylaşılmaz.

> [!NOTE]
> Referans koruması her eşlenen nesne için bir sözlük araması ekler. Döngü içermeyen büyük grafiklerde gereksiz yere açmayın.

## MaxDepth

`MaxDepth(n)` rekürsif bir eşlemenin en fazla kaç seviye derinliğe ineceğini sınırlar. Sınırın altındaki seviyeler hedefte `null` (veya varsayılan değer) olur:

```csharp
cfg.CreateMap<Category, CategoryDto>().MaxDepth(2);

var tree = new Category { Name = "1", Parent = new Category { Name = "2", Parent = new Category { Name = "3" } } };
var dto = mapper.Map<CategoryDto>(tree);
// dto.Name → "1", dto.Parent.Name → "2", dto.Parent.Parent → null
```

`MaxDepth` hem bellek içi eşlemede hem de [ProjectTo](./projection.md#rekürsif-modeller-ve-maxdepth) sorgularında çalışır. `ProjectTo` ile rekürsif bir model kullanıyorsanız `MaxDepth` zorunludur; tanımlanmazsa sorgu üretilirken `VeloxProjectionException` fırlatılır.

## Hangisini kullanmalı

| Senaryo | Öneri |
| --- | --- |
| Ebeveyn–çocuk, iki yönlü navigation property'ler | Varsayılan davranış yeterli |
| Paylaşılan alt nesneler (aynı adres birden çok kişide) | `PreserveReferences()` |
| Derin ağaçlarda yalnızca ilk birkaç seviye gerekiyor | `MaxDepth(n)` |
| `ProjectTo` ile rekürsif model | `MaxDepth(n)` (zorunlu) |
| API yanıtında döngü istemiyorsanız (JSON serileştirme) | Döngüyü DTO tasarımında kırın: `ParentId` gibi tanımlayıcı kullanın |

Hedef nesnelerde döngü kalırsa `System.Text.Json` serileştirmesi varsayılan ayarlarla hata verir. DTO'larınızı döngü içermeyecek şekilde tasarlamak çoğu zaman en temiz çözümdür.

## AutoMapper uyumluluğu

`PreserveReferences` ve `MaxDepth` AutoMapper ile aynıdır. Döngüsel tür grafiklerinde `PreserveReferences`'ın otomatik açılması da AutoMapper davranışıyla aynıdır.
