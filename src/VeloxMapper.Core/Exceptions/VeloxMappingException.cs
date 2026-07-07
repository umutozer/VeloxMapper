using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// Çalışma zamanında (runtime) gerçekleşen eşleştirme hatalarında fırlatılır.
/// </summary>
public class VeloxMappingException : VeloxException
{
    /// <summary>Belirtilen hata mesajı ile yeni bir eşleştirme istisnası oluşturur.</summary>
    /// <param name="message">Hatayı açıklayan mesaj.</param>
    public VeloxMappingException(string message) : base(message) { }

    /// <summary>Belirtilen hata mesajı ve iç istisna ile yeni bir eşleştirme istisnası oluşturur.</summary>
    /// <param name="message">Hatayı açıklayan mesaj.</param>
    /// <param name="innerException">Bu istisnaya neden olan asıl istisna.</param>
    public VeloxMappingException(string message, Exception innerException) : base(message, innerException) { }
}
