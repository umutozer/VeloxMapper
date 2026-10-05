using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// Çakışan, geçersiz veya belirsiz yapılandırma durumlarında fırlatılır. (Fail-fast prensibi)
/// </summary>
public class VeloxConfigurationException : VeloxException
{
    /// <summary>Belirtilen hata mesajı ile yeni bir yapılandırma istisnası oluşturur.</summary>
    /// <param name="message">Yapılandırma hatasını açıklayan mesaj.</param>
    public VeloxConfigurationException(string message) : base(message) { }

    /// <summary>Belirtilen hata mesajı ve iç istisna ile yeni bir yapılandırma istisnası oluşturur.</summary>
    /// <param name="message">Yapılandırma hatasını açıklayan mesaj.</param>
    /// <param name="innerException">Asıl istisna.</param>
    public VeloxConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
