using System;

namespace VeloxMapper.Configuration;

/// <summary>
/// ForPath ile tanımlanan derin yol eşleştirme kuralı.
/// </summary>
public sealed class ForPathRule
{
    /// <summary>Hedef nesne üzerindeki property segmentleri. Örn: ["Customer", "Name"]</summary>
    public string[] PathSegments { get; }

    /// <summary>Üye eşleştirme kuralı detayı.</summary>
    public MemberMappingRule MemberRule { get; }

    /// <summary>
    /// Yeni bir ForPathRule örneği oluşturur.
    /// </summary>
    public ForPathRule(string[] pathSegments, MemberMappingRule memberRule)
    {
        PathSegments = pathSegments ?? throw new ArgumentNullException(nameof(pathSegments));
        MemberRule = memberRule ?? throw new ArgumentNullException(nameof(memberRule));
    }
}
