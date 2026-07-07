using System;
using System.Collections.Generic;
using System.Linq;
using VeloxMapper.Abstractions;

namespace VeloxMapper;

/// <summary>
/// 100% thread-safe ve singleton yaşam döngüsüne sahip ana Mapper arayüzü.
/// DI (Dependency Injection) konteynerine Singleton olarak kaydedilir.
/// </summary>
public interface IVeloxMapper
{
    /// <summary>
    /// Belirtilen kaynak nesneyi hedef türe dönüştürerek yeni bir nesne örneği döndürür.
    /// Kaynak türü çalışma zamanında çözümlenir.
    /// </summary>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <param name="source">Kaynak nesne</param>
    /// <returns>Eşleştirilmiş yeni hedef nesne</returns>
    TDestination Map<TDestination>(object source);

    /// <summary>
    /// Strongly-typed Map — kaynak ve hedef türleri derleme zamanında belirlenir.
    /// AutoMapper'ın <c>mapper.Map&lt;Source, Dest&gt;(source)</c> karşılığıdır.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <param name="source">Kaynak nesne</param>
    /// <returns>Eşleştirilmiş yeni hedef nesne</returns>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Kaynak nesnenin verilerini var olan hedef nesne örneği üzerine yazar (Patch işlemi).
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <param name="source">Kaynak nesne</param>
    /// <param name="destination">Var olan hedef nesne</param>
    void Map<TSource, TDestination>(TSource source, TDestination destination);



    /// <summary>
    /// IQueryable nesnelerini hedef türe projeksiyonla çevirir (Entity Framework uyumlu AST).
    /// </summary>
    /// <typeparam name="TDestination">Hedef projeksiyon türü</typeparam>
    /// <param name="source">IQueryable kaynak</param>
    /// <returns>Projeksiyon uygulanmış IQueryable</returns>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source);

    /// <summary>
    /// Çalışma zamanı bağlam nesnesini (ResolutionContext) yapılandırarak kaynak nesneyi hedef türe dönüştürür.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <param name="source">Kaynak nesne</param>
    /// <param name="contextConfig">Bağlam yapılandırma eylemi</param>
    /// <returns>Eşleştirilmiş yeni hedef nesne</returns>
    TDestination Map<TSource, TDestination>(TSource source, Action<VeloxResolutionContext> contextConfig);

    /// <summary>
    /// Çalışma zamanı bağlam nesnesini (ResolutionContext) yapılandırarak kaynak nesnenin verilerini var olan hedef nesne örneği üzerine yazar.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <param name="source">Kaynak nesne</param>
    /// <param name="destination">Var olan hedef nesne</param>
    /// <param name="contextConfig">Bağlam yapılandırma eylemi</param>
    void Map<TSource, TDestination>(TSource source, TDestination destination, Action<VeloxResolutionContext> contextConfig);

    /// <summary>
    /// Dahili kullanım için: Belirtilen bağlam nesnesini (ResolutionContext) kullanarak dönüşüm yapar.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, VeloxResolutionContext context);
}
