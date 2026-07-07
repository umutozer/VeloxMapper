namespace VeloxMapper.Abstractions;

/// <summary>
/// Kaynak ve hedef özellik (property) isimlerini eşleştirirken kullanılan isimlendirme kuralı arayüzü.
/// (Örneğin: PascalCase -> camelCase).
/// Sınırlı genişletilebilirlik (extensibility) kuralına uygun oluşturulmuştur.
/// </summary>
public interface ICustomNamingConvention
{
    /// <summary>
    /// Eşleştirme sürecinde özellik ismini normalize eder.
    /// </summary>
    /// <param name="propertyName">Özellik adı</param>
    /// <returns>Normalize edilmiş ve eşlaştırmada baz alınacak ad</returns>
    string Normalize(string propertyName);
}
