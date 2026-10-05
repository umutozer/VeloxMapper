using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VeloxMapper.Generators;
using Xunit;

namespace VeloxMapper.Tests;

/// <summary>
/// Source Generator (Layer 1) için derleme zamanı davranış testleri.
/// Roslyn'in CSharpCompilation + IIncrementalGenerator API'si kullanılarak
/// tam bir compile+generate döngüsü in-memory olarak çalıştırılır.
/// </summary>
public class SourceGeneratorTests
{
    // ─── Yardımcı: Generator'ı Çalıştır ─────────────────────────────────────

    /// <summary>
    /// Verilen C# kaynak kodunu Roslyn ile derleyip Generator'ı çalıştırır.
    /// Dönen (Compilation, GeneratorDriverRunResult) ikisi ile assertion yapılır.
    /// </summary>
    private static (Compilation Output, GeneratorDriverRunResult RunResult) RunGenerator(string source)
    {
        // Derleme için gerekli temel assembly referansları
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(VeloxMapper.Attributes.VeloxMapAttribute).Assembly.Location),
            // System.Runtime — nullable annotation'lar için gerekli
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };

        // NetStandard referansı, netstandard2.0 hedefleri için in-memory derlemede gereklidir
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
        }
        catch
        {
            // netstandard bulunamazsa sessizce geç
        }

        // Kaynak kodu giriş compilation'ı
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTest",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Generator'ı çalıştır
        var generator = new MapperGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();

        // Üretilen kaynaklarla zenginleştirilmiş compilation'ı al
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        // Derleme hatalarını yakalamak için kontrol ekliyoruz
        var compilationDiagnostics = outputCompilation.GetDiagnostics();
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Any())
        {
            var errorList = string.Join("\n", errors.Select(d => d.ToString()));
            throw new System.Exception("In-memory derleme hataları:\n" + errorList);
        }

        return (outputCompilation, runResult);
    }

    // ─── Test 1: Temel kod üretimi ───────────────────────────────────────────

    /// <summary>
    /// [assembly: VeloxMap] attribute verildiğinde GeneratedMappers sınıfının
    /// ve MapToXxx() metodunun üretildiğini doğrular.
    /// </summary>
    [Fact]
    public void Generator_AssemblyAttribute_UretimYapar()
    {
        // Arrange
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(TestKaynak), typeof(TestHedef))]

            public class TestKaynak { public int Id { get; set; } public string Ad { get; set; } = ""; }
            public class TestHedef  { public int Id { get; set; } public string Ad { get; set; } = ""; }
            """;

        // Act
        var (outputCompilation, runResult) = RunGenerator(source);

        // Assert — Generator en az bir dosya üretmiş olmalı
        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(runResult.GeneratedTrees);

        var generatedSource = runResult.GeneratedTrees[0].ToString();
        Assert.Contains("VeloxMapper.Generated", generatedSource);
        Assert.Contains("GeneratedMappers", generatedSource);
        Assert.Contains("MapToTestHedef", generatedSource);
        Assert.Contains("this TestKaynak source", generatedSource);

        // Üretilen kod da hatasız derlenmeli
        var outputDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(outputDiags);
    }

    // ─── Test 2: Birden fazla [VeloxMap] ────────────────────────────────────

    /// <summary>
    /// Aynı assembly'de birden fazla [VeloxMap] attribute kullanıldığında
    /// her çift için ayrı bir metot üretildiğini doğrular.
    /// (Eski kodun foreach+return hatası bu testi çökertirdi.)
    /// </summary>
    [Fact]
    public void Generator_BirdenFazlaAttribute_TumCiftleriUretir()
    {
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(Kaynak1), typeof(Hedef1))]
            [assembly: VeloxMap(typeof(Kaynak2), typeof(Hedef2))]

            public class Kaynak1 { public int Id { get; set; } }
            public class Hedef1  { public int Id { get; set; } }
            public class Kaynak2 { public string Ad { get; set; } = ""; }
            public class Hedef2  { public string Ad { get; set; } = ""; }
            """;

        var (outputCompilation, runResult) = RunGenerator(source);

        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(runResult.GeneratedTrees);

        var generatedSource = runResult.GeneratedTrees[0].ToString();

        // Her iki çift için metot üretilmiş olmalı
        Assert.Contains("MapToHedef1", generatedSource);
        Assert.Contains("MapToHedef2", generatedSource);
    }

    // ─── Test 3: Nullable int? → int ────────────────────────────────────────

    /// <summary>
    /// Kaynak int? iken hedef int olduğunda Generator'ın
    /// "?? default" ile null-safe atama ürettiğini doğrular.
    /// </summary>
    [Fact]
    public void Generator_NullableSource_NullCoalescingUretir()
    {
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(Kaynak), typeof(Hedef))]

            public class Kaynak { public int? Puan { get; set; } }
            public class Hedef  { public int  Puan { get; set; } }
            """;

        var (outputCompilation, runResult) = RunGenerator(source);

        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = runResult.GeneratedTrees[0].ToString();

        // null-coalescing operatörü üretilmiş olmalı
        Assert.Contains("?? default", generatedSource);
    }

    // ─── Test 4: Tip uyumsuzluğu → property atlanır ─────────────────────────

    /// <summary>
    /// Kaynak ve hedef tipler farklıysa (string ↔ int gibi) property eşleştirilmez;
    /// üretilen constructor boş kalır ya da o field eksik olur.
    /// </summary>
    [Fact]
    public void Generator_TipUyumsuzlugu_PropertyAtlanir()
    {
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(Kaynak), typeof(Hedef))]

            public class Kaynak { public string Deger { get; set; } = ""; }
            public class Hedef  { public int    Deger { get; set; } }
            """;

        var (_, runResult) = RunGenerator(source);

        // Üretim hatasız olmalı (tip uyumsuzluğu sessizce atlanır)
        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = runResult.GeneratedTrees[0].ToString();

        // Deger property'si üretilmemiş olmalı (tipler uyuşmuyor)
        Assert.DoesNotContain("Deger = source.Deger", generatedSource);
    }

    // ─── Test 5: [VeloxMap] yoksa dosya üretilmez ───────────────────────────

    /// <summary>
    /// [VeloxMap] attribute kullanılmadığında Generator herhangi bir dosya üretmemeli.
    /// </summary>
    [Fact]
    public void Generator_AttributeYok_DosyaUretilmez()
    {
        const string source = """
            public class Kaynak { public int Id { get; set; } }
            public class Hedef  { public int Id { get; set; } }
            """;

        var (_, runResult) = RunGenerator(source);

        Assert.Empty(runResult.GeneratedTrees);
    }

    // ─── Test 6: Sınıf düzeyinde [VeloxMap] ─────────────────────────────────

    /// <summary>
    /// Assembly değil sınıf düzeyinde [VeloxMap] attribute kullanılmasını da destekler.
    /// </summary>
    [Fact]
    public void Generator_SinifDuzeyi_Attribute_Desteklenir()
    {
        const string source = """
            using VeloxMapper.Attributes;

            [VeloxMap(typeof(SinifKaynak), typeof(SinifHedef))]
            public class SinifKaynak { public int Id { get; set; } }
            public class SinifHedef  { public int Id { get; set; } }
            """;

        var (_, runResult) = RunGenerator(source);

        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(runResult.GeneratedTrees);

        var generatedSource = runResult.GeneratedTrees[0].ToString();
        Assert.Contains("MapToSinifHedef", generatedSource);
    }

    // ─── Test 7: Farklı namespace'lerde aynı isimli Enum casting testi ──────

    /// <summary>
    /// Farklı ad alanlarındaki aynı isimli Enum türleri arasında mapping yapılırken
    /// generator'ın explicit cast kodu ürettiğini ve hatasız derlendiğini doğrular.
    /// </summary>
    [Fact]
    public void Generator_FarkliNamespace_AyniIsimliEnum_CastUretir()
    {
        const string source = """
            using VeloxMapper.Attributes;

            namespace KaynakNamespace
            {
                public enum SiparisDurumu { Alindi, Yolda, TeslimEdildi }
                public class KaynakModel { public SiparisDurumu Durum { get; set; } }
            }

            namespace HedefNamespace
            {
                public enum SiparisDurumu { Alindi, Yolda, TeslimEdildi }
                public class HedefModel { public SiparisDurumu Durum { get; set; } }
            }

            namespace TestApp
            {
                [VeloxMap(typeof(KaynakNamespace.KaynakModel), typeof(HedefNamespace.HedefModel))]
                public class MapConfig {}
            }
            """;

        var (outputCompilation, runResult) = RunGenerator(source);

        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(runResult.GeneratedTrees);

        var generatedSource = runResult.GeneratedTrees[0].ToString();
        
        // Explicit cast üretilmiş olmalı
        Assert.Contains("Durum = (global::HedefNamespace.SiparisDurumu)(source.Durum)", generatedSource);

        // Üretilen kodun in-memory derlemesi hatasız geçmiş olmalı
        var outputDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(outputDiags);
    }

    // ─── Test 8: Miras Alınan (Base Class) Property'leri ───────────────────

    /// <summary>
    /// Base class'lardan miras alınan public property'lerin de mapping sürecine dahil
    /// edildiğini ve doğru eşleştiğini doğrular.
    /// </summary>
    [Fact]
    public void Generator_BaseClassProperties_DahilEdilir_Ve_Eslesir()
    {
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(DerivedKaynak), typeof(DerivedHedef))]

            public class BaseKaynak { public int Id { get; set; } }
            public class DerivedKaynak : BaseKaynak { public string Ad { get; set; } = ""; }

            public class BaseHedef { public int Id { get; set; } }
            public class DerivedHedef : BaseHedef { public string Ad { get; set; } = ""; }
            """;

        var (outputCompilation, runResult) = RunGenerator(source);

        Assert.Empty(runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(runResult.GeneratedTrees);

        var generatedSource = runResult.GeneratedTrees[0].ToString();

        // Miras alınan 'Id' ve doğrudan sınıftaki 'Ad' property'leri üretilmiş olmalı
        Assert.Contains("Id = source.Id", generatedSource);
        Assert.Contains("Ad = source.Ad", generatedSource);
    }

    [Fact]
    public void Generator_RecordVeGetOnlyHedef_DerlenebilirKodUretir()
    {
        const string source = """
            using VeloxMapper.Attributes;
            [assembly: VeloxMap(typeof(Kaynak), typeof(KayitHedef))]
            [assembly: VeloxMap(typeof(Kaynak), typeof(SaltOkunurHedef))]
            [assembly: VeloxMap(typeof(Kaynak), typeof(KurucusuzHedef))]

            public class Kaynak { public int Id { get; set; } public string Ad { get; set; } = ""; public string Not { get; set; } = ""; }
            public record KayitHedef(int Id, string Ad) { public string Not { get; init; } = ""; }
            public class SaltOkunurHedef { public int Id { get; } public string Ad { get; set; } = ""; }
            public class KurucusuzHedef { public KurucusuzHedef(System.Guid anahtar) { } }
            """;

        var (_, runResult) = RunGenerator(source); // üretilen kod hatasız derlenmeli (RunGenerator hata varsa fırlatır)

        var generated = runResult.GeneratedTrees.Single().ToString();
        Assert.Contains("new KayitHedef(source.Id, source.Ad)", generated);
        Assert.Contains("Not = source.Not", generated);
        Assert.DoesNotContain("Id = source.Id", generated.Substring(generated.IndexOf("MapToSaltOkunurHedef")));
        Assert.Contains(runResult.Diagnostics, d => d.Id == "VM003" && d.GetMessage().Contains("KurucusuzHedef"));
    }
}
