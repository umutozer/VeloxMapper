using System;
using System.Linq;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Resolver, converter ve mapping action türlerini (AutoMapper uyumlu ve VeloxMapper 5.x arayüzleri) tanıyan yardımcı.
/// </summary>
internal static class ExtensibilityTypes
{
    internal static readonly Type[] ValueResolvers = { typeof(IValueResolver<,,>), typeof(IVeloxValueResolver<,,>) };
    internal static readonly Type[] MemberValueResolvers = { typeof(IMemberValueResolver<,,,>), typeof(IVeloxMemberValueResolver<,,,>) };
    internal static readonly Type[] ValueConverters = { typeof(IValueConverter<,>), typeof(IVeloxValueConverter<,>) };
    internal static readonly Type[] TypeConverters = { typeof(ITypeConverter<,>), typeof(IVeloxTypeConverter<,>) };
    internal static readonly Type[] MappingActions = { typeof(IMappingAction<,>), typeof(IVeloxMappingAction<,>) };

    /// <summary>Tüm genişletme arayüzlerinin generic tanımları (DI taraması için).</summary>
    internal static readonly Type[] All = ValueResolvers.Concat(MemberValueResolvers).Concat(ValueConverters)
        .Concat(TypeConverters).Concat(MappingActions).ToArray();

    /// <summary>Türün verilen generic arayüzlerden en az birini uygulayıp uygulamadığını kontrol eder.</summary>
    internal static bool Implements(Type type, Type[] openInterfaces)
        => type.GetInterfaces().Any(i => i.IsGenericType && openInterfaces.Contains(i.GetGenericTypeDefinition()));

    /// <summary>
    /// Türün uyguladığı arayüzlerden, generic argümanları verilen koşulu sağlayan ilkini döndürür.
    /// </summary>
    internal static Type? FindInterface(Type type, Type[] openInterfaces, Func<Type[], bool>? argumentsMatch = null)
    {
        foreach (var open in openInterfaces)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != open) continue;
                if (argumentsMatch == null || argumentsMatch(iface.GetGenericArguments())) return iface;
            }
        }

        return null;
    }

    /// <summary>
    /// Türün beklenen genişletme arayüzünü uygulamadığı durumda açıklayıcı bir hata fırlatır.
    /// </summary>
    internal static void EnsureImplements(Type type, Type[] openInterfaces, string usage)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (type.IsGenericTypeDefinition) return; // open generic converter'lar kapatıldığında doğrulanır
        if (!Implements(type, openInterfaces))
        {
            var names = string.Join(" veya ", openInterfaces.Select(t => t.Name.Substring(0, t.Name.IndexOf('`')) + "<>"));
            throw new ArgumentException($"'{type.FullName}' türü {usage} için kullanılamaz; {names} arayüzünü uygulamalıdır.", nameof(type));
        }
    }
}
