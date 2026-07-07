using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using VeloxMapper.Configuration;

namespace VeloxMapper.Tests;

/// <summary>
/// AutoMapper API uyumluluğu — Koleksiyon mapping davranışlarını doğrulayan testler.
/// AutoMapper'dan geçiş yapan geliştiricilerin alışık olduğu kullanım kalıplarını test eder.
/// </summary>
public class CollectionApiCompatibilityTests
{
    #region Test Modelleri

    public class CollSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CollDest
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    /// <summary>
    /// AutoMapper tarzı kullanım: mapper.Map&lt;List&lt;Dest&gt;&gt;(sourceList)
    /// Koleksiyon tipi açıkça belirtilir (AutoMapper'ın standart davranışı).
    /// </summary>
    [Fact]
    public void Map_ListSource_ToListDest_AutoMapperStyle()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        var sourceList = new List<CollSource>
        {
            new() { Id = 1, Name = "Ali" },
            new() { Id = 2, Name = "Veli" },
            new() { Id = 3, Name = "Deniz" }
        };

        // Act — AutoMapper tarzı: mapper.Map<List<CollDest>>(sourceList)
        var result = mapper.Map<List<CollDest>>((object)sourceList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Ali", result[0].Name);
        Assert.Equal("Veli", result[1].Name);
        Assert.Equal("Deniz", result[2].Name);
    }

    /// <summary>
    /// AutoMapper strongly-typed tarzı: mapper.Map&lt;List&lt;Source&gt;, List&lt;Dest&gt;&gt;(sourceList)
    /// Hem kaynak hem hedef koleksiyon tipi açıkça belirtilir.
    /// </summary>
    [Fact]
    public void Map_ListSource_ToListDest_StronglyTyped_AutoMapperStyle()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        var sourceList = new List<CollSource>
        {
            new() { Id = 10, Name = "Kaynak1" },
            new() { Id = 20, Name = "Kaynak2" }
        };

        // Act — AutoMapper tarzı: mapper.Map<List<Source>, List<Dest>>(sourceList)
        var result = mapper.Map<List<CollSource>, List<CollDest>>(sourceList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[0].Id);
        Assert.Equal("Kaynak2", result[1].Name);
    }

    /// <summary>
    /// AutoMapper tarzı: mapper.Map&lt;IEnumerable&lt;Dest&gt;&gt;(sourceList) — Interface dönüş tipi.
    /// </summary>
    [Fact]
    public void Map_ListSource_ToIEnumerableDest_AutoMapperStyle()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        var sourceList = new List<CollSource>
        {
            new() { Id = 1, Name = "Test" }
        };

        // Act — AutoMapper tarzı: mapper.Map<IEnumerable<Dest>>(sourceList)
        var result = mapper.Map<IEnumerable<CollDest>>((object)sourceList);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test", result.First().Name);
    }

    /// <summary>
    /// AutoMapper tarzı: mapper.Map&lt;Dest[]&gt;(sourceList) — Array dönüş tipi.
    /// </summary>
    [Fact]
    public void Map_ListSource_ToArrayDest_AutoMapperStyle()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        var sourceList = new List<CollSource>
        {
            new() { Id = 5, Name = "Array" },
            new() { Id = 6, Name = "Test" }
        };

        // Act — AutoMapper tarzı: mapper.Map<Dest[]>(sourceList)
        var result = mapper.Map<CollDest[]>((object)sourceList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("Array", result[0].Name);
    }

    /// <summary>
    /// Null koleksiyon verisi geldiğinde boş koleksiyon dönmelidir (AllowNullCollections = false).
    /// AutoMapper'ın varsayılan davranışıdır.
    /// </summary>
    [Fact]
    public void Map_NullList_ShouldReturnEmptyList_ByDefault()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        List<CollSource>? sourceList = null;

        // Act
        var result = mapper.Map<List<CollDest>>((object)sourceList!);

        // Assert — null yerine boş liste dönmeli
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Boş koleksiyon geldiğinde boş koleksiyon dönmelidir.
    /// </summary>
    [Fact]
    public void Map_EmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollSource, CollDest>();
        });
        var mapper = new Mapper(config);

        var sourceList = new List<CollSource>();

        // Act
        var result = mapper.Map<List<CollDest>>((object)sourceList);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
