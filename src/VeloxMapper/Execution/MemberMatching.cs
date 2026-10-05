using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using VeloxMapper.Configuration;

namespace VeloxMapper.Execution;

/// <summary>
/// Profil ayarlarına (ShouldMapProperty/Field/Method) göre kaynak ve hedef üyelerini listeler.
/// </summary>
internal static class TypeMembers
{
    private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Kaynak olarak okunabilen üyeler: property'ler, field'lar ve parametresiz metotlar.</summary>
    internal static MemberInfo[] GetSourceMembers(Type type, ProfileMap profile)
        => profile.SourceMembersCache.GetOrAdd(type, t => DiscoverSourceMembers(t, profile));

    /// <summary>
    /// Hedefte atanabilen üyeler: setter'ı olan property'ler, readonly olmayan field'lar ve setter'ı olmayan
    /// ama mevcut örneğine eşlenebilen koleksiyon property'leri.
    /// </summary>
    internal static MemberInfo[] GetDestinationMembers(Type type, ProfileMap profile)
        => profile.DestinationMembersCache.GetOrAdd(type, t => DiscoverDestinationMembers(t, profile));

    internal static Type GetMemberType(MemberInfo member) => member switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        MethodInfo m => m.ReturnType,
        _ => throw new ArgumentException($"Desteklenmeyen üye türü: {member}")
    };

    internal static bool CanWrite(MemberInfo member) => member switch
    {
        PropertyInfo p => p.CanWrite,
        FieldInfo f => !f.IsInitOnly && !f.IsLiteral,
        _ => false
    };

    internal static Expression Access(Expression instance, MemberInfo member) => member switch
    {
        MethodInfo m => Expression.Call(instance, m),
        _ => Expression.MakeMemberAccess(instance, member)
    };

    private static MemberInfo[] DiscoverSourceMembers(Type type, ProfileMap profile)
    {
        var members = new List<MemberInfo>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in GetPropertiesHidingAware(type))
        {
            if (property.GetMethod == null || property.GetIndexParameters().Length != 0) continue;
            if (!profile.ShouldMapProperty(property)) continue;
            if (names.Add(property.Name)) members.Add(property);
        }

        foreach (var field in type.GetFields(InstanceMembers))
        {
            if (IsCompilerGenerated(field) || !profile.ShouldMapField(field)) continue;
            if (names.Add(field.Name)) members.Add(field);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.ReturnType == typeof(void) || method.GetParameters().Length != 0 || method.IsGenericMethodDefinition) continue;
            if (method.DeclaringType == typeof(object) || method.GetBaseDefinition().DeclaringType == typeof(object)) continue;
            if (!profile.ShouldMapMethod(method)) continue;
            if (names.Add(method.Name)) members.Add(method);
        }

        return members.ToArray();
    }

    private static MemberInfo[] DiscoverDestinationMembers(Type type, ProfileMap profile)
    {
        var members = new List<MemberInfo>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in GetPropertiesHidingAware(type))
        {
            if (property.GetIndexParameters().Length != 0 || !profile.ShouldMapProperty(property)) continue;

            var writable = property.SetMethod != null;
            var inPlaceCollection = !writable && property.GetMethod != null && IsMutableCollectionType(property.PropertyType);
            if ((writable || inPlaceCollection) && names.Add(property.Name)) members.Add(property);
        }

        foreach (var field in type.GetFields(InstanceMembers))
        {
            if (field.IsInitOnly || field.IsLiteral || IsCompilerGenerated(field) || !profile.ShouldMapField(field)) continue;
            if (names.Add(field.Name)) members.Add(field);
        }

        return members.ToArray();
    }

    /// <summary>
    /// <c>new</c> ile gizlenen property'lerde en türetilmiş olanı döndürür (AmbiguousMatch hatalarını önler).
    /// </summary>
    private static IEnumerable<PropertyInfo> GetPropertiesHidingAware(Type type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (var property in current.GetProperties(InstanceMembers | BindingFlags.DeclaredOnly))
            {
                if (seen.Add(property.Name)) yield return property;
            }
        }

        if (type.IsInterface)
        {
            foreach (var iface in type.GetInterfaces())
            {
                foreach (var property in iface.GetProperties())
                {
                    if (seen.Add(property.Name)) yield return property;
                }
            }
        }
    }

    private static bool IsCompilerGenerated(FieldInfo field)
        => field.Name.IndexOf('<') >= 0 || field.IsDefined(typeof(CompilerGeneratedAttribute), false);

    /// <summary>Clear/Add ile yerinde güncellenebilen (dizi olmayan, ICollection&lt;T&gt;) koleksiyon türü mü?</summary>
    internal static bool IsMutableCollectionType(Type type)
    {
        if (type.IsArray || type == typeof(string)) return false;
        var elementType = CollectionExpressionHelper.GetCollectionElementType(type);
        return elementType != null && typeof(ICollection<>).MakeGenericType(elementType).IsAssignableFrom(type);
    }
}

/// <summary>
/// Kaynak ve hedef üye adlarını profil konvansiyonlarına (ön/son ek, isimlendirme kuralı, ad değiştirme,
/// <c>Get</c> metot ön eki) göre eşleştirir. Karşılaştırma büyük/küçük harfe duyarsızdır.
/// </summary>
internal static class NameMatcher
{
    internal static bool IsMatch(MemberInfo sourceMember, string destinationName, ProfileMap profile)
        => IsMatch(sourceMember.Name, sourceMember is MethodInfo, destinationName, profile);

    internal static bool IsMatch(string sourceName, bool isMethod, string destinationName, ProfileMap profile)
    {
        var source = SourceCandidates(sourceName, isMethod, profile);
        var destination = DestinationCandidates(destinationName, profile);
        foreach (var s in source)
        {
            foreach (var d in destination)
            {
                if (string.Equals(s, d, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    private static string[] SourceCandidates(string name, bool isMethod, ProfileMap profile)
        => profile.SourceNameCandidates.GetOrAdd((isMethod ? "m:" : "p:") + name, _ =>
        {
            var replaced = name;
            foreach (var replacer in profile.MemberNameReplacers) replaced = replaced.Replace(replacer.Key, replacer.Value);

            var seeds = new List<string> { name };
            if (replaced != name) seeds.Add(replaced);
            if (isMethod && profile.RecognizeGetPrefix)
            {
                foreach (var seed in seeds.ToList())
                {
                    if (seed.Length > 3 && seed.StartsWith("Get", StringComparison.Ordinal)) seeds.Add(seed.Substring(3));
                }
            }

            return Expand(seeds, profile.Prefixes, profile.Postfixes, profile.SourceNamingConvention);
        });

    private static string[] DestinationCandidates(string name, ProfileMap profile)
        => profile.DestinationNameCandidates.GetOrAdd(name, _ =>
            Expand(new List<string> { name }, profile.DestinationPrefixes, profile.DestinationPostfixes, profile.DestinationNamingConvention));

    private static string[] Expand(List<string> seeds, IReadOnlyList<string> prefixes, IReadOnlyList<string> postfixes, VeloxMapper.Abstractions.ICustomNamingConvention? convention)
    {
        var candidates = new HashSet<string>(seeds, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            foreach (var prefix in prefixes)
            {
                if (seed.Length > prefix.Length && seed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(seed.Substring(prefix.Length));
            }
        }

        foreach (var candidate in candidates.ToList())
        {
            foreach (var postfix in postfixes)
            {
                if (candidate.Length > postfix.Length && candidate.EndsWith(postfix, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(candidate.Substring(0, candidate.Length - postfix.Length));
            }
        }

        if (convention != null)
        {
            foreach (var candidate in candidates.ToList()) candidates.Add(convention.Normalize(candidate));
        }

        return candidates.ToArray();
    }
}

/// <summary>
/// Hedef üye için konvansiyonla kaynak değeri bulur: doğrudan üye, <c>Get</c> metodu, extension metot,
/// flattening (<c>CustomerName</c> → <c>Customer.Name</c>) ve <c>IncludeMembers</c>.
/// </summary>
internal static class ConventionResolver
{
    private const int MaxFlatteningDepth = 8;

    /// <summary>
    /// Hedef üye adına karşılık gelen kaynak ifadesini döndürür; bulunamazsa <c>null</c>.
    /// Dönen ifade null-güvenli değildir; çağıran taraf <c>NullSafety.Wrap</c> uygular.
    /// </summary>
    internal static Expression? Resolve(Expression source, string destinationName, ProfileMap profile, MappingRegistration? registration, MapperConfiguration config)
    {
        var sourceType = source.Type;

        var direct = FindDirect(sourceType, destinationName, profile);
        if (direct != null) return TypeMembers.Access(source, direct);

        var extension = FindExtensionMethod(sourceType, destinationName, profile);
        if (extension != null) return Expression.Call(extension, Coerce(source, extension.GetParameters()[0].ParameterType));

        var flattened = ResolveFlattening(source, destinationName, profile);
        if (flattened != null) return flattened;

        if (registration?.IncludeMembers != null)
        {
            foreach (var include in registration.IncludeMembers)
            {
                var child = ExpressionUtil.ReplaceParameter(include, source);
                child = ExpressionUtil.StripConvert(child);

                var childRegistration = config.FindRegistration(child.Type, registration.DestinationType);
                if (childRegistration != null &&
                    childRegistration.MemberRules.TryGetValue(destinationName, out var childRule) &&
                    childRule.MapFromExpression != null && !childRule.IsIgnored)
                {
                    return ExpressionUtil.ReplaceParameter(childRule.MapFromExpression, child);
                }

                var childMember = FindDirect(child.Type, destinationName, profile);
                if (childMember != null) return TypeMembers.Access(child, childMember);

                var childFlattened = ResolveFlattening(child, destinationName, profile);
                if (childFlattened != null) return childFlattened;
            }
        }

        return null;
    }

    /// <summary>Hedef üye adına isimle eşleşen kaynak üyesini döndürür (flattening hariç).</summary>
    internal static MemberInfo? FindDirect(Type sourceType, string destinationName, ProfileMap profile)
    {
        MemberInfo? fallback = null;
        foreach (var member in TypeMembers.GetSourceMembers(sourceType, profile))
        {
            if (!NameMatcher.IsMatch(member, destinationName, profile)) continue;
            if (string.Equals(member.Name, destinationName, StringComparison.Ordinal)) return member;
            fallback ??= member;
        }

        return fallback;
    }

    private static MethodInfo? FindExtensionMethod(Type sourceType, string destinationName, ProfileMap profile)
    {
        foreach (var type in profile.SourceExtensionMethodTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!method.IsDefined(typeof(ExtensionAttribute), false) || method.ReturnType == typeof(void)) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsAssignableFrom(sourceType)) continue;
                if (NameMatcher.IsMatch(method.Name, isMethod: true, destinationName, profile)) return method;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>CustomerAddressCity</c> → <c>source.Customer.Address.City</c> biçiminde flattening ifadesi üretir.
    /// </summary>
    internal static Expression? ResolveFlattening(Expression source, string destinationName, ProfileMap profile)
        => TryFlatten(source, destinationName, profile, 0);

    private static Expression? TryFlatten(Expression current, string remaining, ProfileMap profile, int depth)
    {
        if (depth > MaxFlatteningDepth || remaining.Length == 0) return null;

        var members = TypeMembers.GetSourceMembers(current.Type, profile);
        for (var length = 1; length <= remaining.Length; length++)
        {
            var prefix = remaining.Substring(0, length);
            foreach (var member in members)
            {
                if (!NameMatcher.IsMatch(member, prefix, profile)) continue;

                var access = TypeMembers.Access(current, member);
                var rest = remaining.Substring(length);
                if (rest.Length == 0)
                {
                    if (depth > 0) return access; // en az bir seviye derinlik: gerçek flattening
                    continue;
                }

                var memberType = TypeMembers.GetMemberType(member);
                if (IsLeafType(memberType)) continue;

                var nested = TryFlatten(access, rest, profile, depth + 1);
                if (nested != null) return nested;
            }
        }

        return null;
    }

    private static bool IsLeafType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(decimal) ||
               underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(Guid) || underlying == typeof(TimeSpan);
    }

    private static Expression Coerce(Expression expression, Type type)
        => expression.Type == type ? expression : Expression.Convert(expression, type);
}
