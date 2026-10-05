using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace VeloxMapper.Configuration;

/// <summary>
/// <c>ReverseMap()</c> için ters yön kaydını üretir. AutoMapper ile aynı kurallar uygulanır:
/// <list type="bullet">
/// <item>Ters eşleştirme varsayılan olarak doğrulanmaz (<c>MemberList.None</c>).</item>
/// <item><c>MapFrom(s =&gt; s.A)</c> → ters yönde <c>A</c> ← <c>d.Member</c>; <c>MapFrom(s =&gt; s.A.B)</c> → ters yönde <c>ForPath(A.B)</c>.</item>
/// <item>Flattening (<c>CustomerName</c> ← <c>Customer.Name</c>) ters yönde unflattening olarak uygulanır.</item>
/// <item><c>ReverseMap()</c> sonrasında zincirlenen açık kurallar otomatik kuralları ezer.</item>
/// <item><c>Ignore</c> kuralları ters çevrilmez.</item>
/// </list>
/// </summary>
internal static class ReverseMapBuilder
{
    internal static MappingRegistration Build(MappingRegistration forward, MappingRegistration reverse)
    {
        reverse.IsGeneratedReverse = true;
        var rules = reverse.MutableMemberRules;
        var forPaths = reverse.ForPathRules.ToList();

        bool PathConfigured(string[] segments)
        {
            var key = string.Join(".", segments);
            return rules.ContainsKey(segments[0]) || forPaths.Any(p => string.Join(".", p.PathSegments) == key);
        }

        // 1. MapFrom üye zincirlerini ters çevir
        foreach (var rule in forward.MemberRules.Values)
        {
            if (rule.IsIgnored || rule.MapFromExpression == null) continue;

            var chain = MemberChain.TryGetPath(rule.MapFromExpression.Body, rule.MapFromExpression.Parameters[0]);
            if (chain == null || !IsWritable(chain[chain.Count - 1])) continue;

            var forwardDestinationMember = FindReadableMember(forward.DestinationType, rule.DestinationMemberName);
            if (forwardDestinationMember == null) continue;

            var lambda = BuildAccessor(forward.DestinationType, forwardDestinationMember);
            var segments = chain.Select(m => m.Name).ToArray();

            if (segments.Length == 1)
            {
                if (!rules.ContainsKey(segments[0])) rules[segments[0]] = new MemberMappingRule(segments[0], lambda);
            }
            else if (!PathConfigured(segments))
            {
                forPaths.Add(new ForPathRule(segments, new MemberMappingRule(segments[segments.Length - 1], lambda)));
            }
        }

        // 2. Flattening → unflattening
        var forwardSourceMembers = GetReadableMembers(forward.SourceType).Select(m => m.Name).ToList();
        foreach (var destinationMember in GetReadableMembers(forward.DestinationType))
        {
            if (forward.MemberRules.TryGetValue(destinationMember.Name, out var existing) && (existing.IsIgnored || existing.HasValueSource)) continue;
            if (forwardSourceMembers.Any(n => string.Equals(n, destinationMember.Name, StringComparison.OrdinalIgnoreCase))) continue;

            var path = ResolveUnflatteningPath(forward.SourceType, destinationMember.Name);
            if (path == null || PathConfigured(path)) continue;

            var lambda = BuildAccessor(forward.DestinationType, destinationMember);
            forPaths.Add(new ForPathRule(path, new MemberMappingRule(path[path.Length - 1], lambda)));
        }

        reverse.ForPathRules = forPaths;

        // 3. Enum eşleştirmesi
        if (forward.EnumMapping != null && !reverse.HasTypeConverter)
        {
            var snapshot = forward.EnumMapping.Reverse();
            reverse.EnumMapping = snapshot;
            reverse.ConvertUsingFunc = (Delegate)typeof(ReverseMapBuilder)
                .GetMethod(nameof(CreateEnumConverter), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(reverse.SourceType, reverse.DestinationType)
                .Invoke(null, new object[] { snapshot })!;
        }

        // 4. Kalıtım ve diğer seçenekler
        if (reverse.IncludedDerivedTypes.Count == 0)
        {
            foreach (var (derivedSource, derivedDestination) in forward.IncludedDerivedTypes)
                reverse.AddIncludedDerivedType(derivedDestination, derivedSource);
        }

        if (reverse.BaseTypeMapping == null && forward.BaseTypeMapping is { } baseMapping)
            reverse.BaseTypeMapping = (baseMapping.BaseDestination, baseMapping.BaseSource);

        if (reverse.MaxDepth == 0) reverse.MaxDepth = forward.MaxDepth;
        reverse.PreserveReferences |= forward.PreserveReferences;
        return reverse;
    }

    private static Func<TSource, TDestination, TDestination> CreateEnumConverter<TSource, TDestination>(EnumMappingSnapshot snapshot)
        => (src, _) => src == null ? default! : (TDestination)snapshot.Convert(src);

    private static LambdaExpression BuildAccessor(Type type, MemberInfo member)
    {
        var param = Expression.Parameter(type, "src");
        return Expression.Lambda(Expression.MakeMemberAccess(param, member), param);
    }

    private static bool IsWritable(MemberInfo member) => member switch
    {
        PropertyInfo p => p.CanWrite,
        FieldInfo f => !f.IsInitOnly,
        _ => false
    };

    private static IEnumerable<MemberInfo> GetReadableMembers(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0) yield return property;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) yield return field;
    }

    private static MemberInfo? FindReadableMember(Type type, string name)
        => GetReadableMembers(type).FirstOrDefault(m => m.Name == name);

    /// <summary>
    /// <c>CustomerAddressCity</c> gibi düz bir adı kaynak türdeki <c>Customer.Address.City</c> yoluna çözer.
    /// </summary>
    internal static string[]? ResolveUnflatteningPath(Type sourceType, string flattenedName)
    {
        var segments = new List<string>();
        return TryResolve(sourceType, flattenedName, segments, 0) && segments.Count > 1 ? segments.ToArray() : null;
    }

    private static bool TryResolve(Type type, string remaining, List<string> segments, int depth)
    {
        if (remaining.Length == 0) return true;
        if (depth > 8) return false;

        var members = GetReadableMembers(type).ToList();
        for (var length = remaining.Length; length >= 1; length--)
        {
            var candidate = remaining.Substring(0, length);
            var member = members.FirstOrDefault(m => string.Equals(m.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (member == null) continue;

            var memberType = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
            var rest = remaining.Substring(length);
            if (rest.Length == 0 && !IsWritable(member)) continue;
            if (rest.Length > 0 && (!memberType.IsClass || memberType == typeof(string))) continue;

            segments.Add(member.Name);
            if (TryResolve(memberType, rest, segments, depth + 1)) return true;
            segments.RemoveAt(segments.Count - 1);
        }

        return false;
    }
}
