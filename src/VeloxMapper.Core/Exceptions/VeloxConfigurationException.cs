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
}
