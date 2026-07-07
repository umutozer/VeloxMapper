using System;

namespace VeloxMapper.Attributes;

/// <summary>
/// Source Generator (Layer 1) tarafından analiz edilerek, derleme zamanında NativeAOT uyumlu mapper sınıfının oluşturulmasını tetikler.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class VeloxMapAttribute : Attribute
{
    /// <summary>Eşleştirmenin kaynak (source) türü.</summary>
    public Type SourceType { get; }

    /// <summary>Eşleştirmenin hedef (destination) türü.</summary>
    public Type DestinationType { get; }

    /// <summary>
    /// Belirtilen kaynak ve hedef türleri için derleme zamanı (Layer 1) eşleştirmesi tanımlar.
    /// </summary>
    /// <param name="sourceType">Eşleştirmenin kaynak türü.</param>
    /// <param name="destinationType">Eşleştirmenin hedef türü.</param>
    public VeloxMapAttribute(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }
}
