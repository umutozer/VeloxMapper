using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VeloxMapper.Generators.Analyzer;

/// <summary>
/// [VeloxMap] hedef türlerinde constructor belirsizliğini derleme zamanında tespit eden analizer.
/// Kurallar:
///   - Birden fazla [VeloxConstructor] → VM001 hatası
///   - Birden fazla aynı parametre sayılı constructor ve [VeloxConstructor] yok → VM002 uyarısı
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>VM001: Birden fazla [VeloxConstructor] özniteliği tespit edildi.</summary>
    public static readonly DiagnosticDescriptor MultipleVeloxConstructorRule = new(
        id: "VM001",
        title: "Birden fazla VeloxConstructor",
        messageFormat: "'{0}' türünde birden fazla [VeloxConstructor] özniteliği bulundu. Yalnızca bir kurucuya uygulanmalıdır.",
        category: "VeloxMapper.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>VM002: Constructor belirsizliği — [VeloxConstructor] kullanmanız önerilir.</summary>
    public static readonly DiagnosticDescriptor AmbiguousConstructorRule = new(
        id: "VM002",
        title: "Belirsiz constructor seçimi",
        messageFormat: "'{0}' türünde aynı parametre sayısına sahip birden fazla kurucu bulundu. [VeloxConstructor] özniteliği kullanarak çözün.",
        category: "VeloxMapper.Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MultipleVeloxConstructorRule, AmbiguousConstructorRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Named type sembollerini analiz et
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol) return;
        if (typeSymbol.TypeKind != TypeKind.Class) return;

        // Sadece VeloxMap eşleştirmesine dahil olan türleri analiz et
        if (!IsVeloxMapParticipant(typeSymbol)) return;

        var ctors = typeSymbol.Constructors
            .Where(c => !c.IsImplicitlyDeclared && c.DeclaredAccessibility == Accessibility.Public)
            .ToArray();

        if (ctors.Length <= 1) return;

        // [VeloxConstructor] kontrolü
        var veloxCtors = ctors.Where(c =>
            c.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "VeloxMapper.Attributes.VeloxConstructorAttribute"))
            .ToArray();

        if (veloxCtors.Length > 1)
        {
            // VM001: Birden fazla [VeloxConstructor]
            foreach (var ctor in veloxCtors)
            {
                var diagnostic = Diagnostic.Create(
                    MultipleVeloxConstructorRule,
                    ctor.Locations.FirstOrDefault(),
                    typeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
        else if (veloxCtors.Length == 0)
        {
            // [VeloxConstructor] yoksa → aynı parametre sayılı constructorlar kontrol et
            var maxParams = ctors.Max(c => c.Parameters.Length);
            var ambiguous = ctors.Where(c => c.Parameters.Length == maxParams).ToArray();

            if (ambiguous.Length > 1)
            {
                // VM002: Belirsizlik uyarısı
                foreach (var ctor in ambiguous)
                {
                    var diagnostic = Diagnostic.Create(
                        AmbiguousConstructorRule,
                        ctor.Locations.FirstOrDefault(),
                        typeSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    /// <summary>
    /// Sınıfın herhangi bir [VeloxMap] özniteliği ile işaretlenip işaretlenmediğini
    /// veya assembly düzeyindeki [VeloxMap] özniteliklerinde kaynak/hedef olarak geçip geçmediğini kontrol eder.
    /// </summary>
    private static bool IsVeloxMapParticipant(INamedTypeSymbol typeSymbol)
    {
        // 1. Sınıfın kendi üzerindeki [VeloxMap] kontrolü
        if (typeSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "VeloxMapper.Attributes.VeloxMapAttribute"))
        {
            return true;
        }

        // 2. Assembly düzeyindeki [VeloxMap] kontrolü
        var assemblyAttributes = typeSymbol.ContainingAssembly.GetAttributes();
        foreach (var attr in assemblyAttributes)
        {
            if (attr.AttributeClass?.ToDisplayString() == "VeloxMapper.Attributes.VeloxMapAttribute")
            {
                if (attr.ConstructorArguments.Length == 2)
                {
                    var srcType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                    var dstType = attr.ConstructorArguments[1].Value as INamedTypeSymbol;

                    if (SymbolEqualityComparer.Default.Equals(typeSymbol, srcType) ||
                        SymbolEqualityComparer.Default.Equals(typeSymbol, dstType))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
