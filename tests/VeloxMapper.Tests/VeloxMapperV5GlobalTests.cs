using System;
using System.Collections.Generic;
using Xunit;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;
using VeloxMapper.Diagnostics;

namespace VeloxMapper.Tests;

#region Test Modelleri

/// <summary>
/// Global ignore özelliğini test etmek için kaynak sınıfı.
/// </summary>
public class IgnoreSrc
{
    public string Id { get; set; } = "123";
    public string Token { get; set; } = "GizliVeri";
}

/// <summary>
/// Global ignore özelliğini test etmek için hedef sınıfı.
/// </summary>
public class IgnoreDst
{
    public string Id { get; set; } = "";
    public string Token { get; set; } = "Degistirilmedi";
}

/// <summary>
/// Prefix/postfix özelliklerini test etmek için kaynak sınıfı.
/// </summary>
public class PrefixPostfixSrc
{
    public string m_Code { get; set; } = "CODE1";
    public string GetName { get; set; } = "Ad1";
    public int ValueProp { get; set; } = 42;
}

/// <summary>
/// Prefix/postfix özelliklerini test etmek için hedef sınıfı.
/// </summary>
public class PrefixPostfixDst
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>
/// Hedef prefix özelliğini test etmek için kaynak sınıfı.
/// </summary>
public class DestPrefixSrc
{
    public string Key { get; set; } = "Anahtar";
}

/// <summary>
/// Hedef prefix özelliğini test etmek için hedef sınıfı.
/// </summary>
public class DestPrefixDst
{
    public string m_Key { get; set; } = "";
}

/// <summary>
/// Koleksiyon mapping davranışlarını test etmek için kaynak sınıfı.
/// </summary>
public class CollectionSrc
{
    public List<string>? Items { get; set; }
    public string[]? Tags { get; set; }
    public HashSet<int>? Numbers { get; set; }
}

/// <summary>
/// Koleksiyon mapping davranışlarını test etmek için hedef sınıfı.
/// </summary>
public class CollectionDst
{
    public List<string>? Items { get; set; }
    public string[]? Tags { get; set; }
    public HashSet<int>? Numbers { get; set; }
}

/// <summary>
/// PascalCase kaynak sınıfı.
/// </summary>
public class PascalSrc
{
    public string FirstName { get; set; } = "Ali";
    public string LastName { get; set; } = "Veli";
}

/// <summary>
/// camelCase hedef sınıfı.
/// </summary>
public class CamelDst
{
    public string firstName { get; set; } = "";
    public string lastName { get; set; } = "";
}

#endregion

#region Yardımcı Sınıflar

/// <summary>
/// Test amaçlı mock Naming Convention sınıfı. (PascalCase -> camelCase dönüşümü sağlar)
/// </summary>
public class CamelCaseNamingConvention : ICustomNamingConvention
{
    public string Normalize(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return propertyName;
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}

/// <summary>
/// Test amaçlı mock Diagnostics Sink sınıfı.
/// </summary>
public class FakeDiagnosticsSink : IVeloxDiagnosticsSink
{
    public List<string> LoggedMessages { get; } = new();

    public void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null)
    {
        LoggedMessages.Add($"[{severity}] {message}");
    }
}

#endregion

/// <summary>
/// VeloxMapper Faz 7 global yapılandırma özelliklerinin birim testleri.
/// </summary>
public class VeloxMapperV5GlobalTests
{
    [Fact]
    public void AddGlobalIgnore_BelirtilenPropertyler_EslemedenHaricTutulmali()
    {
        // 1. Yapılandırma - Token property'si global olarak yoksayılacak
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IgnoreSrc, IgnoreDst>();
            cfg.AddGlobalIgnore("Token");
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new IgnoreSrc();
        var dest = mapper.Map<IgnoreDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal("123", dest.Id); // Id kopyalanmalı
        Assert.Equal("Degistirilmedi", dest.Token); // Token yoksayılmalı ve hedefin orijinal değeri korunmalı
    }

    [Fact]
    public void RecognizePrefixes_Ve_RecognizePostfixes_Propertyleri_Eslemeli()
    {
        // 1. Yapılandırma - "m_" ve "Get" ön ekleri ile "Prop" son eki tanınacak
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PrefixPostfixSrc, PrefixPostfixDst>();
            cfg.RecognizePrefixes("m_", "Get");
            cfg.RecognizePostfixes("Prop");
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new PrefixPostfixSrc { m_Code = "XYZ", GetName = "Ahmet", ValueProp = 99 };
        var dest = mapper.Map<PrefixPostfixDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal("XYZ", dest.Code); // m_Code -> Code
        Assert.Equal("Ahmet", dest.Name); // GetName -> Name
        Assert.Equal(99, dest.Value); // ValueProp -> Value
    }

    [Fact]
    public void RecognizeDestinationPrefixes_HedefOnEkliPropertyleri_Eslemeli()
    {
        // 1. Yapılandırma - Hedef için "m_" ön eki tanınacak
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<DestPrefixSrc, DestPrefixDst>();
            cfg.RecognizeDestinationPrefixes("m_");
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new DestPrefixSrc { Key = "Gizli" };
        var dest = mapper.Map<DestPrefixDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal("Gizli", dest.m_Key); // Key -> m_Key
    }

    [Fact]
    public void AllowNullCollections_Kapaliysa_NullKoleksiyonu_BosKoleksiyonaDondurmeli()
    {
        // 1. Yapılandırma - AllowNullCollections = false (Varsayılan)
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollectionSrc, CollectionDst>();
            cfg.AllowNullCollections = false;
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new CollectionSrc { Items = null, Tags = null, Numbers = null };
        var dest = mapper.Map<CollectionDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.NotNull(dest.Items);
        Assert.Empty(dest.Items); // null Liste -> boş Liste

        Assert.NotNull(dest.Tags);
        Assert.Empty(dest.Tags); // null Array -> boş Array

        Assert.NotNull(dest.Numbers);
        Assert.Empty(dest.Numbers); // null HashSet -> boş HashSet
    }

    [Fact]
    public void AllowNullCollections_Aciksa_NullKoleksiyonu_NullBrakmali()
    {
        // 1. Yapılandırma - AllowNullCollections = true
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CollectionSrc, CollectionDst>();
            cfg.AllowNullCollections = true;
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new CollectionSrc { Items = null, Tags = null, Numbers = null };
        var dest = mapper.Map<CollectionDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Null(dest.Items); // null olarak kalmalı
        Assert.Null(dest.Tags);  // null olarak kalmalı
        Assert.Null(dest.Numbers); // null olarak kalmalı
    }

    [Fact]
    public void ForAllMaps_TumEslemelereGlobalKuralUygulamali()
    {
        // 1. Yapılandırma - Tüm eşlemelere AfterMap eylemi ekle
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IgnoreSrc, IgnoreDst>();
            cfg.ForAllMaps(reg =>
            {
                // Tüm eşleşmelerde çalışacak bir AfterMap ekliyoruz.
                // Not: Generic bir işlem simüle etmek için BeforeMap/AfterMap listesine ekleme yapıyoruz.
                if (reg.AfterMapActions is List<object> afterActions)
                {
                    afterActions.Add(new Action<IgnoreSrc, IgnoreDst>((src, dest) =>
                    {
                        dest.Id = "TUM_HARITALARDA_CALISTI";
                    }));
                }
            });
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new IgnoreSrc { Id = "OrijinalId" };
        var dest = mapper.Map<IgnoreDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal("TUM_HARITALARDA_CALISTI", dest.Id); // ForAllMaps ile eklenen delege çalışmış olmalı
    }

    [Fact]
    public void ICustomNamingConvention_FarkliFormatlardakiIsimleriEslemeli()
    {
        // 1. Yapılandırma - Naming Convention kaydı
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PascalSrc, CamelDst>();
            cfg.NamingConvention = new CamelCaseNamingConvention();
        });

        var mapper = new Mapper(config);

        // 2. Eşleme
        var source = new PascalSrc { FirstName = "Umut", LastName = "Ozen" };
        var dest = mapper.Map<CamelDst>(source);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal("Umut", dest.firstName); // FirstName -> firstName
        Assert.Equal("Ozen", dest.lastName);  // LastName -> lastName
    }

    [Fact]
    public void IVeloxDiagnosticsSink_DerlemeEsnasindaLoglamaYapmali()
    {
        var fakeSink = new FakeDiagnosticsSink();

        // 1. Yapılandırma - Diagnostics Sink kaydı
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IgnoreSrc, IgnoreDst>();
            cfg.DiagnosticsSink = fakeSink;
        });

        // 2. Derleme (Mapper instance yaratılırken mapping metodu derlenir ve log basılır)
        var mapper = new Mapper(config);
        
        var source = new IgnoreSrc();
        var dest = mapper.Map<IgnoreDst>(source);

        // 3. Doğrulama - Log basıldığını kontrol et
        Assert.NotEmpty(fakeSink.LoggedMessages);
        
        // Loglardan en az birinin "IgnoreSrc" veya "IgnoreDst" kelimelerini içerdiğini doğrula
        Assert.Contains(fakeSink.LoggedMessages, log => log.Contains("IgnoreSrc") || log.Contains("IgnoreDst"));
    }
}
