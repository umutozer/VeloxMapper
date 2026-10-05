namespace VeloxMapper;

/// <summary>
/// VeloxMapper markalı mapper arayüzü. <see cref="IMapper"/> ile birebir aynıdır; 5.x sürümlerinden yükselten
/// projelerin derlenmeye devam etmesi için korunur. DI konteynerine hem <see cref="IMapper"/> hem de
/// <see cref="IVeloxMapper"/> olarak kaydedilir.
/// </summary>
public interface IVeloxMapper : IMapper
{
}
