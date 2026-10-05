using System;

namespace VeloxMapper.Exceptions;

/// <summary>
/// IQueryable.ProjectTo&lt;T&gt; metodu içinde desteklenmeyen (MethodCallExpression gibi) bir ifade kullanıldığında fırlatılır.
/// </summary>
public class VeloxProjectionException : VeloxException
{
    /// <summary>Belirtilen hata mesajı ile yeni bir projeksiyon istisnası oluşturur.</summary>
    /// <param name="message">Projeksiyon hatasını açıklayan mesaj.</param>
    public VeloxProjectionException(string message) : base(message) { }

    /// <summary>Belirtilen hata mesajı ve iç istisna ile yeni bir projeksiyon istisnası oluşturur.</summary>
    /// <param name="message">Hata mesajı.</param>
    /// <param name="innerException">Asıl istisna.</param>
    public VeloxProjectionException(string message, Exception innerException) : base(message, innerException) { }
}
