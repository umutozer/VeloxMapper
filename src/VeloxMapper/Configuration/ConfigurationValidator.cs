using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Caching;
using VeloxMapper.Exceptions;
using VeloxMapper.Execution;

namespace VeloxMapper.Configuration;

/// <summary>
/// <c>AssertConfigurationIsValid</c> kurallarını uygular: eşlenmemiş hedef/kaynak üyeleri, çözülemeyen kurucular ve
/// dönüştürülemeyen üye türleri. Kurallar çalışma zamanı motoruyla aynı çözümleme mantığını kullanır.
/// </summary>
internal sealed class ConfigurationValidator
{
    private readonly MapperConfiguration _config;

    public ConfigurationValidator(MapperConfiguration config) => _config = config;

    public List<string> Validate(IReadOnlyList<MappingRegistration> registrations)
    {
        var errors = new List<string>();
        foreach (var registration in registrations.OrderBy(r => r.ProfileName).ThenBy(r => r.SourceType.FullName).ThenBy(r => r.DestinationType.FullName))
        {
            if (registration.ValidationDisabled || registration.HasTypeConverter) continue;
            if (!ExpressionBuilder.IsComplex(registration.DestinationType)) continue;
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(registration.SourceType)) continue; // sözlük anahtarları çalışma zamanında bilinir
            if (registration.SourceType.ContainsGenericParameters || registration.DestinationType.ContainsGenericParameters) continue;

            var mapErrors = ValidateRegistration(registration);
            if (mapErrors.Count == 0) continue;

            var header = $"[{registration.ProfileName ?? "Global"}] {Describe(registration.SourceType)} -> {Describe(registration.DestinationType)} ({registration.MemberList} üye listesi)";
            errors.Add(header + Environment.NewLine + string.Join(Environment.NewLine, mapErrors.Select(e => "       - " + e)));
        }

        return errors;
    }

    private List<string> ValidateRegistration(MappingRegistration registration)
    {
        var errors = new List<string>();
        var profile = _config.GetProfile(registration);
        var context = Expression.Parameter(typeof(VeloxResolutionContext), "context");
        var scope = new BuildScope(_config, MappingMode.Map, context);
        var source = Expression.Parameter(registration.SourceType, "src");
        var destinationType = registration.RedirectDestinationType ?? registration.DestinationType;

        // 1. Kurucu
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasFactory = registration.FactoryDelegate != null || registration.FactoryDelegateWithContext != null || registration.ConstructUsingServiceLocator ||
                         destinationType.IsAbstract || destinationType.IsInterface; // soyut hedefler Include ile somut türlere yönlenir
        if (!hasFactory)
        {
            try
            {
                var (ctor, _, error) = ExpressionBuilder.SelectConstructor(source, registration, destinationType, scope, profile);
                if (ctor != null)
                {
                    foreach (var parameter in ctor.GetParameters()) consumed.Add(parameter.Name!);
                }
                else if (!destinationType.IsValueType && !registration.CtorValidationDisabled)
                {
                    errors.Add(error!);
                }
            }
            catch (VeloxException ex)
            {
                if (!registration.CtorValidationDisabled) errors.Add(ex.Message);
            }
        }

        // 2. Hedef üyeler
        if (registration.MemberList == MemberList.Destination)
        {
            foreach (var member in TypeMembers.GetDestinationMembers(destinationType, profile))
            {
                MemberMappingRule? rule = null;
                registration.MemberRules.TryGetValue(member.Name, out rule);
                if (rule != null && (rule.IsIgnored || rule.IsDoNotValidate)) continue;
                if (rule == null && profile.IsGloballyIgnored(member.Name)) continue;
                if (consumed.Contains(member.Name)) continue;
                if (registration.ForPathRules.Any(p => p.PathSegments[0] == member.Name)) continue;

                var memberType = TypeMembers.GetMemberType(member);
                if (rule != null && rule.HasValueSource)
                {
                    if (rule.MapFromExpression != null) CheckConvertible(rule.MapFromExpression.ReturnType, memberType, member.Name, scope, profile, errors);
                    continue;
                }

                Expression? convention;
                try
                {
                    convention = ConventionResolver.Resolve(source, member.Name, profile, registration, _config);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    convention = null;
                }

                if (convention == null)
                {
                    errors.Add($"'{member.Name}' ({Describe(memberType)}): eşleşen kaynak üye bulunamadı.");
                    continue;
                }

                CheckConvertible(convention.Type, memberType, member.Name, scope, profile, errors);
            }
        }

        // 3. Kaynak üyeler
        if (registration.MemberList == MemberList.Source)
        {
            var used = CollectUsedSourceMembers(registration, source, profile, consumed);
            foreach (var member in TypeMembers.GetSourceMembers(registration.SourceType, profile))
            {
                if (member is MethodInfo) continue;
                if (used.Contains(member.Name) || registration.SourceMembersNotValidated.Contains(member.Name)) continue;
                if (registration.IgnoreSourceMembersWithInaccessibleGetter && member is PropertyInfo p && (p.GetMethod == null || !p.GetMethod.IsPublic)) continue;
                errors.Add($"Kaynak üye '{member.Name}' ({Describe(TypeMembers.GetMemberType(member))}) hiçbir hedef üyeye eşlenmiyor.");
            }
        }

        return errors;
    }

    private static void CheckConvertible(Type sourceType, Type destinationType, string memberName, BuildScope scope, ProfileMap profile, List<string> errors)
    {
        try
        {
            var mapped = ExpressionBuilder.MapValue(Expression.Parameter(sourceType, "value"), destinationType, null, scope, profile, null, inlineTypeMap: false);
            if (mapped == null)
            {
                errors.Add($"'{memberName}': {Describe(sourceType)} türü {Describe(destinationType)} türüne dönüştürülemiyor. CreateMap<{Describe(sourceType)}, {Describe(destinationType)}>().ConvertUsing(...) ekleyin.");
            }
        }
        catch (VeloxException ex)
        {
            errors.Add($"'{memberName}': {ex.Message}");
        }
    }

    private HashSet<string> CollectUsedSourceMembers(MappingRegistration registration, ParameterExpression source, ProfileMap profile, HashSet<string> consumed)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var collector = new RootMemberCollector(used);

        void Collect(Expression? expression, ParameterExpression parameter)
        {
            if (expression == null) return;
            collector.Root = parameter;
            collector.Visit(expression);
        }

        foreach (var member in TypeMembers.GetDestinationMembers(registration.DestinationType, profile))
        {
            registration.MemberRules.TryGetValue(member.Name, out var rule);
            if (rule?.IsIgnored == true) continue;

            if (rule?.MapFromExpression != null) Collect(rule.MapFromExpression.Body, rule.MapFromExpression.Parameters[0]);
            else if (rule?.SourceMemberForResolver != null) Collect(rule.SourceMemberForResolver.Body, rule.SourceMemberForResolver.Parameters[0]);
            else if (rule?.ValueConverterSourceMember != null) Collect(rule.ValueConverterSourceMember.Body, rule.ValueConverterSourceMember.Parameters[0]);
            else Collect(ConventionResolver.Resolve(source, member.Name, profile, registration, _config), source);
        }

        foreach (var parameterName in consumed) Collect(ConventionResolver.Resolve(source, parameterName, profile, registration, _config), source);
        foreach (var pathRule in registration.ForPathRules)
        {
            if (pathRule.MemberRule.MapFromExpression is { } lambda) Collect(lambda.Body, lambda.Parameters[0]);
        }

        foreach (var ctorRule in registration.CtorParamRules)
        {
            if (ctorRule.MapFromExpression is { } lambda) Collect(lambda.Body, lambda.Parameters[0]);
        }

        if (registration.IncludeMembers != null)
        {
            foreach (var include in registration.IncludeMembers) Collect(include.Body, include.Parameters[0]);
        }

        return used;
    }

    private static string Describe(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var name = type.Name.Substring(0, type.Name.IndexOf('`'));
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>";
    }

    /// <summary>Bir ifadede kök parametre üzerinde doğrudan erişilen üye adlarını toplar.</summary>
    private sealed class RootMemberCollector : ExpressionVisitor
    {
        private readonly HashSet<string> _used;

        public RootMemberCollector(HashSet<string> used) => _used = used;

        public ParameterExpression? Root { get; set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsRoot(node.Expression)) _used.Add(node.Member.Name);
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null && IsRoot(node.Object)) _used.Add(node.Method.Name);
            return base.VisitMethodCall(node);
        }

        private bool IsRoot(Expression? expression)
        {
            while (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert) expression = unary.Operand;
            return expression == Root;
        }
    }
}
