using System;

namespace VeloxMapper;

/// <summary>
/// Hedef tür üzerinde, verilen kaynak türden eşleştirme tanımlar. Assembly taraması (<c>AddMaps</c>,
/// <c>AddVeloxMapper(typeof(Program))</c>) sırasında otomatik olarak bir <c>CreateMap</c> kaydı üretilir.
/// AutoMapper'ın <c>[AutoMap(typeof(Source))]</c> özniteliği ile uyumludur. Üye düzeyinde
/// <c>VeloxMapper.Configuration.Annotations</c> öznitelikleri (<c>[Ignore]</c>, <c>[SourceMember]</c>...) kullanılabilir.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class AutoMapAttribute : Attribute
{
    /// <summary>
    /// Verilen kaynak türden bu hedef türe eşleştirme tanımlar.
    /// </summary>
    /// <param name="sourceType">Kaynak tür.</param>
    public AutoMapAttribute(Type sourceType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
    }

    /// <summary>Kaynak tür.</summary>
    public Type SourceType { get; }

    /// <summary>True ise ters yönde eşleştirme de oluşturulur (<c>ReverseMap()</c>).</summary>
    public bool ReverseMap { get; set; }

    /// <summary>True ise hedef nesne DI konteynerinden çözülür (<c>ConstructUsingServiceLocator()</c>).</summary>
    public bool ConstructUsingServiceLocator { get; set; }

    /// <summary>Rekürsif eşleştirme derinliği sınırı (0 = sınırsız).</summary>
    public int MaxDepth { get; set; }

    /// <summary>True ise döngüsel referanslarda referans koruması etkinleşir.</summary>
    public bool PreserveReferences { get; set; }

    /// <summary>True ise kurucu doğrulaması kapatılır.</summary>
    public bool DisableCtorValidation { get; set; }

    /// <summary>True ise türetilmiş tüm eşleştirmeler polimorfik olarak dahil edilir.</summary>
    public bool IncludeAllDerived { get; set; }

    /// <summary>Eşleştirmeyi devralacak tip dönüştürücü türü (<c>ConvertUsing</c>).</summary>
    public Type? TypeConverter { get; set; }

    /// <summary>AutoMapper uyumluluğu için kabul edilir; VeloxMapper arayüz proxy'leri üretmez.</summary>
    public bool AsProxy { get; set; }
}
