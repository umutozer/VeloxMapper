namespace VeloxMapper.Abstractions;

/// <summary>
/// VeloxMapper çalışma zamanı veya derleme zamanı durumları (hata, eşleşme eksikliği, vb.) için
/// sıfır maliyetli ve ILogger gibi dış servislere basılabilen (sink) diagnostic arayüzü.
/// </summary>
public interface IVeloxDiagnosticsSink
{
    /// <summary>
    /// Bir olay kaydeder.
    /// </summary>
    void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null);
}
