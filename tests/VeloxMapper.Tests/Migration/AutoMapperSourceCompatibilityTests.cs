// Bu dosyadaki kod, tipik bir AutoMapper projesinden alınmış gibi yazılmıştır.
// Tek fark: "using AutoMapper;" → "using VeloxMapper;" ve "using AutoMapper.QueryableExtensions;" → "using VeloxMapper.QueryableExtensions;".
// Amaç: AutoMapper kullanıcılarının kodunun değiştirilmeden derlendiğini ve aynı sonucu ürettiğini kanıtlamak.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

namespace VeloxMapper.Tests.Migration;

#region Domain

public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public Address? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public enum OrderStatus { Pending = 0, Shipped = 1, Delivered = 2 }

public enum OrderStatusDto { Delivered = 0, Pending = 1, Shipped = 2 }

public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
    public OrderStatus Status { get; set; }
    public decimal Discount { get; set; }
    public string? Note { get; set; }
    public decimal GetTotal() => Lines.Sum(l => l.Price * l.Quantity) - Discount;
}

public class OrderLine
{
    public string Product { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

#endregion

#region DTOs

public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? AddressCity { get; set; }
}

public class OrderLineDto
{
    public string Product { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public CustomerDto Buyer { get; set; } = new();
    public List<OrderLineDto> Items { get; set; } = new();
    public OrderStatusDto Status { get; set; }
    public decimal Total { get; set; }
    public string Note { get; set; } = "";
    public string? IgnoredField { get; set; }
}

#endregion

#region Profiles

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.FirstName + " " + s.LastName));
    }
}

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderLine, OrderLineDto>().ReverseMap();

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.FirstName))
            .ForMember(d => d.Buyer, opt => opt.MapFrom(s => s.Customer))   // farklı tür: Customer → CustomerDto
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Lines))      // List<OrderLine> → List<OrderLineDto>
            .ForMember(d => d.Note, opt => opt.NullSubstitute("(yok)"))
            .ForMember(d => d.IgnoredField, opt => opt.Ignore());
    }
}

#endregion

public class AutoMapperSourceCompatibilityTests
{
    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<CustomerProfile>();
            cfg.AddProfile<OrderProfile>();
        });
        configuration.AssertConfigurationIsValid();
        return configuration.CreateMapper();
    }

    private static Order SampleOrder() => new()
    {
        Id = 7,
        Status = OrderStatus.Shipped,
        Discount = 5m,
        Customer = new Customer { Id = 3, FirstName = "Ada", LastName = "Lovelace", Address = new Address { City = "Londra" } },
        Lines = { new OrderLine { Product = "Kalem", Price = 10m, Quantity = 2 }, new OrderLine { Product = "Defter", Price = 25m, Quantity = 1 } }
    };

    [Fact]
    public void Profile_CreateMap_ForMember_MapFrom_derlenir_ve_esler()
    {
        var mapper = CreateMapper();
        var dto = mapper.Map<OrderDto>(SampleOrder());

        Assert.Equal(7, dto.Id);
        Assert.Equal("Ada", dto.CustomerName);
        Assert.Equal("Ada Lovelace", dto.Buyer.FullName);
        Assert.Equal("Londra", dto.Buyer.AddressCity);           // flattening
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal("Kalem", dto.Items[0].Product);
        Assert.Equal(40m, dto.Total);                             // GetTotal() → Total
        Assert.Equal("(yok)", dto.Note);                          // NullSubstitute
        Assert.Null(dto.IgnoredField);
    }

    [Fact]
    public void Enum_esleme_AutoMapper_gibi_once_isme_gore_yapilir()
    {
        var mapper = CreateMapper();
        var dto = mapper.Map<Order, OrderDto>(SampleOrder());
        Assert.Equal(OrderStatusDto.Shipped, dto.Status); // değerler farklı, isimler aynı
    }

    [Fact]
    public void Generic_iki_parametreli_Map_ve_koleksiyon_esleme()
    {
        var mapper = CreateMapper();
        var orders = new List<Order> { SampleOrder(), SampleOrder() };

        var dtos = mapper.Map<List<Order>, List<OrderDto>>(orders);
        var array = mapper.Map<OrderDto[]>(orders);
        var enumerable = mapper.Map<IEnumerable<OrderDto>>(orders);

        Assert.Equal(2, dtos.Count);
        Assert.Equal(2, array.Length);
        Assert.Equal(2, enumerable.Count());
    }

    [Fact]
    public void Map_null_kaynak_null_veya_bos_koleksiyon_dondurur()
    {
        var mapper = CreateMapper();
        Assert.Null(mapper.Map<OrderDto>(null));
        Assert.Null(mapper.Map<Order, OrderDto>(null!));
        Assert.Empty(mapper.Map<List<Order>, List<OrderDto>>(null!));
    }

    [Fact]
    public void Map_mevcut_nesneye_hedefi_dondurur_ve_ic_nesneleri_korur()
    {
        var mapper = CreateMapper();
        var existing = new OrderDto();
        var buyer = existing.Buyer;
        var items = existing.Items;

        var result = mapper.Map(SampleOrder(), existing);

        Assert.Same(existing, result);
        Assert.Same(buyer, result.Buyer);     // iç nesne yerinde güncellendi (EF Core takip edilen varlıklar için önemli)
        Assert.Same(items, result.Items);     // koleksiyon örneği korundu
        Assert.Equal("Ada Lovelace", result.Buyer.FullName);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public void Map_calisma_zamani_turleriyle()
    {
        var mapper = CreateMapper();
        object source = SampleOrder();

        var dto = (OrderDto)mapper.Map(source, typeof(Order), typeof(OrderDto))!;
        var existing = new OrderDto();
        var patched = mapper.Map(source, existing, typeof(Order), typeof(OrderDto));

        Assert.Equal(7, dto.Id);
        Assert.Same(existing, patched);
        Assert.Equal(7, existing.Id);
    }

    [Fact]
    public void ReverseMap_tersi_dondurur_ve_zincirlenebilir()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Customer, CustomerDto>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName))
                .ReverseMap()
                .ForMember(s => s.LastName, o => o.MapFrom(d => "ters"))   // ters yöne özel kural
                .ForMember(s => s.CreatedAt, o => o.Ignore());
        });
        var mapper = config.CreateMapper();

        var customer = mapper.Map<Customer>(new CustomerDto { Id = 1, FullName = "Grace", AddressCity = "NY" });

        Assert.Equal("Grace", customer.FirstName);          // MapFrom(s => s.FirstName) tersine çevrildi
        Assert.Equal("ters", customer.LastName);
        Assert.Equal("NY", customer.Address!.City);         // unflattening
    }

    [Fact]
    public void Map_islem_secenekleri_Items_ve_BeforeAfterMap()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<Customer, CustomerDto>()
                .ForMember(d => d.FullName, o => o.MapFrom((src, dest, member, ctx) => (string)ctx.Items["prefix"] + src.FirstName)));
        var mapper = config.CreateMapper();
        var after = false;

        var dto = mapper.Map<Customer, CustomerDto>(new Customer { FirstName = "Linus" }, opt =>
        {
            opt.Items["prefix"] = "Sn. ";
            opt.AfterMap((src, dest) => after = true);
        });
        var dto2 = mapper.Map<CustomerDto>(new Customer { FirstName = "Ken" }, opt => opt.Items["prefix"] = "Dr. ");

        Assert.Equal("Sn. Linus", dto.FullName);
        Assert.Equal("Dr. Ken", dto2.FullName);
        Assert.True(after);
    }

    [Fact]
    public void ProjectTo_IConfigurationProvider_ile_derlenir_ve_string_birlestirme_desteklenir()
    {
        var mapper = CreateMapper();
        var customers = new List<Customer>
        {
            new() { Id = 1, FirstName = "Alan", LastName = "Turing", Address = new Address { City = "Wilmslow" } },
            new() { Id = 2, FirstName = "Edsger", LastName = "Dijkstra" }
        }.AsQueryable();

        var result = customers.ProjectTo<CustomerDto>(mapper.ConfigurationProvider).ToList();
        var viaMapper = mapper.ProjectTo<CustomerDto>(customers).ToList();

        Assert.Equal("Alan Turing", result[0].FullName);
        Assert.Equal("Wilmslow", result[0].AddressCity);
        Assert.Null(result[1].AddressCity); // null propagation
        Assert.Equal(2, viaMapper.Count);
    }

    [Fact]
    public void DI_AddAutoMapper_takma_adi_ile_IMapper_cozulur()
    {
        var services = new ServiceCollection();
        services.AddAutoMapper(typeof(CustomerProfile));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        Assert.Equal("Ada Lovelace", mapper.Map<CustomerDto>(new Customer { FirstName = "Ada", LastName = "Lovelace" }).FullName);
    }

    [Fact]
    public void Statik_MapperConfiguration_kullanim_bicimi()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(CustomerProfile).Assembly));
        IMapper mapper = new Mapper(configuration);

        Assert.Equal("A B", mapper.Map<CustomerDto>(new Customer { FirstName = "A", LastName = "B" }).FullName);
    }
}
