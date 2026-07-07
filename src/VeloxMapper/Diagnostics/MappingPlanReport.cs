using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VeloxMapper.Caching;

namespace VeloxMapper.Diagnostics;

/// <summary>
/// VeloxMapper sürüm ve plan şema sabitleri.
/// CI snapshot testlerinde plan şeması değişirse hash kırılır — bu beklenen davranıştır.
/// </summary>
public static class VeloxVersion
{
    /// <summary>
    /// VeloxMapper kütüphane sürümü (SemVer 2.0).
    /// Major versiyon değişikliği = breaking change.
    /// NuGet paket sürümü ile senkron tutulmalıdır.
    /// </summary>
    public const string Current = "5.2.1";

    /// <summary>
    /// Plan raporu şema sürümü.
    /// Plan yapısında (alan ekleme/silme/yeniden adlandırma) değişiklik olursa artırılır.
    /// CI snapshot testlerinin kırılmaması için bu sürüm kontrol edilmelidir.
    /// </summary>
    public const string PlanSchemaVersion = "1.0";
}

/// <summary>
/// Bir özellik seviyesindeki eşleştirme bilgisini tanımlar (kaynak → hedef).
/// </summary>
public sealed class MappedPropertyDesc
{
    /// <summary>Kaynak özellik adı.</summary>
    public string SourceProperty { get; set; } = "";

    /// <summary>Hedef özellik adı.</summary>
    public string TargetProperty { get; set; } = "";

    /// <summary>Eşleştirme yürütme tipi: "Assigned", "Complex", "Ignored" vb.</summary>
    public string ExecutionType { get; set; } = "";
}

/// <summary>
/// Bir kaynak → hedef eşleştirme planını tanımlar.
/// <c>VeloxVersion</c> ve <c>PlanSchemaVersion</c> alanları CI/CD snapshot doğrulaması için zorunludur.
/// </summary>
public sealed class MappingPlanDef
{
    /// <summary>
    /// VeloxMapper kütüphane sürümü. Otomatik olarak <see cref="VeloxVersion.Current"/> değerini alır.
    /// </summary>
    public string VeloxMapperVersion { get; set; } = VeloxVersion.Current;

    /// <summary>
    /// Plan şema sürümü. Plan yapısı değiştiğinde artırılır.
    /// CI snapshot testlerinde bu alan kontrol edilerek uyumsuzluk tespit edilir.
    /// </summary>
    public string PlanSchemaVersion { get; set; } = VeloxVersion.PlanSchemaVersion;

    /// <summary>Profil adı (opsiyonel).</summary>
    public string ProfileName { get; set; } = "";

    /// <summary>Kaynak türün tam adı.</summary>
    public string SourceType { get; set; } = "";

    /// <summary>Hedef türün tam adı.</summary>
    public string DestinationType { get; set; } = "";

    /// <summary>Eşleştirme modu: Map, Patch veya ProjectTo.</summary>
    public MappingMode Mode { get; set; }

    /// <summary>Eşleştirilen özellikler listesi.</summary>
    public MappedPropertyDesc[] Properties { get; set; } = [];
}

/// <summary>
/// Eşleştirme planı raporlarını (Text/JSON) ve deterministik SHA-256 hash üretir.
/// Hash yalnızca eşleştirme mantığı değiştiğinde değişir — CI/CD doğrulaması için kullanılır.
/// </summary>
public static class MappingPlanReport
{
    /// <summary>
    /// Eşleştirme planını JSON formatında döndürür (makine tüketimi için).
    /// Çıktıda <c>veloxMapperVersion</c> ve <c>planSchemaVersion</c> alanları bulunur.
    /// </summary>
    public static string GenerateJsonReport(MappingPlanDef plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(plan, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Eşleştirme planını okunabilir metin formatında döndürür (severity seviyeleri ile).
    /// Başlık satırında veloxVersion ve planSchemaVersion bilgileri yer alır.
    /// </summary>
    public static string GenerateTextReport(MappingPlanDef plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sb = new StringBuilder();

        // Versiyon bilgisi — CI uyumluluğu için zorunlu
        sb.AppendLine($"[META] VeloxMapper v{plan.VeloxMapperVersion} | PlanSchema v{plan.PlanSchemaVersion}");
        sb.AppendLine($"[INFO] Mapping Plan: {plan.SourceType} -> {plan.DestinationType} [{plan.Mode}]");

        if (!string.IsNullOrEmpty(plan.ProfileName))
        {
            sb.AppendLine($"[INFO] Profile: {plan.ProfileName}");
        }

        foreach (var prop in plan.Properties)
        {
            sb.AppendLine($"  [DETAIL] {prop.SourceProperty} => {prop.TargetProperty} ({prop.ExecutionType})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Eşleştirme planından deterministik, kararlı bir SHA-256 hash üretir.
    /// <para>
    /// Hash bileşenleri (sırasıyla):
    /// <list type="number">
    ///   <item>planSchemaVersion — şema değişince hash kırılır (beklenen davranış)</item>
    ///   <item>SourceType, DestinationType, Mode</item>
    ///   <item>Sıralı property eşleştirmeleri</item>
    /// </list>
    /// </para>
    /// <c>veloxVersion</c> hash'e dahil DEĞİLDİR — minor/patch güncellemelerinde hash kırılmamalı.
    /// </summary>
    public static string GenerateHash(MappingPlanDef plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Deterministik sıralama — özellik sırası hash'i etkilememeli
        var sortedProps = plan.Properties
            .OrderBy(p => p.TargetProperty, StringComparer.Ordinal)
            .ToArray();

        var sb = new StringBuilder();

        // PlanSchemaVersion hash'e dahil — şema değişirse CI snapshot kırılmalı
        sb.Append(plan.PlanSchemaVersion);
        sb.Append('|');
        sb.Append(plan.SourceType);
        sb.Append('|');
        sb.Append(plan.DestinationType);
        sb.Append('|');
        sb.Append(plan.Mode.ToString());

        foreach (var p in sortedProps)
        {
            sb.Append('|');
            sb.Append(p.TargetProperty);
            sb.Append(':');
            sb.Append(p.SourceProperty);
            sb.Append(':');
            sb.Append(p.ExecutionType);
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(bytes);

        // .NET 8.0 ve üzeri uyumluluk için ToHexString ve ToLowerInvariant kullanıldı
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
