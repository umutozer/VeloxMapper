using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// Hedef tür için kullanılabilecek bir kurucu (constructor) seçilemediğinde fırlatılır: hiçbir kurucunun parametreleri
/// kaynaktan çözülemiyor veya birden fazla <c>[VeloxConstructor]</c> işaretli kurucu var.
/// </summary>
public class VeloxAmbiguousConstructorException : VeloxConfigurationException
{
    /// <summary>Hedef tür için kurucu seçilemediğini belirten bir istisna oluşturur.</summary>
    /// <param name="targetType">Kurucusu seçilemeyen hedef tür.</param>
    public VeloxAmbiguousConstructorException(Type targetType)
        : base($"'{targetType.Name}' türü için kullanılabilir bir kurucu seçilemedi. ForCtorParam, ConstructUsing veya [VeloxConstructor] kullanın.")
    {
        TargetType = targetType;
    }

    /// <summary>Hedef tür ve ayrıntılı açıklama ile bir istisna oluşturur.</summary>
    /// <param name="targetType">Kurucusu seçilemeyen hedef tür.</param>
    /// <param name="message">Ayrıntılı açıklama.</param>
    public VeloxAmbiguousConstructorException(Type targetType, string message)
        : base(message)
    {
        TargetType = targetType;
    }

    /// <summary>Kurucusu seçilemeyen hedef tür.</summary>
    public Type? TargetType { get; }
}
