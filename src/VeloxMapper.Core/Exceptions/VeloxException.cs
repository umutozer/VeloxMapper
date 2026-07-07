using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// VeloxMapper tabanlı tüm istisnaların temel (base) sınıfı.
/// </summary>
public abstract class VeloxException : Exception
{
    /// <summary>Belirtilen hata mesajı ile yeni bir istisna oluşturur.</summary>
    /// <param name="message">Hatayı açıklayan mesaj.</param>
    protected VeloxException(string message) : base(message) { }

    /// <summary>Belirtilen hata mesajı ve iç istisna (inner exception) ile yeni bir istisna oluşturur.</summary>
    /// <param name="message">Hatayı açıklayan mesaj.</param>
    /// <param name="innerException">Bu istisnaya neden olan asıl istisna.</param>
    protected VeloxException(string message, Exception innerException) : base(message, innerException) { }
}
