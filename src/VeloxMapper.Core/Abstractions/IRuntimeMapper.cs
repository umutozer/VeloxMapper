namespace VeloxMapper;

/// <summary>
/// Bir eşleştirme çağrısı sırasında <see cref="ResolutionContext.Mapper"/> üzerinden erişilen mapper. AutoMapper'ın
/// <c>IRuntimeMapper</c> arayüzü ile aynı addadır; resolver, converter ve action içinden yapılan
/// <c>context.Mapper.Map(...)</c> çağrıları dış çağrının bağlamını (<c>Items</c>, <c>State</c>, servis fabrikası) devralır.
/// </summary>
public interface IRuntimeMapper : IMapper
{
}
