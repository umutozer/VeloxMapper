using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Abstractions;
using VeloxMapper.Attributes;
using VeloxMapper.Caching;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;

namespace VeloxMapper.Execution;

/// <summary>
/// Çalışma zamanı (Layer 2) eşleştirme ifadelerini üretir. Üç çıktı türü vardır:
/// <list type="bullet">
/// <item><b>Map</b>: <c>Func&lt;TSource, VeloxResolutionContext, TDestination&gt;</c> — yeni nesne oluşturur.</item>
/// <item><b>Patch</b>: <c>Func&lt;TSource, TDestination, VeloxResolutionContext, TDestination&gt;</c> — mevcut nesneye eşler.</item>
/// <item><b>ProjectTo</b>: <c>Expression&lt;Func&lt;TSource, TDestination&gt;&gt;</c> — EF Core tarafından SQL'e çevrilebilir saf ifade.</item>
/// </list>
/// İç içe karmaşık türler Map/Patch modunda çalışma zamanı çağrısıyla (önbellekten), ProjectTo modunda satır içi eşlenir.
/// </summary>
internal static class ExpressionBuilder
{
    private static readonly Type ContextType = typeof(VeloxResolutionContext);
    private static readonly MethodInfo ResolveServiceMethod = typeof(RuntimeServices).GetMethod(nameof(RuntimeServices.Resolve))!;
    private static readonly MethodInfo MapNestedMethod = typeof(Mapper).GetMethod(nameof(Mapper.MapNested), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly PropertyInfo CurrentMemberProperty = typeof(ResolutionContext).GetProperty(nameof(ResolutionContext.CurrentMember))!;
    private static readonly PropertyInfo CurrentDepthProperty = typeof(ResolutionContext).GetProperty("CurrentDepth", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly PropertyInfo ReferenceCacheProperty = typeof(ResolutionContext).GetProperty("ReferenceCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo CacheTryGetMethod = typeof(VeloxReferenceCache).GetMethod(nameof(VeloxReferenceCache.TryGetValue))!;
    private static readonly MethodInfo CacheSetMethod = typeof(VeloxReferenceCache).GetMethod(nameof(VeloxReferenceCache.Set))!;
    private static readonly MethodInfo ObjectToStringMethod = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;

    // ─── Giriş noktaları ────────────────────────────────────────────────────

    /// <summary>Yeni nesne oluşturan eşleştirme lambda'sını üretir: <c>(source, context) =&gt; destination</c>.</summary>
    public static LambdaExpression BuildMapLambda(Type sourceType, Type destinationType, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Map ifadesi üretiliyor: {sourceType.Name} -> {destinationType.Name}", "Debug");
        var source = Expression.Parameter(sourceType, "source");
        var context = Expression.Parameter(ContextType, "context");
        var scope = new BuildScope(config, MappingMode.Map, context);
        var body = ExpressionUtil.Coerce(MapRoot(source, destinationType, null, scope), destinationType);
        return Expression.Lambda(typeof(Func<,,>).MakeGenericType(sourceType, ContextType, destinationType), body, source, context);
    }

    /// <summary>Mevcut nesneye eşleyen lambda'yı üretir: <c>(source, destination, context) =&gt; destination</c>.</summary>
    public static LambdaExpression BuildPatchLambda(Type sourceType, Type destinationType, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Patch ifadesi üretiliyor: {sourceType.Name} -> {destinationType.Name}", "Debug");
        var source = Expression.Parameter(sourceType, "source");
        var destination = Expression.Parameter(destinationType, "destination");
        var context = Expression.Parameter(ContextType, "context");
        var patchScope = new BuildScope(config, MappingMode.Patch, context);

        Expression body = ExpressionUtil.Coerce(MapRoot(source, destinationType, destination, patchScope), destinationType);
        if (ExpressionUtil.CanBeNull(destinationType))
        {
            // Hedef null ise yeni nesne oluşturulur (AutoMapper davranışı)
            var mapScope = new BuildScope(config, MappingMode.Map, context);
            var create = ExpressionUtil.Coerce(MapRoot(source, destinationType, null, mapScope), destinationType);
            body = Expression.Condition(ExpressionUtil.IsNull(destination), create, body);
        }

        return Expression.Lambda(typeof(Func<,,,>).MakeGenericType(sourceType, destinationType, ContextType, destinationType), body, source, destination, context);
    }

    /// <summary>
    /// <c>ProjectTo</c> için saf (yan etkisiz, çalışma zamanı çağrısı içermeyen) projeksiyon ifadesi üretir.
    /// </summary>
    public static LambdaExpression BuildProjectionLambda(Type sourceType, Type destinationType, MapperConfiguration config, IReadOnlyCollection<string> membersToExpand)
    {
        config.DiagnosticsSink?.Log($"ProjectTo ifadesi üretiliyor: {sourceType.Name} -> {destinationType.Name}", "Debug");
        var source = Expression.Parameter(sourceType, "source");
        var scope = new BuildScope(config, MappingMode.ProjectTo, null) { Expansions = new HashSet<string>(membersToExpand, StringComparer.OrdinalIgnoreCase) };
        var body = ExpressionUtil.Coerce(MapRoot(source, destinationType, null, scope), destinationType);
        return Expression.Lambda(typeof(Func<,>).MakeGenericType(sourceType, destinationType), body, source);
    }

    // ─── Kök ve değer eşleştirme ────────────────────────────────────────────

    private static Expression MapRoot(Expression source, Type destinationType, Expression? existing, BuildScope scope)
    {
        var registration = scope.Config.FindRegistration(source.Type, destinationType);
        var profile = scope.Config.GetProfile(registration);
        var mapped = MapValue(source, destinationType, existing, scope, profile, allowNull: null, inlineTypeMap: true);
        return mapped ?? throw new VeloxConfigurationException(
            $"'{source.Type.FullName}' türü '{destinationType.FullName}' türüne eşlenemiyor. " +
            "Bir CreateMap tanımı veya ConvertUsing ile özel dönüştürücü ekleyin.");
    }

    /// <summary>
    /// Bir değeri hedef türe dönüştüren ifade üretir. Eşlenemiyorsa <c>null</c> döner.
    /// </summary>
    /// <param name="value">Kaynak değer ifadesi.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <param name="existing">Yerinde eşlenecek mevcut hedef değer (yoksa <c>null</c>).</param>
    /// <param name="scope">Üretim kapsamı.</param>
    /// <param name="profile">Geçerli profil ayarları.</param>
    /// <param name="allowNull">Üye düzeyinde AllowNull/DoNotAllowNull tercihi.</param>
    /// <param name="inlineTypeMap">True ise karmaşık tür eşleştirmesi satır içi üretilir (kök çağrı).</param>
    internal static Expression? MapValue(Expression value, Type destinationType, Expression? existing, BuildScope scope, ProfileMap profile, bool? allowNull, bool inlineTypeMap)
    {
        var sourceType = value.Type;
        var config = scope.Config;
        var registration = config.FindRegistration(sourceType, destinationType);

        // 1. Kayıtlı tip dönüştürücü (ConvertUsing) her şeyden önce gelir
        if (registration != null && registration.HasTypeConverter)
        {
            if (inlineTypeMap || scope.IsProjection) return BuildTypeMap(value, registration, destinationType, existing, scope);
            return RuntimeCall(value, destinationType, existing, scope, profile, allowNull);
        }

        // 2. Doğrudan atanabilir değerler (kayıt yoksa; koleksiyonlar her zaman kopyalanır)
        var bothCollections = CollectionExpressionHelper.IsCollectionType(sourceType) && CollectionExpressionHelper.IsCollectionType(destinationType);
        if (registration == null && !bothCollections && destinationType.IsAssignableFrom(sourceType))
        {
            return ExpressionUtil.Coerce(value, destinationType);
        }

        // 3. Sözlük → sözlük
        if (CollectionExpressionHelper.TryGetDictionaryTypes(destinationType, out var destinationKey, out var destinationValue) &&
            CollectionExpressionHelper.TryGetDictionaryTypes(sourceType, out var sourceKey, out var sourceValue) && !scope.IsProjection)
        {
            return BuildDictionary(value, sourceKey, sourceValue, destinationType, destinationKey, destinationValue, existing, scope, profile, allowNull);
        }

        // 4. Koleksiyon → koleksiyon
        if (bothCollections && registration == null)
        {
            return BuildCollection(value, destinationType, existing, scope, profile, allowNull);
        }

        // 5. Nullable sarmalama / açma
        var sourceUnderlying = Nullable.GetUnderlyingType(sourceType);
        var destinationUnderlying = Nullable.GetUnderlyingType(destinationType);
        if (sourceUnderlying != null)
        {
            var inner = MapValue(Expression.Property(value, "Value"), destinationType, null, scope, profile, allowNull, inlineTypeMap: false);
            if (inner == null) return null;
            return Expression.Condition(Expression.Property(value, "HasValue"), ExpressionUtil.Coerce(inner, destinationType), Expression.Default(destinationType));
        }

        if (destinationUnderlying != null)
        {
            var inner = MapValue(value, destinationUnderlying, null, scope, profile, allowNull, inlineTypeMap: false);
            return inner == null ? null : Expression.Convert(inner, destinationType);
        }

        // 6. Enum ve yerleşik dönüşümler
        var builtIn = TryBuiltInConversion(value, destinationType, scope);
        if (builtIn != null) return builtIn;

        // 7. Sözlük → nesne
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(sourceType) && IsComplex(destinationType) && !scope.IsProjection && registration == null)
        {
            return BuildFromDictionary(value, destinationType, scope);
        }

        // 8. Karmaşık tür → karmaşık tür
        if (IsComplex(sourceType) && IsComplex(destinationType) || registration != null)
        {
            if (inlineTypeMap) return BuildTypeMap(value, registration, destinationType, existing, scope);

            if (scope.IsProjection)
            {
                var projected = BuildTypeMap(value, registration, destinationType, null, scope);
                if (projected is ConstantExpression) return projected;
                return ExpressionUtil.CanBeNull(sourceType) && profile.EnableNullPropagationForQueryMapping && value is not ParameterExpression
                    ? Expression.Condition(ExpressionUtil.IsNull(value), Expression.Default(destinationType), projected)
                    : projected;
            }

            if (existing == null && CanInline(sourceType, destinationType, registration, scope))
            {
                return InlineTypeMap(value, registration, destinationType, scope, allowNull ?? profile.AllowNullDestinationValues);
            }

            return RuntimeCall(value, destinationType, existing, scope, profile, allowNull);
        }

        return null;
    }

    private const int MaxInlineDepth = 2;

    /// <summary>
    /// Alt eşleştirmenin üst ifadeye gömülüp gömülemeyeceğini belirler (AutoMapper'ın MaxExecutionPlanDepth yaklaşımı).
    /// Polimorfik, referans korumalı, derinlik sınırlı veya rekürsif eşleştirmeler çalışma zamanı çağrısıyla yapılır.
    /// </summary>
    private static bool CanInline(Type sourceType, Type destinationType, MappingRegistration? registration, BuildScope scope)
    {
        if (scope.Mode != MappingMode.Map || scope.InlineStack.Count >= MaxInlineDepth) return false;
        if (scope.InlineStack.Contains((sourceType, destinationType))) return false;
        if (scope.Config.IsPrecompiled(sourceType, destinationType)) return false;
        if (destinationType.IsAbstract || destinationType.IsInterface) return false;
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(sourceType)) return false;
        return registration == null ||
               (registration.IncludedDerivedTypes.Count == 0 && !registration.PreserveReferences && registration.MaxDepth == 0 &&
                registration.RedirectDestinationType == null && !registration.HasTypeConverter);
    }

    private static Expression InlineTypeMap(Expression value, MappingRegistration? registration, Type destinationType, BuildScope scope, bool allowNullDestination)
    {
        scope.InlineStack.Add((value.Type, destinationType));
        try
        {
            if (!ExpressionUtil.CanBeNull(value.Type)) return BuildTypeMap(value, registration, destinationType, null, scope);

            var variable = Expression.Variable(value.Type, "nested");
            var mapped = BuildTypeMap(variable, registration, destinationType, null, scope);
            var parameterless = destinationType.GetConstructor(Type.EmptyTypes);
            Expression whenNull = !allowNullDestination && parameterless != null ? Expression.New(parameterless) : Expression.Default(destinationType);
            return Expression.Block(destinationType, new[] { variable },
                Expression.Assign(variable, value),
                Expression.Condition(ExpressionUtil.IsNull(variable), whenNull, ExpressionUtil.Coerce(mapped, destinationType)));
        }
        finally
        {
            scope.InlineStack.RemoveAt(scope.InlineStack.Count - 1);
        }
    }

    /// <summary>
    /// İç içe karmaşık türü çalışma zamanı önbelleğinden (aynı bağlamla) eşleyen çağrı.
    /// </summary>
    private static Expression RuntimeCall(Expression value, Type destinationType, Expression? existing, BuildScope scope, ProfileMap profile, bool? allowNull)
    {
        var method = MapNestedMethod.MakeGenericMethod(value.Type, destinationType);
        return Expression.Call(method,
            value,
            existing != null ? ExpressionUtil.Coerce(existing, destinationType) : Expression.Default(destinationType),
            Expression.Constant(existing != null),
            scope.Context!,
            Expression.Constant(allowNull ?? profile.AllowNullDestinationValues));
    }

    // ─── Tür eşleştirmesi (TypeMap) ─────────────────────────────────────────

    private static Expression BuildTypeMap(Expression source, MappingRegistration? registration, Type destinationType, Expression? existing, BuildScope scope)
    {
        var config = scope.Config;

        if (registration?.RedirectDestinationType != null && registration.RedirectDestinationType != destinationType)
        {
            var redirectType = registration.RedirectDestinationType;
            var redirected = config.FindRegistration(source.Type, redirectType);
            var existingRedirect = existing != null ? Expression.TypeAs(existing, redirectType) : null;
            Expression redirectedBody = Expression.Convert(BuildTypeMap(source, redirected, redirectType, existingRedirect, scope), destinationType);
            return scope.IsProjection ? redirectedBody : WrapIncludes(source, registration, destinationType, existing, redirectedBody, scope);
        }

        if (registration != null && registration.HasTypeConverter)
        {
            return BuildConverterCall(source, registration, destinationType, existing, scope);
        }

        if (!IsComplex(destinationType))
        {
            // Kayıt var ama hedef basit tür (ör. CreateMap<int, string>() dönüştürücüsüz): yerleşik dönüşüm
            return TryBuiltInConversion(source, destinationType, scope)
                   ?? throw new VeloxConfigurationException($"'{source.Type.FullName}' → '{destinationType.FullName}' için ConvertUsing tanımlayın.");
        }

        if (scope.IsProjection) return BuildProjectionObject(source, registration, destinationType, scope);

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(source.Type) && existing == null && (registration == null || registration.MemberRules.Count == 0))
        {
            return BuildFromDictionary(source, destinationType, scope);
        }

        var profile = config.GetProfile(registration);
        var hasFactory = registration != null && (registration.FactoryDelegate != null || registration.FactoryDelegateWithContext != null || registration.ConstructUsingServiceLocator);
        Expression body = (destinationType.IsAbstract || destinationType.IsInterface) && existing == null && !hasFactory
            ? Expression.Throw(
                Expression.New(typeof(VeloxMappingException).GetConstructor(new[] { typeof(string) })!,
                    Expression.Call(typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string) })!,
                        Expression.Constant($"'{destinationType.Name}' soyut hedef türü için '"),
                        Expression.Property(Expression.Call(Expression.Convert(source, typeof(object)), typeof(object).GetMethod(nameof(GetType))!), nameof(Type.Name)),
                        Expression.Constant("' kaynağına uygun bir Include<,>() eşleştirmesi bulunamadı."))),
                destinationType)
            : BuildObject(source, registration, destinationType, existing, scope, profile);

        body = WrapIncludes(source, registration, destinationType, existing, body, scope);

        return WrapDepthAndReferences(source, registration, destinationType, body, scope);
    }

    /// <summary>
    /// Polimorfik eşleştirme (Include / IncludeBase / IncludeAllDerived): kaynak çalışma zamanında türetilmiş bir türse
    /// ilgili türetilmiş eşleştirmeye yönlendirir. Yalnızca yeni nesne oluşturulurken uygulanır.
    /// </summary>
    private static Expression WrapIncludes(Expression source, MappingRegistration? registration, Type destinationType, Expression? existing, Expression body, BuildScope scope)
    {
        if (registration == null || registration.IncludedDerivedTypes.Count == 0 || existing != null || scope.Context == null) return body;

        foreach (var (derivedSource, derivedDestination) in registration.IncludedDerivedTypes
                     .Where(t => source.Type.IsAssignableFrom(t.DerivedSource) && destinationType.IsAssignableFrom(t.DerivedDestination) &&
                                 !(t.DerivedSource == source.Type && t.DerivedDestination == destinationType))
                     .OrderBy(t => InheritanceDepth(t.DerivedSource)))
        {
            var derivedCall = Expression.Call(MapNestedMethod.MakeGenericMethod(derivedSource, derivedDestination),
                Expression.Convert(source, derivedSource),
                Expression.Default(derivedDestination),
                Expression.Constant(false),
                scope.Context,
                Expression.Constant(true));
            body = Expression.Condition(Expression.TypeIs(source, derivedSource), Expression.Convert(derivedCall, destinationType), body);
        }

        return body;
    }

    private static Expression WrapDepthAndReferences(Expression source, MappingRegistration? registration, Type destinationType, Expression body, BuildScope scope)
    {
        if (registration == null || scope.Context == null) return body;
        var context = scope.Context;
        var result = Expression.Variable(destinationType, "result");
        var flow = (Expression)Expression.Assign(result, body);

        if (registration.MaxDepth > 0)
        {
            var depth = Expression.Property(context, CurrentDepthProperty);
            flow = Expression.IfThenElse(
                Expression.GreaterThanOrEqual(depth, Expression.Constant(registration.MaxDepth)),
                Expression.Assign(result, Expression.Default(destinationType)),
                Expression.Block(
                    Expression.Assign(depth, Expression.Increment(depth)),
                    Expression.TryFinally(flow, Expression.Assign(depth, Expression.Decrement(depth)))));
        }

        if (registration.PreserveReferences && !source.Type.IsValueType && !destinationType.IsValueType)
        {
            var cache = Expression.Property(context, ReferenceCacheProperty);
            var cached = Expression.Variable(typeof(object), "cached");
            flow = Expression.Block(new[] { cached },
                Expression.IfThenElse(
                    Expression.AndAlso(
                        Expression.NotEqual(cache, Expression.Constant(null, typeof(VeloxReferenceCache))),
                        Expression.Call(cache, CacheTryGetMethod, Expression.Convert(source, typeof(object)), cached)),
                    Expression.Assign(result, Expression.Convert(cached, destinationType)),
                    flow));
        }

        if (flow is BinaryExpression) return body; // ek sarmalama yok
        return Expression.Block(new[] { result }, flow, result);
    }

    /// <summary>
    /// Nesne oluşturur (veya mevcut nesneyi kullanır), BeforeMap → üye atamaları → ForPath → AfterMap sırasını uygular.
    /// </summary>
    private static Expression BuildObject(Expression source, MappingRegistration? registration, Type destinationType, Expression? existing, BuildScope scope, ProfileMap profile)
    {
        var dest = Expression.Variable(destinationType, "dest");
        var statements = new List<Expression>();
        var consumedByConstructor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existing != null)
        {
            statements.Add(Expression.Assign(dest, ExpressionUtil.Coerce(existing, destinationType)));
        }
        else
        {
            statements.Add(Expression.Assign(dest, Construct(source, registration, destinationType, scope, profile, consumedByConstructor)));
        }

        if (registration?.PreserveReferences == true && !source.Type.IsValueType && !destinationType.IsValueType)
        {
            var cache = Expression.Property(scope.Context!, ReferenceCacheProperty);
            statements.Add(Expression.IfThen(Expression.Equal(cache, Expression.Constant(null, typeof(VeloxReferenceCache))),
                Expression.Assign(cache, Expression.New(typeof(VeloxReferenceCache)))));
            statements.Add(Expression.Call(cache, CacheSetMethod, Expression.Convert(source, typeof(object)), Expression.Convert(dest, typeof(object))));
        }

        if (registration != null) statements.AddRange(BuildActions(registration.BeforeMapActions, source, dest, scope));

        var members = TypeMembers.GetDestinationMembers(destinationType, profile)
            .OrderBy(m => registration != null && registration.MemberRules.TryGetValue(m.Name, out var r) ? r.MappingOrder : 0)
            .ToList();

        foreach (var member in members)
        {
            MemberMappingRule? rule = null;
            registration?.MemberRules.TryGetValue(member.Name, out rule);
            if (rule?.IsIgnored == true) continue;
            if (rule == null && profile.IsGloballyIgnored(member.Name)) continue;
            if (consumedByConstructor.Contains(member.Name)) continue;

            var assignment = BuildMemberAssignment(source, dest, member, rule, registration, scope, profile);
            if (assignment != null) statements.Add(assignment);
        }

        if (registration != null)
        {
            foreach (var pathRule in registration.ForPathRules)
            {
                var pathAssignment = BuildForPath(source, dest, pathRule, registration, scope, profile);
                if (pathAssignment != null) statements.Add(pathAssignment);
            }

            statements.AddRange(BuildActions(registration.AfterMapActions, source, dest, scope));
        }

        statements.Add(dest);
        return Expression.Block(new[] { dest }, statements);
    }

    // ─── Nesne oluşturma ────────────────────────────────────────────────────

    private static Expression Construct(Expression source, MappingRegistration? registration, Type destinationType, BuildScope scope, ProfileMap profile, HashSet<string> consumed)
    {
        if (registration?.FactoryDelegateWithContext != null && scope.Context != null)
        {
            return Expression.Convert(ExpressionUtil.InvokeDelegate(registration.FactoryDelegateWithContext, Expression.Convert(source, typeof(object)), scope.Context), destinationType);
        }

        if (registration?.FactoryDelegate != null)
        {
            return Expression.Convert(ExpressionUtil.InvokeDelegate(registration.FactoryDelegate, Expression.Convert(source, typeof(object))), destinationType);
        }

        if (registration?.ConstructUsingServiceLocator == true && scope.Context != null)
        {
            return Expression.Convert(Expression.Call(ResolveServiceMethod, scope.Context, Expression.Constant(destinationType)), destinationType);
        }

        var (ctor, arguments, error) = SelectConstructor(source, registration, destinationType, scope, profile);
        if (ctor == null)
        {
            if (destinationType.IsValueType) return Expression.New(destinationType);
            throw new VeloxAmbiguousConstructorException(destinationType, error!);
        }

        foreach (var parameter in ctor.GetParameters()) consumed.Add(parameter.Name!);
        return Expression.New(ctor, arguments);
    }

    /// <summary>
    /// Kurucu seçimi (AutoMapper ile uyumlu): <c>[VeloxConstructor]</c> varsa o; yoksa tüm parametreleri çözülebilen en
    /// çok parametreli kurucu; hiçbiri çözülemezse parametresiz kurucu.
    /// </summary>
    internal static (ConstructorInfo? Ctor, Expression[] Arguments, string? Error) SelectConstructor(
        Expression source, MappingRegistration? registration, Type destinationType, BuildScope scope, ProfileMap profile)
    {
        if (destinationType.IsAbstract || destinationType.IsInterface)
        {
            return (null, Array.Empty<Expression>(),
                $"'{destinationType.FullName}' soyut bir tür veya arayüz olduğu için örneği oluşturulamaz. " +
                "Include/As ile somut bir tür belirtin veya ConstructUsing kullanın.");
        }

        var ctors = destinationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(profile.ShouldUseConstructor)
            .ToList();

        var marked = ctors.Where(c => c.IsDefined(typeof(VeloxConstructorAttribute), false)).ToList();
        if (marked.Count > 1)
        {
            return (null, Array.Empty<Expression>(), $"'{destinationType.FullName}' türünde birden fazla [VeloxConstructor] kurucusu var; yalnızca birini işaretleyin.");
        }

        if (marked.Count == 1)
        {
            var args = TryResolveConstructorArguments(marked[0], source, registration, scope, profile, allowDefaults: true);
            return (marked[0], args!, null);
        }

        if (!profile.ConstructorMappingEnabled)
        {
            var parameterless = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
            return parameterless != null
                ? (parameterless, Array.Empty<Expression>(), null)
                : (null, Array.Empty<Expression>(), $"'{destinationType.FullName}' türünün parametresiz kurucusu yok ve kurucu eşleştirmesi kapalı (DisableConstructorMapping).");
        }

        foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
        {
            var args = TryResolveConstructorArguments(ctor, source, registration, scope, profile, allowDefaults: false);
            if (args != null) return (ctor, args, null);
        }

        var signatures = string.Join(", ", ctors.Select(c => "(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"));
        return (null, Array.Empty<Expression>(),
            $"'{destinationType.FullName}' için parametreleri kaynak '{source.Type.FullName}' türünden çözülebilen bir kurucu bulunamadı. " +
            $"Kurucular: {(signatures.Length == 0 ? "yok" : signatures)}. ForCtorParam, ConstructUsing veya parametresiz kurucu kullanın.");
    }

    private static Expression[]? TryResolveConstructorArguments(ConstructorInfo ctor, Expression source, MappingRegistration? registration, BuildScope scope, ProfileMap profile, bool allowDefaults)
    {
        var parameters = ctor.GetParameters();
        var arguments = new Expression[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            Expression? raw = null;

            var ctorRule = registration?.CtorParamRules.FirstOrDefault(r => string.Equals(r.ParameterName, parameter.Name, StringComparison.OrdinalIgnoreCase));
            if (ctorRule?.MapFromExpression != null)
            {
                raw = NullSafe(ExpressionUtil.ReplaceParameter(ctorRule.MapFromExpression, source), scope, profile);
            }
            else if (ctorRule?.MapFromFunc != null && scope.Context != null)
            {
                raw = ExpressionUtil.InvokeDelegate(ctorRule.MapFromFunc, source, scope.Context);
            }
            else if (registration != null && TryFindMemberRule(registration, parameter.Name!, out var memberRule) && memberRule.MapFromExpression != null && !memberRule.IsIgnored)
            {
                raw = NullSafe(ExpressionUtil.ReplaceParameter(memberRule.MapFromExpression, source), scope, profile);
            }
            else
            {
                var convention = ConventionResolver.Resolve(source, parameter.Name!, profile, registration, scope.Config);
                if (convention != null) raw = NullSafe(convention, scope, profile);
            }

            Expression? value = raw == null ? null : MapValue(raw, parameter.ParameterType, null, scope, profile, null, inlineTypeMap: false);
            if (value == null)
            {
                if (parameter.HasDefaultValue) value = Expression.Constant(parameter.DefaultValue, parameter.ParameterType);
                else if (allowDefaults) value = Expression.Default(parameter.ParameterType);
                else return null;
            }

            arguments[i] = ExpressionUtil.Coerce(value, parameter.ParameterType);
        }

        return arguments;
    }

    private static bool TryFindMemberRule(MappingRegistration registration, string name, out MemberMappingRule rule)
    {
        foreach (var kvp in registration.MemberRules)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                rule = kvp.Value;
                return true;
            }
        }

        rule = null!;
        return false;
    }

    // ─── Üye ataması ────────────────────────────────────────────────────────

    private static Expression? BuildMemberAssignment(Expression source, Expression dest, MemberInfo member, MemberMappingRule? rule,
        MappingRegistration? registration, BuildScope scope, ProfileMap profile, Expression? target = null)
    {
        var owner = target ?? dest;
        var memberType = TypeMembers.GetMemberType(member);
        var access = Expression.MakeMemberAccess(owner, member);
        var canWrite = TypeMembers.CanWrite(member);

        var raw = ResolveRawValue(source, dest, access, member.Name, rule, registration, scope, profile);
        if (raw == null) return null;

        var context = scope.Context!;
        var rawVariable = Expression.Variable(raw.Type, "src_" + member.Name);
        var statements = new List<Expression>
        {
            Expression.Assign(Expression.Property(context, CurrentMemberProperty), Expression.Constant(member.Name)),
            Expression.Assign(rawVariable, raw)
        };

        // UseDestinationValue: Patch modunda veya setter'ı olmayan koleksiyonlarda mevcut değere eşle
        var useExisting = rule?.UseDestinationValue == true || (rule?.UseDestinationValue != false && (scope.Mode == MappingMode.Patch || !canWrite));
        var existing = useExisting && IsReadable(member) && !memberType.IsValueType ? access : null;

        Expression? mapped;
        if (rule != null && rule.HasNullSubstitute && ExpressionUtil.CanBeNull(raw.Type))
        {
            var converted = MapValue(rawVariable, memberType, existing, scope, profile, rule.AllowNull, inlineTypeMap: false);
            if (converted == null) return null;
            mapped = Expression.Condition(ExpressionUtil.IsNull(rawVariable), SubstituteConstant(rule.NullSubstituteValue, memberType), ExpressionUtil.Coerce(converted, memberType));
        }
        else
        {
            mapped = MapValue(rawVariable, memberType, existing, scope, profile, rule?.AllowNull, inlineTypeMap: false);
            if (mapped == null) return null;
        }

        mapped = ApplyTransformers(ExpressionUtil.Coerce(mapped, memberType), memberType, rule, registration, profile);

        Expression write;
        if (canWrite)
        {
            write = Expression.Assign(access, mapped);
        }
        else if (existing != null)
        {
            // Setter'ı olmayan koleksiyon: mevcut örnek yerinde doldurulur
            write = Expression.IfThen(Expression.NotEqual(access, Expression.Constant(null, memberType)), mapped);
        }
        else
        {
            return null;
        }

        // Condition (AutoMapper: değer çözüldükten sonra, eşleştirmeden önce değerlendirilir)
        if (rule?.ConditionDelegate != null)
        {
            var parameters = rule.ConditionDelegate.GetType().GetMethod("Invoke")!.GetParameters();
            var arguments = new List<Expression> { source, dest };
            if (parameters.Length >= 3) arguments.Add(ConditionValueArgument(rawVariable, mapped, parameters[2].ParameterType));
            if (parameters.Length >= 4) arguments.Add(IsReadable(member) ? (Expression)access : Expression.Default(memberType));
            if (parameters.Length >= 5) arguments.Add(context);
            write = Expression.IfThen(ExpressionUtil.InvokeDelegate(rule.ConditionDelegate, arguments.ToArray()), write);
        }

        // PatchMapping.IgnoreNullValues (VeloxMapper seçeneği): mevcut nesneye eşlemede null kaynak değerleri atla
        if (scope.Mode == MappingMode.Patch && scope.Config.IgnoreNullValues && ExpressionUtil.CanBeNull(raw.Type) && rule?.HasNullSubstitute != true)
        {
            write = Expression.IfThen(Expression.Not(ExpressionUtil.IsNull(rawVariable)), write);
        }

        statements.Add(write);
        Expression block = Expression.Block(new[] { rawVariable }, statements);

        // PreCondition: değer hiç çözülmeden önce
        if (rule?.PreConditionDelegate != null)
        {
            var parameters = rule.PreConditionDelegate.GetType().GetMethod("Invoke")!.GetParameters();
            var arguments = parameters.Length switch
            {
                1 => new[] { source },
                2 => new[] { source, (Expression)context },
                _ => new[] { source, dest, (Expression)context }
            };
            block = Expression.IfThen(ExpressionUtil.InvokeDelegate(rule.PreConditionDelegate, arguments), block);
        }

        return block;
    }

    /// <summary>
    /// Koşul delegesinin üçüncü parametresi için değer: tür uyuyorsa çözülen kaynak değer, aksi halde eşlenmiş değer.
    /// </summary>
    private static Expression ConditionValueArgument(Expression raw, Expression mapped, Type parameterType)
    {
        if (parameterType == typeof(object)) return Expression.Convert(raw, typeof(object));
        if (parameterType.IsAssignableFrom(raw.Type)) return ExpressionUtil.Coerce(raw, parameterType);
        return ExpressionUtil.Coerce(mapped, parameterType);
    }

    /// <summary>
    /// Kural (resolver, converter, MapFrom) veya konvansiyonla hedef üye için ham kaynak değeri çözer.
    /// </summary>
    private static Expression? ResolveRawValue(Expression source, Expression dest, Expression destinationAccess, string memberName,
        MemberMappingRule? rule, MappingRegistration? registration, BuildScope scope, ProfileMap profile)
    {
        var context = scope.Context;
        var currentValue = IsReadable(destinationAccess) ? destinationAccess : Expression.Default(destinationAccess.Type);

        if (rule != null && !scope.IsProjection && context != null)
        {
            if (rule.ResolverInstance != null || rule.ResolverType != null)
            {
                var resolverType = rule.ResolverInstance?.GetType() ?? rule.ResolverType!;
                if (resolverType.IsGenericTypeDefinition) resolverType = CloseGeneric(resolverType, source.Type, dest.Type);
                var iface = ExtensibilityTypes.FindInterface(resolverType, ExtensibilityTypes.ValueResolvers,
                                args => args[0].IsAssignableFrom(source.Type) && args[1].IsAssignableFrom(dest.Type))
                            ?? throw new VeloxConfigurationException($"'{resolverType.FullName}', {source.Type.Name} -> {dest.Type.Name} için uygun bir IValueResolver<,,> uygulamıyor.");
                var instance = ServiceInstance(rule.ResolverInstance, resolverType, iface, context);
                var args = iface.GetGenericArguments();
                return Expression.Call(instance, iface.GetMethod("Resolve")!,
                    ExpressionUtil.Coerce(source, args[0]), ExpressionUtil.Coerce(dest, args[1]), CoerceValue(currentValue, args[2]), context);
            }

            if (rule.MemberValueResolverInstance != null || rule.MemberValueResolverType != null)
            {
                var resolverType = rule.MemberValueResolverInstance?.GetType() ?? rule.MemberValueResolverType!;
                var iface = ExtensibilityTypes.FindInterface(resolverType, ExtensibilityTypes.MemberValueResolvers,
                                args => args[0].IsAssignableFrom(source.Type) && args[1].IsAssignableFrom(dest.Type))
                            ?? throw new VeloxConfigurationException($"'{resolverType.FullName}', {source.Type.Name} -> {dest.Type.Name} için uygun bir IMemberValueResolver<,,,> uygulamıyor.");
                var instance = ServiceInstance(rule.MemberValueResolverInstance, resolverType, iface, context);
                var args = iface.GetGenericArguments();
                var sourceMember = rule.SourceMemberForResolver != null
                    ? NullSafe(ExpressionUtil.ReplaceParameter(rule.SourceMemberForResolver, source), scope, profile)
                    : ConventionResolver.Resolve(source, memberName, profile, registration, scope.Config) ?? Expression.Default(args[2]);
                var sourceMemberValue = MapValue(sourceMember, args[2], null, scope, profile, null, inlineTypeMap: false) ?? CoerceValue(sourceMember, args[2]);
                return Expression.Call(instance, iface.GetMethod("Resolve")!,
                    ExpressionUtil.Coerce(source, args[0]), ExpressionUtil.Coerce(dest, args[1]), ExpressionUtil.Coerce(sourceMemberValue, args[2]), CoerceValue(currentValue, args[3]), context);
            }

            if (rule.HasValueConverter)
            {
                var converterType = rule.ValueConverter?.GetType() ?? rule.ValueConverterType!;
                var sourceMember = rule.ValueConverterSourceMember != null
                    ? NullSafe(ExpressionUtil.ReplaceParameter(rule.ValueConverterSourceMember, source), scope, profile)
                    : ConventionResolver.Resolve(source, memberName, profile, registration, scope.Config);
                if (sourceMember == null)
                    throw new VeloxConfigurationException($"'{memberName}' için value converter kaynağı bulunamadı; ConvertUsing(converter, src => src.Member) kullanın.");
                sourceMember = NullSafe(sourceMember, scope, profile);

                var iface = ExtensibilityTypes.FindInterface(converterType, ExtensibilityTypes.ValueConverters, args => args[0].IsAssignableFrom(sourceMember.Type))
                            ?? ExtensibilityTypes.FindInterface(converterType, ExtensibilityTypes.ValueConverters)
                            ?? throw new VeloxConfigurationException($"'{converterType.FullName}' bir IValueConverter<,> uygulamıyor.");
                var instance = ServiceInstance(rule.ValueConverter, converterType, iface, context);
                var args = iface.GetGenericArguments();
                var input = MapValue(sourceMember, args[0], null, scope, profile, null, inlineTypeMap: false) ?? CoerceValue(sourceMember, args[0]);
                return Expression.Call(instance, iface.GetMethod("Convert")!, ExpressionUtil.Coerce(input, args[0]), context);
            }

            if (rule.MapFromFunc != null)
            {
                return rule.MapFromFuncArity switch
                {
                    2 => ExpressionUtil.InvokeDelegate(rule.MapFromFunc, source, dest),
                    3 => ExpressionUtil.InvokeDelegate(rule.MapFromFunc, source, dest, currentValue),
                    _ => ExpressionUtil.InvokeDelegate(rule.MapFromFunc, source, dest, currentValue, context)
                };
            }
        }

        if (rule?.MapFromExpression != null)
        {
            return NullSafe(ExpressionUtil.ReplaceParameter(rule.MapFromExpression, source), scope, profile);
        }

        if (rule != null && rule.HasValueSource && scope.IsProjection)
        {
            // Resolver/converter/fonksiyon kuralları sorguya çevrilemez; ProjectTo'da üye atlanır
            scope.Config.DiagnosticsSink?.Log($"ProjectTo: '{memberName}' üyesi özel resolver/converter kullandığı için projeksiyona dahil edilmedi.", "Warning");
            return null;
        }

        var convention = ConventionResolver.Resolve(source, memberName, profile, registration, scope.Config);
        return convention == null ? null : NullSafe(convention, scope, profile);
    }

    private static Expression ServiceInstance(object? instance, Type implementationType, Type interfaceType, ParameterExpression context)
    {
        if (instance != null) return Expression.Constant(instance, interfaceType);
        return Expression.Convert(Expression.Call(ResolveServiceMethod, context, Expression.Constant(implementationType)), interfaceType);
    }

    private static Type CloseGeneric(Type openType, Type sourceType, Type destinationType)
    {
        var args = sourceType.GetGenericArguments().Concat(destinationType.GetGenericArguments()).ToArray();
        var arity = openType.GetGenericArguments().Length;
        if (args.Length == arity) return openType.MakeGenericType(args);
        if (sourceType.GetGenericArguments().Length == arity) return openType.MakeGenericType(sourceType.GetGenericArguments());
        if (destinationType.GetGenericArguments().Length == arity) return openType.MakeGenericType(destinationType.GetGenericArguments());
        throw new VeloxConfigurationException($"'{openType.FullName}' open generic türü {sourceType.Name} -> {destinationType.Name} için kapatılamadı.");
    }

    private static Expression CoerceValue(Expression value, Type type)
    {
        if (type.IsAssignableFrom(value.Type)) return ExpressionUtil.Coerce(value, type);
        try
        {
            return Expression.Convert(value, type);
        }
        catch (InvalidOperationException)
        {
            return Expression.Default(type);
        }
    }

    private static Expression NullSafe(Expression expression, BuildScope scope, ProfileMap profile)
    {
        if (scope.IsProjection) return profile.EnableNullPropagationForQueryMapping ? NullSafetyVisitor.Apply(expression, forProjection: true) : expression;
        return NullSafetyVisitor.Apply(expression);
    }

    private static Expression SubstituteConstant(object? value, Type memberType)
    {
        if (value == null) return Expression.Default(memberType);
        if (memberType.IsInstanceOfType(value)) return Expression.Constant(value, memberType);

        var target = Nullable.GetUnderlyingType(memberType) ?? memberType;
        try
        {
            object converted = target.IsEnum
                ? (value is string s ? Enum.Parse(target, s, true) : Enum.ToObject(target, value))
                : target == typeof(string) ? value.ToString()! : System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
            return Expression.Convert(Expression.Constant(converted, target), memberType);
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            throw new VeloxConfigurationException($"NullSubstitute değeri ('{value}') '{memberType.Name}' türüne dönüştürülemiyor.");
        }
    }

    private static Expression ApplyTransformers(Expression value, Type memberType, MemberMappingRule? rule, MappingRegistration? registration, ProfileMap profile, bool forProjection = false)
    {
        IEnumerable<LambdaExpression> transformers = Enumerable.Empty<LambdaExpression>();
        if (rule != null) transformers = transformers.Concat(rule.Transformers.Where(t => t.Parameters[0].Type.IsAssignableFrom(memberType)));
        if (registration != null) transformers = transformers.Concat(registration.ValueTransformers.Where(t => t.Parameters[0].Type.IsAssignableFrom(memberType)));
        if (profile.ProfileTransformers != null) transformers = transformers.Concat(profile.ProfileTransformers.GetTransformers(memberType));
        if (profile.GlobalTransformers != null) transformers = transformers.Concat(profile.GlobalTransformers.GetTransformers(memberType));

        foreach (var transformer in transformers)
        {
            var parameterType = transformer.Parameters[0].Type;
            var input = ExpressionUtil.Coerce(value, parameterType);
            Expression applied = ExpressionUtil.ReplaceParameter(transformer, input);

            // Null değerler transformer'a gönderilmez (ör. string.Trim null'da hata vermesin)
            if (ExpressionUtil.CanBeNull(parameterType) && forProjection)
            {
                // Sorgularda BlockExpression çevrilemez: değişken yerine koşullu ifade
                applied = Expression.Condition(ExpressionUtil.IsNull(input), input, applied);
            }
            else if (ExpressionUtil.CanBeNull(parameterType))
            {
                var variable = Expression.Variable(parameterType, "transformed");
                applied = Expression.Block(new[] { variable },
                    Expression.Assign(variable, input),
                    Expression.Condition(ExpressionUtil.IsNull(variable), variable, ExpressionUtil.ReplaceParameter(transformer, variable)));
            }

            value = ExpressionUtil.Coerce(applied, memberType);
        }

        return value;
    }

    private static Expression? BuildForPath(Expression source, Expression dest, ForPathRule pathRule, MappingRegistration registration, BuildScope scope, ProfileMap profile)
    {
        if (pathRule.MemberRule.IsIgnored) return null;

        var statements = new List<Expression>();
        Expression current = dest;
        for (var i = 0; i < pathRule.PathSegments.Length - 1; i++)
        {
            var member = FindMember(current.Type, pathRule.PathSegments[i])
                ?? throw new VeloxConfigurationException($"ForPath: '{current.Type.FullName}' türünde '{pathRule.PathSegments[i]}' üyesi bulunamadı.");
            var access = Expression.MakeMemberAccess(current, member);
            var memberType = TypeMembers.GetMemberType(member);

            if (!memberType.IsValueType && TypeMembers.CanWrite(member))
            {
                var ctor = memberType.GetConstructor(Type.EmptyTypes);
                if (ctor != null)
                {
                    statements.Add(Expression.IfThen(Expression.Equal(access, Expression.Constant(null, memberType)), Expression.Assign(access, Expression.New(ctor))));
                }
            }

            current = access;
        }

        var leaf = FindMember(current.Type, pathRule.PathSegments[pathRule.PathSegments.Length - 1])
            ?? throw new VeloxConfigurationException($"ForPath: '{current.Type.FullName}' türünde '{pathRule.PathSegments[pathRule.PathSegments.Length - 1]}' üyesi bulunamadı.");
        if (!TypeMembers.CanWrite(leaf))
            throw new VeloxConfigurationException($"ForPath: '{current.Type.FullName}.{leaf.Name}' üyesi yazılabilir değil.");

        var assignment = BuildMemberAssignment(source, dest, leaf, pathRule.MemberRule, registration, scope, profile, current);
        if (assignment == null) return null;
        statements.Add(assignment);
        return Expression.Block(statements);
    }

    private static MemberInfo? FindMember(Type type, string name)
        => (MemberInfo?)type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
           ?? type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static bool IsReadable(MemberInfo member) => member is FieldInfo || (member is PropertyInfo p && p.GetMethod != null);

    private static bool IsReadable(Expression access)
        => access is not MemberExpression memberAccess || IsReadable(memberAccess.Member);

    // ─── BeforeMap / AfterMap ───────────────────────────────────────────────

    private static IEnumerable<Expression> BuildActions(IReadOnlyList<object> actions, Expression source, Expression dest, BuildScope scope)
    {
        var context = scope.Context!;
        foreach (var action in actions)
        {
            if (action is Type actionType)
            {
                var iface = ExtensibilityTypes.FindInterface(actionType, ExtensibilityTypes.MappingActions,
                                args => args[0].IsAssignableFrom(source.Type) && args[1].IsAssignableFrom(dest.Type))
                            ?? throw new VeloxConfigurationException($"'{actionType.FullName}', {source.Type.Name} -> {dest.Type.Name} için uygun bir IMappingAction<,> uygulamıyor.");
                var args = iface.GetGenericArguments();
                var instance = Expression.Convert(Expression.Call(ResolveServiceMethod, context, Expression.Constant(actionType)), iface);
                yield return Expression.Call(instance, iface.GetMethod("Process")!, ExpressionUtil.Coerce(source, args[0]), ExpressionUtil.Coerce(dest, args[1]), context);
            }
            else if (action is Delegate inline)
            {
                yield return ExpressionUtil.Arity(inline) >= 3
                    ? ExpressionUtil.InvokeDelegate(inline, source, dest, context)
                    : ExpressionUtil.InvokeDelegate(inline, source, dest);
            }
        }
    }

    // ─── Tip dönüştürücüler ─────────────────────────────────────────────────

    private static Expression BuildConverterCall(Expression source, MappingRegistration registration, Type destinationType, Expression? existing, BuildScope scope)
    {
        var context = scope.Context;
        var existingValue = existing != null ? ExpressionUtil.Coerce(existing, destinationType) : Expression.Default(destinationType);

        if (registration.ConvertUsingExpression != null)
        {
            return ExpressionUtil.Coerce(ExpressionUtil.ReplaceParameter(registration.ConvertUsingExpression, source), destinationType);
        }

        if (scope.IsProjection || context == null)
        {
            throw new VeloxProjectionException(
                $"{source.Type.Name} -> {destinationType.Name}: ProjectTo yalnızca ifade tabanlı ConvertUsing(src => ...) dönüştürücülerini destekler. " +
                "ITypeConverter veya fonksiyon tabanlı dönüştürücüler sorguya çevrilemez.");
        }

        if (registration.ConvertUsingFunc != null)
        {
            var func = registration.ConvertUsingFunc;
            var call = ExpressionUtil.Arity(func) >= 3
                ? ExpressionUtil.InvokeDelegate(func, source, existingValue, context)
                : ExpressionUtil.InvokeDelegate(func, source, existingValue);
            return ExpressionUtil.Coerce(call, destinationType);
        }

        var converterType = registration.CustomConverter?.GetType() ?? registration.CustomConverterType!;
        if (converterType.IsGenericTypeDefinition) converterType = CloseGeneric(converterType, source.Type, destinationType);

        var typeConverter = ExtensibilityTypes.FindInterface(converterType, new[] { typeof(ITypeConverter<,>) },
            args => args[0].IsAssignableFrom(source.Type) && args[1].IsAssignableFrom(destinationType) || args[1] == destinationType);
        if (typeConverter != null)
        {
            var args = typeConverter.GetGenericArguments();
            var instance = ServiceInstance(registration.CustomConverter, converterType, typeConverter, context);
            var call = Expression.Call(instance, typeConverter.GetMethod("Convert")!,
                ExpressionUtil.Coerce(source, args[0]), CoerceValue(existingValue, args[1]), context);
            return ExpressionUtil.Coerce(call, destinationType);
        }

        var veloxConverter = ExtensibilityTypes.FindInterface(converterType, new[] { typeof(IVeloxTypeConverter<,>) })
            ?? throw new VeloxConfigurationException($"'{converterType.FullName}' bir ITypeConverter<,> uygulamıyor.");
        var veloxArgs = veloxConverter.GetGenericArguments();
        var veloxInstance = ServiceInstance(registration.CustomConverter, converterType, veloxConverter, context);
        return ExpressionUtil.Coerce(Expression.Call(veloxInstance, veloxConverter.GetMethod("Convert")!, ExpressionUtil.Coerce(source, veloxArgs[0])), destinationType);
    }

    // ─── Koleksiyonlar ──────────────────────────────────────────────────────

    private static Expression? BuildCollection(Expression value, Type destinationType, Expression? existing, BuildScope scope, ProfileMap profile, bool? allowNull)
    {
        var sourceElement = CollectionExpressionHelper.GetCollectionElementType(value.Type)!;
        var destinationElement = CollectionExpressionHelper.GetCollectionElementType(destinationType)!;

        var element = Expression.Parameter(sourceElement, "item");
        var elementBody = MapValue(element, destinationElement, null, scope, profile, null, inlineTypeMap: false);
        if (elementBody == null) return null;

        if (scope.IsProjection)
        {
            var projectionMap = Expression.Lambda(ExpressionUtil.Coerce(elementBody, destinationElement), element);
            return CollectionExpressionHelper.BuildNewCollection(value, sourceElement, destinationElement, destinationType, projectionMap, forProjection: true)
                   ?? throw new VeloxConfigurationException($"'{destinationType.FullName}' koleksiyon türü projeksiyonda oluşturulamıyor; List<T>, T[] veya IEnumerable<T> kullanın.");
        }

        // Kaynak bir kez değerlendirilir (null kontrolü, sayım ve döngü aynı değişkeni kullanır)
        var source = Expression.Variable(value.Type, "sourceCollection");
        var created = CollectionExpressionHelper.BuildLoopCollection(source, sourceElement, destinationElement, destinationType,
                          item => ExpressionUtil.Coerce(MapValue(item, destinationElement, null, scope, profile, null, inlineTypeMap: false)!, destinationElement))
                      ?? CollectionExpressionHelper.BuildNewCollection(source, sourceElement, destinationElement, destinationType,
                          Expression.Lambda(ExpressionUtil.Coerce(elementBody, destinationElement), element), forProjection: false)
                      ?? throw new VeloxConfigurationException($"'{destinationType.FullName}' koleksiyon türü oluşturulamıyor; List<T>, T[], HashSet<T> veya parametresiz kurucusu olan bir ICollection<T> kullanın.");

        Expression mapped = existing != null && (TypeMembers.IsMutableCollectionType(destinationType) || destinationType.IsInterface)
            ? CollectionExpressionHelper.BuildFillExisting(existing, source, sourceElement, destinationElement,
                Expression.Lambda(ExpressionUtil.Coerce(elementBody, destinationElement), element), ExpressionUtil.Coerce(created, existing.Type))
            : created;

        var allowNullCollections = allowNull ?? profile.AllowNullCollections;
        Expression whenNull;
        if (allowNullCollections)
        {
            whenNull = Expression.Constant(null, destinationType);
        }
        else
        {
            var empty = CollectionExpressionHelper.CreateEmptyCollection(destinationType);
            whenNull = empty == null
                ? Expression.Constant(null, destinationType)
                : NewEmptyCollection(destinationType, destinationElement);
        }

        return Expression.Block(destinationType, new[] { source },
            Expression.Assign(source, value),
            Expression.Condition(ExpressionUtil.IsNull(source), whenNull, ExpressionUtil.Coerce(mapped, destinationType)));
    }

    private static Expression NewEmptyCollection(Type destinationType, Type elementType)
    {
        if (destinationType.IsArray) return Expression.NewArrayBounds(elementType, Expression.Constant(0));
        var listType = typeof(List<>).MakeGenericType(elementType);
        if (destinationType.IsAssignableFrom(listType)) return Expression.Convert(Expression.New(listType), destinationType);
        var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
        if (destinationType.IsAssignableFrom(hashSetType)) return Expression.Convert(Expression.New(hashSetType), destinationType);
        return Expression.New(destinationType);
    }

    private static Expression? BuildDictionary(Expression value, Type sourceKey, Type sourceValue, Type destinationType, Type destinationKey, Type destinationValue,
        Expression? existing, BuildScope scope, ProfileMap profile, bool? allowNull)
    {
        var keyParameter = Expression.Parameter(sourceKey, "key");
        var valueParameter = Expression.Parameter(sourceValue, "value");
        var keyBody = MapValue(keyParameter, destinationKey, null, scope, profile, null, inlineTypeMap: false);
        var valueBody = MapValue(valueParameter, destinationValue, null, scope, profile, null, inlineTypeMap: false);
        if (keyBody == null || valueBody == null) return null;

        var built = CollectionExpressionHelper.BuildDictionary(value, sourceKey, sourceValue, destinationType, destinationKey, destinationValue,
            Expression.Lambda(ExpressionUtil.Coerce(keyBody, destinationKey), keyParameter),
            Expression.Lambda(ExpressionUtil.Coerce(valueBody, destinationValue), valueParameter),
            existing);
        if (built == null) return null;

        var whenNull = (allowNull ?? profile.AllowNullCollections) || CollectionExpressionHelper.CreateEmptyCollection(destinationType) == null
            ? (Expression)Expression.Constant(null, destinationType)
            : ExpressionUtil.Coerce(Expression.New(destinationType.IsInterface ? typeof(Dictionary<,>).MakeGenericType(destinationKey, destinationValue) : destinationType), destinationType);
        return Expression.Condition(ExpressionUtil.IsNull(value), whenNull, built);
    }

    // ─── Yerleşik dönüşümler ────────────────────────────────────────────────

    /// <summary>
    /// Enum, string ve sayısal türler arası yerleşik dönüşümler (AutoMapper'ın yerleşik mapper'ları ile uyumlu).
    /// </summary>
    private static Expression? TryBuiltInConversion(Expression value, Type destinationType, BuildScope scope)
    {
        var sourceType = value.Type;
        if (destinationType.IsAssignableFrom(sourceType)) return ExpressionUtil.Coerce(value, destinationType);

        // Enum → enum: isme göre, bulunamazsa değere göre (ProjectTo: değere göre)
        if (sourceType.IsEnum && destinationType.IsEnum)
        {
            if (scope.IsProjection) return Expression.Convert(Expression.Convert(value, Enum.GetUnderlyingType(sourceType)), destinationType);
            var method = typeof(EnumConverter<,>).MakeGenericType(sourceType, destinationType).GetMethod("Convert", BindingFlags.NonPublic | BindingFlags.Static)!;
            return Expression.Call(method, value);
        }

        // Enum ↔ tamsayı
        if (sourceType.IsEnum && IsIntegral(destinationType)) return Expression.Convert(value, destinationType);
        if (destinationType.IsEnum && IsIntegral(sourceType)) return Expression.Convert(value, destinationType);

        // string → enum
        if (sourceType == typeof(string) && destinationType.IsEnum)
        {
            if (scope.IsProjection) return null;
            var parse = typeof(EnumParser<>).MakeGenericType(destinationType).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
            return Expression.Call(parse, value);
        }

        // Herhangi bir tür → string (ToString)
        if (destinationType == typeof(string))
        {
            Expression call = Expression.Call(value, sourceType.GetMethod(nameof(ToString), Type.EmptyTypes) ?? ObjectToStringMethod);
            return sourceType.IsValueType ? call : Expression.Condition(ExpressionUtil.IsNull(value), Expression.Constant(null, typeof(string)), call);
        }

        if (scope.IsProjection)
        {
            // Sorgularda yalnızca SQL'e çevrilebilen sayısal dönüşümler
            return IsNumeric(sourceType) && IsNumeric(destinationType) ? Expression.Convert(value, destinationType) : TryConversionOperator(value, destinationType);
        }

        // string → değer türleri
        if (sourceType == typeof(string))
        {
            if (destinationType == typeof(Guid)) return Expression.Call(StringParsers.ParseGuidMethod, value);
            if (destinationType == typeof(TimeSpan)) return Expression.Call(StringParsers.ParseTimeSpanMethod, value);
            if (destinationType == typeof(DateTimeOffset)) return Expression.Call(StringParsers.ParseDateTimeOffsetMethod, value);
            if (IsConvertible(destinationType)) return Expression.Call(StringParsers.ChangeTypeMethod.MakeGenericMethod(destinationType), value);
        }

        // IConvertible türler arası (System.Convert.ToXxx — AutoMapper ConvertMapper ile aynı)
        if (IsConvertible(sourceType) && IsConvertible(destinationType))
        {
            var convert = typeof(Convert).GetMethod("To" + destinationType.Name, new[] { sourceType });
            if (convert != null) return Expression.Call(convert, value);
        }

        return TryConversionOperator(value, destinationType);
    }

    private static Expression? TryConversionOperator(Expression value, Type destinationType)
    {
        var sourceType = value.Type;
        var method = FindConversionOperator(sourceType, sourceType, destinationType) ?? FindConversionOperator(destinationType, sourceType, destinationType);
        return method == null ? null : Expression.Convert(value, destinationType, method);
    }

    private static MethodInfo? FindConversionOperator(Type declaringType, Type sourceType, Type destinationType)
        => declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                                 m.ReturnType == destinationType &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType));

    private static bool IsIntegral(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
           type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

    private static bool IsNumeric(Type type)
        => IsIntegral(type) || type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static bool IsConvertible(Type type)
        => type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(string);

    /// <summary>Tür, üyeleri tek tek eşlenen bir "nesne" türü mü (primitive, string, enum, koleksiyon değil)?</summary>
    internal static bool IsComplex(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null) return false;
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(object)) return false;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan)) return false;
        if (type.FullName == "System.DateOnly" || type.FullName == "System.TimeOnly") return false;
        if (typeof(Delegate).IsAssignableFrom(type)) return false;
        return !CollectionExpressionHelper.IsCollectionType(type);
    }

    // ─── Sözlük → nesne ─────────────────────────────────────────────────────

    private static Expression BuildFromDictionary(Expression value, Type destinationType, BuildScope scope)
    {
        var ctor = destinationType.GetConstructor(Type.EmptyTypes)
            ?? throw new VeloxConfigurationException($"Sözlükten '{destinationType.FullName}' türüne eşleme için parametresiz kurucu gereklidir.");
        var profile = scope.Config.DefaultProfile;
        var dest = Expression.Variable(destinationType, "dest");
        var dictionary = Expression.Convert(value, typeof(System.Collections.IDictionary));
        var statements = new List<Expression> { Expression.Assign(dest, Expression.New(ctor)) };

        foreach (var member in TypeMembers.GetDestinationMembers(destinationType, profile))
        {
            if (!TypeMembers.CanWrite(member)) continue;
            var memberType = TypeMembers.GetMemberType(member);
            var found = Expression.Variable(typeof(object), "found");
            var tryGet = Expression.Call(RuntimeServices.DictionaryTryGetMethod, dictionary, Expression.Constant(member.Name), found);

            Expression converted;
            var objectValue = (Expression)found;
            var direct = MapValue(objectValue, memberType, null, scope, profile, null, inlineTypeMap: false);
            if (memberType.IsEnum || IsConvertible(memberType) && memberType != typeof(string))
            {
                converted = Expression.Convert(Expression.Call(RuntimeServices.ConvertObjectMethod, found, Expression.Constant(memberType)), memberType);
            }
            else if (memberType == typeof(string))
            {
                converted = Expression.Condition(Expression.Equal(found, Expression.Constant(null)), Expression.Constant(null, typeof(string)), Expression.Call(found, ObjectToStringMethod));
            }
            else if (IsComplex(memberType))
            {
                converted = Expression.Call(RuntimeServices.MapObjectMethod.MakeGenericMethod(memberType), scope.Context!, found);
            }
            else
            {
                converted = direct != null ? ExpressionUtil.Coerce(direct, memberType) : Expression.Convert(Expression.Call(RuntimeServices.ConvertObjectMethod, found, Expression.Constant(memberType)), memberType);
            }

            statements.Add(Expression.Block(new[] { found },
                Expression.IfThen(tryGet, Expression.Assign(Expression.MakeMemberAccess(dest, member), converted))));
        }

        statements.Add(dest);
        return Expression.Block(new[] { dest }, statements);
    }

    // ─── Projeksiyon ────────────────────────────────────────────────────────

    private static Expression BuildProjectionObject(Expression source, MappingRegistration? registration, Type destinationType, BuildScope scope)
    {
        var key = (source.Type, destinationType);
        var occurrences = scope.InlineStack.Count(k => k == key);
        if (occurrences > 0)
        {
            var maxDepth = registration?.MaxDepth ?? 0;
            if (maxDepth == 0)
            {
                throw new VeloxProjectionException(
                    $"{source.Type.Name} -> {destinationType.Name} projeksiyonu kendini tekrar eden (rekürsif) bir model içeriyor. " +
                    "Sonsuz sorgu üretimini önlemek için CreateMap(...).MaxDepth(n) tanımlayın.");
            }

            if (occurrences >= maxDepth) return Expression.Constant(null, destinationType);
        }

        scope.InlineStack.Add(key);
        try
        {
            var profile = scope.Config.GetProfile(registration);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NewExpression newExpression;
            var (ctor, arguments, error) = SelectConstructor(source, registration, destinationType, scope, profile);
            if (ctor == null)
            {
                if (!destinationType.IsValueType) throw new VeloxProjectionException(error!);
                newExpression = Expression.New(destinationType);
            }
            else
            {
                newExpression = Expression.New(ctor, arguments);
                foreach (var parameter in ctor.GetParameters()) consumed.Add(parameter.Name!);
            }

            var bindings = new List<MemberBinding>();
            foreach (var member in TypeMembers.GetDestinationMembers(destinationType, profile))
            {
                if (!TypeMembers.CanWrite(member) || consumed.Contains(member.Name)) continue;

                MemberMappingRule? rule = null;
                registration?.MemberRules.TryGetValue(member.Name, out rule);
                if (rule?.IsIgnored == true || (rule == null && profile.IsGloballyIgnored(member.Name))) continue;

                var path = scope.Path.Length == 0 ? member.Name : scope.Path + "." + member.Name;
                if (rule?.ExplicitExpansion == true && !scope.IsExpanded(path)) continue;

                var raw = ResolveRawValue(source, source, Expression.Default(TypeMembers.GetMemberType(member)), member.Name, rule, registration, scope, profile);
                if (raw == null) continue;

                var memberType = TypeMembers.GetMemberType(member);
                var previousPath = scope.Path;
                scope.Path = path;
                Expression? mapped;
                try
                {
                    mapped = MapValue(raw, memberType, null, scope, profile, rule?.AllowNull, inlineTypeMap: false);
                }
                finally
                {
                    scope.Path = previousPath;
                }

                if (mapped == null) continue;
                if (rule != null && rule.HasNullSubstitute && ExpressionUtil.CanBeNull(raw.Type))
                {
                    mapped = Expression.Condition(ExpressionUtil.IsNull(raw), SubstituteConstant(rule.NullSubstituteValue, memberType), ExpressionUtil.Coerce(mapped, memberType));
                }

                mapped = ApplyTransformers(ExpressionUtil.Coerce(mapped, memberType), memberType, rule, registration, profile, forProjection: true);
                bindings.Add(Expression.Bind(member, mapped));
            }

            return bindings.Count > 0 ? Expression.MemberInit(newExpression, bindings) : newExpression;
        }
        finally
        {
            scope.InlineStack.RemoveAt(scope.InlineStack.Count - 1);
        }
    }

    private static int InheritanceDepth(Type type)
    {
        var depth = 0;
        for (var current = type.BaseType; current != null; current = current.BaseType) depth++;
        return depth;
    }
}

/// <summary>
/// Tek bir ifade üretim çağrısının durumu.
/// </summary>
internal sealed class BuildScope
{
    public BuildScope(MapperConfiguration config, MappingMode mode, ParameterExpression? context)
    {
        Config = config;
        Mode = mode;
        Context = context;
    }

    public MapperConfiguration Config { get; }
    public MappingMode Mode { get; }
    public ParameterExpression? Context { get; }
    public bool IsProjection => Mode == MappingMode.ProjectTo;
    public HashSet<string>? Expansions { get; set; }
    public List<(Type, Type)> InlineStack { get; } = new();
    public string Path { get; set; } = string.Empty;

    /// <summary>Verilen üye yolu (veya alt yolları) <c>membersToExpand</c> ile istendiyse <c>true</c>.</summary>
    public bool IsExpanded(string path)
    {
        if (Expansions == null || Expansions.Count == 0) return false;
        foreach (var expansion in Expansions)
        {
            if (string.Equals(expansion, path, StringComparison.OrdinalIgnoreCase) ||
                expansion.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}

/// <summary>
/// Üretilen ifadelerin çalışma zamanında çağırdığı yardımcılar.
/// </summary>
internal static class RuntimeServices
{
    internal static readonly MethodInfo DictionaryTryGetMethod = typeof(RuntimeServices).GetMethod(nameof(TryGetCaseInsensitive), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo ConvertObjectMethod = typeof(RuntimeServices).GetMethod(nameof(ConvertObject), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo MapObjectMethod = typeof(RuntimeServices).GetMethod(nameof(MapObject), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Resolver/converter/action örneğini çözer: <c>ConstructServicesUsing</c> → DI konteyneri → (DI varsa) ActivatorUtilities → parametresiz kurucu.
    /// </summary>
    public static object Resolve(VeloxResolutionContext context, Type type)
    {
        var service = context.GetServiceOrNull(type);
        if (service != null) return service;

        if (context.ServiceProvider != null)
        {
            return Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(context.ServiceProvider, type);
        }

        try
        {
            return Activator.CreateInstance(type)!;
        }
        catch (MissingMethodException ex)
        {
            throw new VeloxMappingException(
                $"'{type.FullName}' örneği oluşturulamadı: parametresiz kurucu yok. Türü DI konteynerine kaydedin " +
                "(AddVeloxMapper ile taranan assembly'lerdeki resolver/converter'lar otomatik kaydedilir) veya ConstructServicesUsing kullanın.", ex);
        }
    }

    private static bool TryGetCaseInsensitive(System.Collections.IDictionary dictionary, string key, out object? value)
    {
        if (dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object? ConvertObject(object? value, Type type)
    {
        if (value == null) return type.IsValueType ? Activator.CreateInstance(type) : null;
        if (type.IsInstanceOfType(value)) return value;
        if (type.IsEnum) return value is string s ? Enum.Parse(type, s, true) : Enum.ToObject(type, value);
        return System.Convert.ChangeType(value, type, System.Globalization.CultureInfo.CurrentCulture);
    }

    private static T MapObject<T>(VeloxResolutionContext context, object? value)
        => value == null ? default! : value is T typed ? typed : ((Mapper)context.Mapper).MapRuntime<T>(value, context);
}
