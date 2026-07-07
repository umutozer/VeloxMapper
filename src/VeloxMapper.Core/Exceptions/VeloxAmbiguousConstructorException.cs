using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// Haritalama için hedef nesne oluşturulurken, hangi kurucunun (constructor) seçileceği net olmadığında fırlatılır.
/// </summary>
public class VeloxAmbiguousConstructorException : VeloxException
{
    /// <summary>Hedef tür için belirsiz kurucu durumunu açıklayan yeni bir istisna oluşturur.</summary>
    /// <param name="targetType">Kurucusu belirsiz olan hedef tür.</param>
    public VeloxAmbiguousConstructorException(Type targetType)
        : base($"'{targetType.Name}' türü için eşleşen kurucu metot bulunamadı veya birden fazla kurucu arasında belirsizlik var. Çözüm için [VeloxConstructor] attribute'u kullanın.") 
    { 
    }
}
