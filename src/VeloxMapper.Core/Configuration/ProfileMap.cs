using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Bir profilin etkin (global ayarlarla birleştirilmiş) konvansiyon ayarları. Eşleştirme motoru üye eşleştirme,
/// null davranışı ve value transformer kararlarını bu nesneye göre verir.
/// </summary>
/// <remarks>
/// Birleştirme kuralları AutoMapper ile aynıdır: profilde verilmeyen tekil ayarlar global değerden alınır,
/// liste ayarları (ön ekler, global ignore, value transformer'lar) profil + global olarak birleştirilir.
/// </remarks>
internal sealed class ProfileMap
{
    internal static readonly Func<PropertyInfo, bool> DefaultShouldMapProperty =
        p => (p.GetMethod != null && p.GetMethod.IsPublic) || (p.SetMethod != null && p.SetMethod.IsPublic);

    internal static readonly Func<FieldInfo, bool> DefaultShouldMapField = f => f.IsPublic;

    internal static readonly Func<MethodInfo, bool> DefaultShouldMapMethod = m => !m.IsSpecialName;

    internal static readonly Func<ConstructorInfo, bool> DefaultShouldUseConstructor = _ => true;

    private ProfileMap()
    {
    }

    public string? Name { get; private set; }
    public ICustomNamingConvention? SourceNamingConvention { get; private set; }
    public ICustomNamingConvention? DestinationNamingConvention { get; private set; }
    public IReadOnlyList<string> Prefixes { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Postfixes { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> DestinationPrefixes { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> DestinationPostfixes { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<KeyValuePair<string, string>> MemberNameReplacers { get; private set; } = Array.Empty<KeyValuePair<string, string>>();
    public IReadOnlyList<string> GlobalIgnores { get; private set; } = Array.Empty<string>();
    public bool AllowNullCollections { get; private set; }
    public bool AllowNullDestinationValues { get; private set; } = true;
    public bool EnableNullPropagationForQueryMapping { get; private set; } = true;
    public Func<PropertyInfo, bool> ShouldMapProperty { get; private set; } = DefaultShouldMapProperty;
    public Func<FieldInfo, bool> ShouldMapField { get; private set; } = DefaultShouldMapField;
    public Func<MethodInfo, bool> ShouldMapMethod { get; private set; } = DefaultShouldMapMethod;
    public Func<ConstructorInfo, bool> ShouldUseConstructor { get; private set; } = DefaultShouldUseConstructor;
    public bool ConstructorMappingEnabled { get; private set; } = true;

    /// <summary>Parametresiz kaynak metotlarında varsayılan <c>Get</c> ön eki tanınsın mı (<c>ClearPrefixes</c> kapatır).</summary>
    public bool RecognizeGetPrefix { get; private set; } = true;

    public IReadOnlyList<Type> SourceExtensionMethodTypes { get; private set; } = Array.Empty<Type>();
    public ValueTransformerCollection? ProfileTransformers { get; private set; }
    public ValueTransformerCollection? GlobalTransformers { get; private set; }

    /// <summary>Kaynak üye adı → eşleşme adayları önbelleği (motor tarafından doldurulur).</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string[]> SourceNameCandidates { get; } = new(StringComparer.Ordinal);

    /// <summary>Hedef üye adı → eşleşme adayları önbelleği (motor tarafından doldurulur).</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string[]> DestinationNameCandidates { get; } = new(StringComparer.Ordinal);

    /// <summary>Tür → okunabilir kaynak üyeleri önbelleği.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<Type, MemberInfo[]> SourceMembersCache { get; } = new();

    /// <summary>Tür → yazılabilir hedef üyeleri önbelleği.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<Type, MemberInfo[]> DestinationMembersCache { get; } = new();

    /// <summary>Hedef üye adı global ignore listesindeki bir ön ekle başlıyorsa <c>true</c>.</summary>
    public bool IsGloballyIgnored(string memberName)
    {
        foreach (var prefix in GlobalIgnores)
        {
            if (memberName.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Profil ve global yapılandırmayı birleştirerek etkin ayarları üretir.
    /// </summary>
    /// <param name="profile">Profil yapılandırması (inline eşleştirmeler için <c>null</c>).</param>
    /// <param name="global">Global yapılandırma.</param>
    /// <param name="globalNamingConvention">Her iki taraf için ortak isimlendirme kuralı (VeloxMapper 5.x <c>NamingConvention</c>).</param>
    internal static ProfileMap Create(ProfileConfiguration? profile, ProfileConfiguration global, ICustomNamingConvention? globalNamingConvention)
    {
        var p = profile ?? global;
        static List<T> Merge<T>(IEnumerable<T> first, IEnumerable<T> second) => first.Concat(second).Distinct().ToList();
        var isGlobal = ReferenceEquals(p, global);

        return new ProfileMap
        {
            Name = p.Name,
            SourceNamingConvention = p.SourceMemberNamingConvention ?? global.SourceMemberNamingConvention ?? globalNamingConvention,
            DestinationNamingConvention = p.DestinationMemberNamingConvention ?? global.DestinationMemberNamingConvention ?? globalNamingConvention,
            Prefixes = isGlobal ? global.Prefixes.ToList() : Merge(p.Prefixes, global.Prefixes),
            Postfixes = isGlobal ? global.Postfixes.ToList() : Merge(p.Postfixes, global.Postfixes),
            DestinationPrefixes = isGlobal ? global.DestinationPrefixes.ToList() : Merge(p.DestinationPrefixes, global.DestinationPrefixes),
            DestinationPostfixes = isGlobal ? global.DestinationPostfixes.ToList() : Merge(p.DestinationPostfixes, global.DestinationPostfixes),
            MemberNameReplacers = isGlobal ? global.MemberNameReplacers.ToList() : Merge(p.MemberNameReplacers, global.MemberNameReplacers),
            GlobalIgnores = isGlobal ? global.GlobalIgnores.ToList() : Merge(p.GlobalIgnores, global.GlobalIgnores),
            AllowNullCollections = p.AllowNullCollections ?? global.AllowNullCollections ?? false,
            AllowNullDestinationValues = p.AllowNullDestinationValues ?? global.AllowNullDestinationValues ?? true,
            EnableNullPropagationForQueryMapping = p.EnableNullPropagationForQueryMapping ?? global.EnableNullPropagationForQueryMapping ?? true,
            ShouldMapProperty = p.ShouldMapProperty ?? global.ShouldMapProperty ?? DefaultShouldMapProperty,
            ShouldMapField = p.ShouldMapField ?? global.ShouldMapField ?? DefaultShouldMapField,
            ShouldMapMethod = p.ShouldMapMethod ?? global.ShouldMapMethod ?? DefaultShouldMapMethod,
            ShouldUseConstructor = p.ShouldUseConstructor ?? global.ShouldUseConstructor ?? DefaultShouldUseConstructor,
            ConstructorMappingEnabled = !(p.ConstructorMappingDisabled || global.ConstructorMappingDisabled),
            RecognizeGetPrefix = !(p.PrefixesCleared || (isGlobal && global.PrefixesCleared)),
            SourceExtensionMethodTypes = isGlobal ? global.SourceExtensionMethodTypes.ToList() : Merge(p.SourceExtensionMethodTypes, global.SourceExtensionMethodTypes),
            ProfileTransformers = isGlobal ? null : p.ValueTransformers,
            GlobalTransformers = global.ValueTransformers,
        };
    }
}
