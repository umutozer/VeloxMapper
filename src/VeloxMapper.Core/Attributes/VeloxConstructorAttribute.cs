using System;

namespace VeloxMapper.Attributes;

/// <summary>
/// Hedef tür (destination type) oluşturulurken hangi kurucu metodun (constructor) kullanılacağını belirler.
/// Eğer birden fazla kurucu varsa, hedef sınıfta bu nitelik işaretlenmiş olana öncelik verilir.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class VeloxConstructorAttribute : Attribute
{
}
