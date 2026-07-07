namespace VeloxMapper.Exceptions;

/// <summary>
/// AssertConfigurationIsValid() çağrısında tespit edilen doğrulama hatalarında fırlatılır.
/// Eksik eşleştirme, uyumsuz tür ataması veya yapılandırılmamış property'ler bu hatayı tetikler.
/// </summary>
public class VeloxValidationException : VeloxException
{
    /// <summary>
    /// Doğrulama hatasını belirten bir mesaj ile oluşturur.
    /// </summary>
    public VeloxValidationException(string message) : base(message) { }

    /// <summary>
    /// Doğrulama hatasını belirten bir mesaj ve iç istisna ile oluşturur.
    /// </summary>
    public VeloxValidationException(string message, System.Exception innerException)
        : base(message, innerException) { }
}
