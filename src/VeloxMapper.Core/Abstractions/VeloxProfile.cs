using System;
using System.Collections.Generic;
using VeloxMapper.Configuration;

namespace VeloxMapper;

/// <summary>
/// Eşleştirme yapılandırmalarını gruplandırmak için kullanılan profil taban sınıfı.
/// AutoMapper'ın <c>Profile</c> sınıfının VeloxMapper karşılığıdır.
/// <para>
/// Kullanım:
/// <code>
/// public class UserProfile : VeloxProfile
/// {
///     public UserProfile()
///     {
///         CreateMap&lt;User, UserDto&gt;();
///         CreateMap&lt;Order, OrderDto&gt;()
///             .ForMember(d =&gt; d.Total, opt =&gt; opt.MapFrom(s =&gt; s.Amount));
///     }
/// }
/// </code>
/// </para>
/// </summary>
public abstract class VeloxProfile
{
    // MappingExpression örneklerini generic-agnostic tutan liste.
    // Her CreateMap çağrısı buraya Build fonksiyonunu kaydeder.
    private readonly List<Func<string?, MappingRegistration>> _registrationFactories = new();

    /// <summary>
    /// Profil düzeyinde tanımlanmış value transformer koleksiyonu.
    /// </summary>
    protected ValueTransformerCollection ValueTransformers { get; } = new();

    /// <summary>
    /// Dahili kullanım için ValueTransformers koleksiyonunu dışa açar.
    /// </summary>
    internal ValueTransformerCollection ProfileValueTransformers => ValueTransformers;

    /// <summary>
    /// Kaynak ve hedef tür çifti için yeni bir eşleştirme kaydı oluşturur.
    /// Dönen <see cref="IMappingExpression{TSource,TDestination}"/> üzerinden
    /// ForMember, Ignore ve ConvertUsing yapılandırmaları zincirlenebilir.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür</typeparam>
    /// <typeparam name="TDestination">Hedef tür</typeparam>
    /// <returns>Fluent yapılandırma arayüzü</returns>
    protected IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var expression = new MappingExpression<TSource, TDestination>();

        _registrationFactories.Add(profileName => expression.Build(profileName));
        return expression;
    }

    /// <summary>
    /// Tip nesneleri ile eşleştirme tanımlar. Özellikle Open Generic eşleştirmeleri için kullanılır.
    /// </summary>
    /// <param name="sourceType">Kaynak türü</param>
    /// <param name="destinationType">Hedef türü</param>
    /// <returns>Fluent yapılandırma arayüzü</returns>
    protected IMappingExpression CreateMap(Type sourceType, Type destinationType)
    {
        if (sourceType is null)
        {
            throw new ArgumentNullException(nameof(sourceType)); // Kaynak tür null olamaz.
        }
        if (destinationType is null)
        {
            throw new ArgumentNullException(nameof(destinationType)); // Hedef tür null olamaz.
        }

        // Açık generic (Open Generic) tipler için özel expression oluşturulur
        if (sourceType.IsGenericTypeDefinition || destinationType.IsGenericTypeDefinition)
        {
            var openExpr = new OpenGenericMappingExpression(sourceType, destinationType);
            _registrationFactories.Add(profileName => openExpr.Build(profileName));
            return openExpr;
        }

        var expressionType = typeof(MappingExpression<,>).MakeGenericType(sourceType, destinationType);
        var expression = (IMappingExpression)Activator.CreateInstance(expressionType)!;

        _registrationFactories.Add(profileName => {
            var buildMethod = expressionType.GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (MappingRegistration)buildMethod.Invoke(expression, [profileName])!;
        });

        return expression;
    }

    /// <summary>
    /// Profil içindeki tüm CreateMap çağrılarını işleyerek
    /// dondurulmuş <see cref="MappingRegistration"/> listesi üretir.
    /// MapperConfiguration tarafından çağrılır.
    /// </summary>
    internal List<MappingRegistration> BuildRegistrations()
    {
        var profileName = GetType().Name;
        var registrations = new List<MappingRegistration>(_registrationFactories.Count);

        foreach (var factory in _registrationFactories)
        {
            registrations.Add(factory(profileName));
        }

        return registrations;
    }
}
