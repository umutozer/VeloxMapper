// Dokümantasyon doğrulaması sırasında bulunan hataların regresyon testleri.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

namespace VeloxMapper.Tests.Migration.Regression;

public class Payment { public decimal Amount { get; set; } }
public class CardPayment : Payment { public string Last4 { get; set; } = ""; }
public abstract class PaymentDto { public decimal Amount { get; set; } }
public class CashPaymentDto : PaymentDto { }
public class CardPaymentDto : PaymentDto { public string Last4 { get; set; } = ""; }
public class PlainPaymentDto { public decimal Amount { get; set; } }
public class PlainCardPaymentDto : PlainPaymentDto { public string Last4 { get; set; } = ""; }

public class Item { public int Id { get; set; } public string? Name { get; set; } public List<Tag> Tags { get; set; } = new(); }
public class Tag { public int Id { get; set; } public string Label { get; set; } = ""; }
public class ItemDto { public int Id { get; set; } public string? Name { get; set; } public int TagsCount { get; set; } public string? Viewer { get; set; } }

public class ItemContext : DbContext
{
    public ItemContext(DbContextOptions<ItemContext> options) : base(options) { }
    public DbSet<Item> Items => Set<Item>();
}

public class Product { public string Name { get; set; } = ""; }
public class ProductDto { public string Name { get; set; } = ""; }
public class Basket { public Product Product { get; set; } = new(); }
public class BasketDto { public ProductDto Product { get; set; } = new(); }

public class RegressionFixesTests
{
    [Fact]
    public void Map_object_kaynak_calisma_zamani_turune_gore_turetilmis_eslemeyi_secer()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Payment, PaymentDto>().Include<CardPayment, CardPaymentDto>().As<CashPaymentDto>();
            cfg.CreateMap<Payment, CashPaymentDto>();
            cfg.CreateMap<CardPayment, CardPaymentDto>();
            cfg.CreateMap<Payment, PlainPaymentDto>().Include<CardPayment, PlainCardPaymentDto>();
            cfg.CreateMap<CardPayment, PlainCardPaymentDto>();
        });
        var mapper = config.CreateMapper();

        var card = new CardPayment { Amount = 5, Last4 = "4242" };
        Assert.Equal("4242", Assert.IsType<CardPaymentDto>(mapper.Map<PaymentDto>(card)).Last4);
        Assert.Equal("4242", Assert.IsType<PlainCardPaymentDto>(mapper.Map<PlainPaymentDto>(card)).Last4);
        Assert.IsType<CashPaymentDto>(mapper.Map<PaymentDto>(new Payment { Amount = 1 }));
    }

    [Fact]
    public void ProjectTo_EF_Core_ile_string_transformer_ve_parametre()
    {
        string? viewer = null;
        var config = new MapperConfiguration(cfg =>
        {
            cfg.ValueTransformers.Add<string>(s => s.Trim());
            cfg.CreateMap<Item, ItemDto>().ForMember(d => d.Viewer, o => o.MapFrom(s => viewer));
        });

        var options = new DbContextOptionsBuilder<ItemContext>().UseInMemoryDatabase("items-" + Guid.NewGuid()).Options;
        using var db = new ItemContext(options);
        db.Items.Add(new Item { Name = "  kalem  ", Tags = { new Tag { Label = "a" }, new Tag { Label = "b" } } });
        db.Items.Add(new Item { Name = null });
        db.SaveChanges();

        var items = db.Items.OrderBy(i => i.Id).ProjectTo<ItemDto>(config, new { viewer = "admin" }).ToList();

        Assert.Equal("kalem", items[0].Name);
        Assert.Equal(2, items[0].TagsCount);
        Assert.Null(items[1].Name);
        Assert.Equal("admin", items[0].Viewer);
    }

    [Fact]
    public void ProjectTo_parametreleri_sabit_yerine_sarmalayici_uye_erisimi_olarak_gomulur()
    {
        string? viewer = null;
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>().ForMember(d => d.Viewer, o => o.MapFrom(s => viewer)));
        var query = new List<Item>().AsQueryable().ProjectTo<ItemDto>(config, new { viewer = "x" });

        Assert.DoesNotContain("\"x\"", query.Expression.ToString());
        Assert.Contains(".Value", query.Expression.ToString());
    }

    [Fact]
    public void Onceden_derlenmis_mapper_ic_uyelerde_de_kullanilir()
    {
        var calls = 0;
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Basket, BasketDto>());
        config.RegisterPrecompiledMapper<Product, ProductDto>(p => { calls++; return new ProductDto { Name = p.Name + "!" }; });

        var dto = config.CreateMapper().Map<BasketDto>(new Basket { Product = new Product { Name = "x" } });

        Assert.Equal("x!", dto.Product.Name);
        Assert.Equal(1, calls);
    }
}
