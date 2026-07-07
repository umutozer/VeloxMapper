using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Caching;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;
using VeloxMapper.Attributes;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Execution;

/// <summary>
/// Source Generator (Layer 1) üretimi olmayan türler için çalışma zamanında (Layer 2)
/// Expression Tree tabanlı deterministik eşleştirme kodu üreten sınıf.
/// Üretilen delegate'ler ConcurrentDictionary ile önbelleklenir ve strongly-typed çağrılır.
/// </summary>
internal static class ExpressionBuilder
{
    /// <summary>
    /// DI üzerinden resolver veya converter tiplerini çözümlemek için kullanılan yardımcı metot.
    /// </summary>
    public static object ResolveService(VeloxResolutionContext context, Type serviceType)
    {
        if (context.ServiceProvider != null)
        {
            var service = context.ServiceProvider.GetService(serviceType);
            if (service != null) return service;
        }
        return Activator.CreateInstance(serviceType)
            ?? throw new InvalidOperationException($"'{serviceType.FullName}' türünde bir servis veya nesne oluşturulamadı.");
    }

    /// <summary>
    /// Map işlemi için <c>Func&lt;object, object&gt;</c> veya <c>Func&lt;object, VeloxResolutionContext, object&gt;</c> tipinde bir delegate üretir.
    /// </summary>
    public static Delegate BuildMapDelegate(Type source, Type destination, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Building map delegate for {source.Name} -> {destination.Name}", "Information");
        var registration = config.GetRegistration(source, destination);
        bool requiresContext = registration?.RequiresContext == true;

        var param = Expression.Parameter(typeof(object), "source");
        var castedSource = Expression.Convert(param, source);

        if (requiresContext)
        {
            var contextParam = Expression.Parameter(typeof(VeloxResolutionContext), "context");
            var body = BuildMapBody(castedSource, source, destination, config, contextParam, MappingMode.Map);
            var boxedBody = Expression.Convert(body, typeof(object));
            var lambda = Expression.Lambda<Func<object, VeloxResolutionContext, object>>(boxedBody, param, contextParam);
            try
            {
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new VeloxMappingException($"Expression compilation failed for {source.Name} -> {destination.Name}. Expression: {lambda}", ex);
            }
        }
        else
        {
            var body = BuildMapBody(castedSource, source, destination, config, null, MappingMode.Map);
            var boxedBody = Expression.Convert(body, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(boxedBody, param);
            try
            {
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new VeloxMappingException($"Expression compilation failed for {source.Name} -> {destination.Name}. Expression: {lambda}", ex);
            }
        }
    }

    /// <summary>
    /// Strongly-typed Map için <c>Func&lt;TSource, TDest&gt;</c> veya <c>Func&lt;TSource, VeloxResolutionContext, TDest&gt;</c> tipinde bir delege üretir.
    /// </summary>
    public static Delegate BuildStronglyTypedMapDelegate(Type source, Type destination, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Building strongly typed map delegate for {source.Name} -> {destination.Name}", "Information");
        var registration = config.GetRegistration(source, destination);
        bool requiresContext = registration?.RequiresContext == true;

        var param = Expression.Parameter(source, "source");

        if (requiresContext)
        {
            var contextParam = Expression.Parameter(typeof(VeloxResolutionContext), "context");
            var body = BuildMapBody(param, source, destination, config, contextParam, MappingMode.Map);

            var funcType = typeof(Func<,,>).MakeGenericType(source, typeof(VeloxResolutionContext), destination);
            var lambda = Expression.Lambda(funcType, body, param, contextParam);
            try
            {
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new VeloxMappingException($"Expression compilation failed for {source.Name} -> {destination.Name}. Expression: {lambda}", ex);
            }
        }
        else
        {
            var body = BuildMapBody(param, source, destination, config, null, MappingMode.Map);

            var funcType = typeof(Func<,>).MakeGenericType(source, destination);
            var lambda = Expression.Lambda(funcType, body, param);
            try
            {
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new VeloxMappingException($"Expression compilation failed for {source.Name} -> {destination.Name}. Expression: {lambda}", ex);
            }
        }
    }

    /// <summary>
    /// Patch işlemi için <c>Action&lt;TSource, TDestination&gt;</c> veya <c>Action&lt;TSource, TDestination, VeloxResolutionContext&gt;</c> tipinde bir delege üretir.
    /// </summary>
    public static Delegate BuildPatchDelegate(Type source, Type destination, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Building patch delegate for {source.Name} -> {destination.Name}", "Information");
        var registration = config.GetRegistration(source, destination);
        bool requiresContext = registration?.RequiresContext == true;

        var sourceParam = Expression.Parameter(source, "source");
        var destParam = Expression.Parameter(destination, "destination");

        if (requiresContext)
        {
            var contextParam = Expression.Parameter(typeof(VeloxResolutionContext), "context");
            var body = BuildPatchBody(sourceParam, destParam, source, destination, config, contextParam);

            var delegateType = typeof(Action<,,>).MakeGenericType(source, destination, typeof(VeloxResolutionContext));
            var lambda = Expression.Lambda(delegateType, body, sourceParam, destParam, contextParam);
            return lambda.Compile();
        }
        else
        {
            var body = BuildPatchBody(sourceParam, destParam, source, destination, config, null);

            var delegateType = typeof(Action<,>).MakeGenericType(source, destination);
            var lambda = Expression.Lambda(delegateType, body, sourceParam, destParam);
            return lambda.Compile();
        }
    }

    /// <summary>
    /// ProjectTo işlemi için <c>Expression&lt;Func&lt;TSource, TDest&gt;&gt;</c> döndürür.
    /// </summary>
    public static LambdaExpression BuildProjectToExpression(Type source, Type destination, MapperConfiguration config)
    {
        config.DiagnosticsSink?.Log($"Building ProjectTo expression for {source.Name} -> {destination.Name}", "Information");
        var sourceParam = Expression.Parameter(source, "source");
        var body = BuildMapBody(sourceParam, source, destination, config, null, MappingMode.ProjectTo);

        var funcType = typeof(Func<,>).MakeGenericType(source, destination);
        var lambda = Expression.Lambda(funcType, body, sourceParam);
        return lambda;
    }

    /// <summary>
    /// Kaynak türden hedef türe eşleme gövdesini oluşturur.
    /// ForMember kuralları, koleksiyon, enum, nullable ve flattening desteği içerir.
    /// </summary>
    private static Expression BuildMapBody(
        Expression sourceExpr, Type source, Type destination, MapperConfiguration config, ParameterExpression? contextParam, MappingMode mode)
    {
        var registration = config.GetRegistration(source, destination);

        // As<T> yönlendirmesi kontrolü (RedirectDestinationType)
        if (registration?.RedirectDestinationType != null)
        {
            var redirectType = registration.RedirectDestinationType;
            var redirectedExpr = BuildMapBody(sourceExpr, source, redirectType, config, contextParam, mode);
            return Expression.Convert(redirectedExpr, destination);
        }

        // 1. Özel tip dönüştürücü (IVeloxTypeConverter) kontrolü
        var converter = config.GetCustomConverter(source, destination);
        if (converter != null && mode != MappingMode.ProjectTo)
        {
            var converterType = converter.GetType();
            var convertMethod = converterType.GetMethod("Convert")!;
            Expression converterExpr;
            if (contextParam != null && !typeof(IConstantConverter).IsAssignableFrom(converterType))
            {
                var resolveServiceMethod = typeof(ExpressionBuilder).GetMethod(nameof(ResolveService))!;
                converterExpr = Expression.Convert(
                    Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(converterType)),
                    converterType
                );
            }
            else
            {
                converterExpr = Expression.Constant(converter);
            }
            return Expression.Call(converterExpr, convertMethod, sourceExpr);
        }

        // 0. İlkel tipler, string, Guid, DateTime vb. için erken çıkış
        if (destination.IsPrimitive || destination == typeof(string) || destination == typeof(decimal) ||
            destination == typeof(DateTime) || destination == typeof(Guid) || destination == typeof(TimeSpan))
        {
            if (destination.IsAssignableFrom(source))
                return sourceExpr;
            return Expression.Convert(sourceExpr, destination);
        }

        // 2b. Dictionary mapping kontrolü
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(source) && !destination.IsPrimitive && destination != typeof(string))
        {
            var dictExpr = TryBuildDictionaryMapping(sourceExpr, source, destination, config, contextParam, mode);
            if (dictExpr != null) return dictExpr;
        }

        // 2. Koleksiyon mapping kontrolü
        if (CollectionExpressionHelper.IsCollectionType(source) &&
            CollectionExpressionHelper.IsCollectionType(destination))
        {
            var srcElementType = CollectionExpressionHelper.GetCollectionElementType(source);
            var dstElementType = CollectionExpressionHelper.GetCollectionElementType(destination);

            if (srcElementType != null && dstElementType != null)
            {
                return CollectionExpressionHelper.BuildCollectionMapping(
                    sourceExpr, srcElementType, dstElementType, destination,
                    elementExpr => BuildPropertyAssignment(elementExpr, srcElementType, dstElementType, config, contextParam, registration?.ProfileName, mode)
                                   ?? throw new VeloxMappingException($"Koleksiyon elemanı eşleştirilemedi: {srcElementType.FullName} -> {dstElementType.FullName}"),
                    config.AllowNullCollections,
                    mode);
            }
        }

        // 3. Enum dönüşümü kontrolü
        var enumExpr = TryBuildEnumConversion(sourceExpr, source, destination);
        if (enumExpr != null) return enumExpr;

        // 4. Nullable dönüşümü kontrolü
        var nullableExpr = TryBuildNullableConversion(sourceExpr, source, destination, config, contextParam, registration?.ProfileName, mode);
        if (nullableExpr != null) return nullableExpr;

        ConstructorInfo? selectedCtor = null;
        Expression newExpr;
        bool hasFactory = registration?.FactoryDelegate != null;
        bool hasCondition = registration?.ForAllMembersCondition != null;
        bool requiresContext = registration?.RequiresContext == true;

        if (hasFactory)
        {
            var factoryExpr = Expression.Constant(registration!.FactoryDelegate);
            var boxedSource = Expression.Convert(sourceExpr, typeof(object));
            var invokeExpr = Expression.Invoke(factoryExpr, boxedSource);
            newExpr = Expression.Convert(invokeExpr, destination);
        }
        else
        {
            // 6. Constructor seçim algoritması
            var ctors = destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
            {
                throw new VeloxConfigurationException(
                    $"'{destination.FullName}' türü için public kurucu bulunamadı.");
            }

            selectedCtor = SelectConstructor(ctors, destination);

            // 7. Constructor parametrelerini kaynak özelliklerinden eşle
            var ctorParams = selectedCtor.GetParameters();
            var arguments = new Expression[ctorParams.Length];
            var sourceProps = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(config.ShouldMapProperty).ToArray();

            for (int i = 0; i < ctorParams.Length; i++)
            {
                var p = ctorParams[i];
                var ctorRule = registration?.CtorParamRules
                    .FirstOrDefault(r => string.Equals(r.ParameterName, p.Name, StringComparison.OrdinalIgnoreCase));

                if (ctorRule != null)
                {
                    var mapFromLambda = ctorRule.MapFromExpression;
                    var body = new ParameterReplacer(mapFromLambda.Parameters[0], sourceExpr)
                        .Visit(mapFromLambda.Body);

                    body = MakeNullSafe(body);

                    if (body.Type != p.ParameterType)
                        arguments[i] = Expression.Convert(body, p.ParameterType);
                    else
                        arguments[i] = body;
                }
                else
                {
                    arguments[i] = ResolvePropertyExpression(
                        sourceExpr, sourceProps, source, p.Name!, p.ParameterType, registration, config, contextParam, registration?.ProfileName, mode)
                        ?? Expression.Default(p.ParameterType);
                }
            }

            newExpr = Expression.New(selectedCtor, arguments);
        }

        Expression mapExpr;

        // Eğer tüm üyeler yoksayılacaksa doğrudan oluşturulan nesneyi dön
        if (registration != null && registration.ForAllMembersIgnored)
        {
            mapExpr = newExpr;
        }
        // Eğer factory veya custom condition varsa veya context gerekiyorsa veya ForPath kuralı varsa block tabanlı atama yap
        else if ((hasFactory || hasCondition || requiresContext || (registration != null && registration.ForPathRules.Count > 0)) && mode != MappingMode.ProjectTo)
        {
            var destVar = Expression.Variable(destination, "dest");
            var blockExprs = new List<Expression>
            {
                Expression.Assign(destVar, newExpr)
            };

            // PreserveReferences aktifse, henüz property atamaları başlamadan önce cache'e ekle
            if (registration != null && registration.PreserveReferences && contextParam != null)
            {
                var referenceCacheProp = typeof(VeloxResolutionContext).GetProperty("ReferenceCache", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
                var cacheAccess = Expression.Property(contextParam, referenceCacheProp);

                var cacheCtor = typeof(VeloxReferenceCache).GetConstructor(Type.EmptyTypes)!;
                var newCacheExpr = Expression.New(cacheCtor);

                var initCacheIfNull = Expression.IfThen(
                    Expression.Equal(cacheAccess, Expression.Constant(null, typeof(VeloxReferenceCache))),
                    Expression.Assign(cacheAccess, newCacheExpr)
                );

                var setMethod = typeof(VeloxReferenceCache).GetMethod("Set", new[] { typeof(object), typeof(object) })!;
                var boxedSource = Expression.Convert(sourceExpr, typeof(object));
                var boxedDest = Expression.Convert(destVar, typeof(object));
                var addToCache = Expression.Call(cacheAccess, setMethod, boxedSource, boxedDest);

                blockExprs.Add(initCacheIfNull);
                blockExprs.Add(addToCache);
            }

            // BeforeMap eylemleri eklenir
            if (registration != null && registration.BeforeMapActions.Count > 0 && contextParam != null)
            {
                var beforeMapExprs = BuildMappingActionsExpressions(
                    registration.BeforeMapActions, sourceExpr, destVar, contextParam, source, destination);
                blockExprs.AddRange(beforeMapExprs);
            }

            var destProps = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.CanWrite && config.ShouldMapProperty(p))
                .OrderBy(p => registration != null && registration.MemberRules.TryGetValue(p.Name, out var r) ? r.MappingOrder : 0)
                .ToArray();
            var sourceProps = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(config.ShouldMapProperty)
                .ToArray();

            foreach (var destProp in destProps)
            {
                // Global ignore edilmişse atla
                if (config.GlobalIgnores.Contains(destProp.Name))
                    continue;

                // Ignore edilmiş property'leri atla
                if (registration != null &&
                    registration.MemberRules.TryGetValue(destProp.Name, out var rule) &&
                    (rule.IsIgnored || rule.IsDoNotValidate))
                    continue;

                // Constructor'dan zaten verilen parametreleri atla (Factory değilse)
                if (!hasFactory && selectedCtor != null)
                {
                    var ctorParams = selectedCtor.GetParameters();
                    if (ctorParams.Any(cp => string.Equals(cp.Name, destProp.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                // Resolver, condition, pre-condition ve null-substitute kurallarını işle
                var propAssignExpr = BuildPropertyExpressionWithRules(
                    sourceExpr, destVar, sourceProps, source, destProp.Name, destProp.PropertyType, registration, config, contextParam, mode);

                if (propAssignExpr != null)
                {
                    blockExprs.Add(propAssignExpr);
                }
            }

            var destFields = destination.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly && config.ShouldMapField(f))
                .OrderBy(f => registration != null && registration.MemberRules.TryGetValue(f.Name, out var r) ? r.MappingOrder : 0)
                .ToArray();

            foreach (var destField in destFields)
            {
                if (config.GlobalIgnores.Contains(destField.Name))
                    continue;

                if (registration != null &&
                    registration.MemberRules.TryGetValue(destField.Name, out var rule) &&
                    (rule.IsIgnored || rule.IsDoNotValidate))
                    continue;

                if (!hasFactory && selectedCtor != null)
                {
                    var ctorParams = selectedCtor.GetParameters();
                    if (ctorParams.Any(cp => string.Equals(cp.Name, destField.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                var fieldAssignExpr = BuildPropertyExpressionWithRules(
                    sourceExpr, destVar, sourceProps, source, destField.Name, destField.FieldType, registration, config, contextParam, mode);

                if (fieldAssignExpr != null)
                {
                    blockExprs.Add(fieldAssignExpr);
                }
            }

            // ForPath atamaları eklenir
            if (registration != null && registration.ForPathRules.Count > 0)
            {
                var forPathExprs = BuildForPathExpressions(
                    registration.ForPathRules, sourceExpr, destVar, config, contextParam, registration.ProfileName, mode);
                blockExprs.AddRange(forPathExprs);
            }

            // AfterMap eylemleri eklenir
            if (registration != null && registration.AfterMapActions.Count > 0 && contextParam != null)
            {
                var afterMapExprs = BuildMappingActionsExpressions(
                    registration.AfterMapActions, sourceExpr, destVar, contextParam, source, destination);
                blockExprs.AddRange(afterMapExprs);
            }

            blockExprs.Add(destVar);
            mapExpr = Expression.Block(new[] { destVar }, blockExprs);
        }
        else
        {
            // 8. Constructor ile verilmemiş property'leri MemberInit ile bağla (Standart Mod)
            var destPropsStandard = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.CanWrite && config.ShouldMapProperty(p))
                .OrderBy(p => registration != null && registration.MemberRules.TryGetValue(p.Name, out var r) ? r.MappingOrder : 0)
                .ToArray();
            var bindings = new List<MemberBinding>();
            var sourcePropsStandard = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(config.ShouldMapProperty)
                .ToArray();

            foreach (var destProp in destPropsStandard)
            {
                // Global ignore edilmişse atla
                if (config.GlobalIgnores.Contains(destProp.Name))
                    continue;

                // Ignore edilmiş property'leri atla
                if (registration != null &&
                    registration.MemberRules.TryGetValue(destProp.Name, out var rule) &&
                    (rule.IsIgnored || rule.IsDoNotValidate))
                    continue;

                // Constructor'dan zaten verilen parametreleri atla
                if (selectedCtor != null)
                {
                    var ctorParams = selectedCtor.GetParameters();
                    if (ctorParams.Any(cp =>
                        string.Equals(cp.Name, destProp.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                var resolved = ResolvePropertyExpression(
                    sourceExpr, sourcePropsStandard, source, destProp.Name, destProp.PropertyType, registration, config, contextParam, registration?.ProfileName, mode);

                if (resolved != null)
                {
                    bindings.Add(Expression.Bind(destProp, resolved));
                }
            }

            var destFieldsStandard = destination.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly && config.ShouldMapField(f))
                .OrderBy(f => registration != null && registration.MemberRules.TryGetValue(f.Name, out var r) ? r.MappingOrder : 0)
                .ToArray();

            foreach (var destField in destFieldsStandard)
            {
                if (config.GlobalIgnores.Contains(destField.Name))
                    continue;

                if (registration != null &&
                    registration.MemberRules.TryGetValue(destField.Name, out var rule) &&
                    (rule.IsIgnored || rule.IsDoNotValidate))
                    continue;

                if (selectedCtor != null)
                {
                    var ctorParams = selectedCtor.GetParameters();
                    if (ctorParams.Any(cp => string.Equals(cp.Name, destField.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                var resolved = ResolvePropertyExpression(
                    sourceExpr, sourcePropsStandard, source, destField.Name, destField.FieldType, registration, config, contextParam, registration?.ProfileName, mode);

                if (resolved != null)
                {
                    bindings.Add(Expression.Bind(destField, resolved));
                }
            }

            mapExpr = bindings.Count > 0
                ? Expression.MemberInit((NewExpression)newExpr, bindings)
                : newExpr;
        }

        // Polimorfik Haritalama (Include) Kontrolü
        if (registration != null && registration.IncludedDerivedTypes.Count > 0 && contextParam != null && mode != MappingMode.ProjectTo)
        {
            var mapMethod = typeof(IVeloxMapper).GetMethods()
                .First(m => m.Name == "Map" && 
                            m.IsGenericMethod && 
                            m.GetGenericArguments().Length == 2 && 
                            m.GetParameters().Length == 2 && 
                            m.GetParameters()[1].ParameterType == typeof(VeloxResolutionContext));

            var mapperProp = typeof(VeloxResolutionContext).GetProperty("Mapper")!;
            var mapperAccess = Expression.Property(contextParam, mapperProp);

            Expression currentElse = mapExpr;

            // Derived tipleri en derindekinden başlayarak sırala (ToyPoodle -> Dog -> Animal)
            var sortedDerivedTypes = registration.IncludedDerivedTypes
                .OrderBy(t => GetInheritanceDepth(t.DerivedSource))
                .Reverse()
                .ToList();

            foreach (var (derivedSource, derivedDest) in sortedDerivedTypes)
            {
                if (sourceExpr.Type.IsAssignableFrom(derivedSource) || derivedSource.IsAssignableFrom(sourceExpr.Type))
                {
                    var isTypeExpr = Expression.TypeIs(sourceExpr, derivedSource);
                    
                    var mapMethodGeneric = mapMethod.MakeGenericMethod(derivedSource, derivedDest);
                    var castedSource = Expression.Convert(sourceExpr, derivedSource);
                    var callMap = Expression.Call(mapperAccess, mapMethodGeneric, castedSource, contextParam);
                    
                    var convertedCall = Expression.Convert(callMap, destination);
                    
                    currentElse = Expression.Condition(isTypeExpr, convertedCall, currentElse);
                }
            }
            
            mapExpr = currentElse;
        }

        // En dış sarmalayıcı (MaxDepth ve PreserveReferences)
        if (registration != null && contextParam != null && mode != MappingMode.ProjectTo)
        {
            var expressions = new List<Expression>();
            var variables = new List<ParameterExpression>();

            var resultVar = Expression.Variable(destination, "result");
            variables.Add(resultVar);

            Expression? preserveReferencesCheck = null;
            Expression? maxDepthCheck = null;
            Expression mainMappingFlow = mapExpr;
            LabelTarget? returnTarget = null;

            // 1. PreserveReferences Giriş Kontrolü
            if (registration.PreserveReferences)
            {
                var referenceCacheProp = typeof(VeloxResolutionContext).GetProperty("ReferenceCache", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
                var cacheAccess = Expression.Property(contextParam, referenceCacheProp);

                var tryGetValueMethod = typeof(VeloxReferenceCache).GetMethod("TryGetValue", new[] { typeof(object), typeof(object).MakeByRefType() })!;
                var cachedVar = Expression.Variable(typeof(object), "cachedVal");
                variables.Add(cachedVar);

                var tryGetValueCall = Expression.Call(cacheAccess, tryGetValueMethod, Expression.Convert(sourceExpr, typeof(object)), cachedVar);

                var cacheCheckCondition = Expression.AndAlso(
                    Expression.NotEqual(cacheAccess, Expression.Constant(null, typeof(VeloxReferenceCache))),
                    tryGetValueCall
                );

                returnTarget = Expression.Label();
                preserveReferencesCheck = Expression.IfThen(
                    cacheCheckCondition,
                    Expression.Block(
                        Expression.Assign(resultVar, Expression.Convert(cachedVar, destination)),
                        Expression.Return(returnTarget) // early return
                    )
                );
            }

            // 2. MaxDepth Giriş Kontrolü ve Derinlik Yönetimi
            if (registration.MaxDepth > 0)
            {
                var currentDepthProp = typeof(VeloxResolutionContext).GetProperty("CurrentDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
                var depthAccess = Expression.Property(contextParam, currentDepthProp);
                var maxDepthConst = Expression.Constant(registration.MaxDepth);

                var depthCheck = Expression.GreaterThanOrEqual(depthAccess, maxDepthConst);

                var incrementDepth = Expression.Assign(depthAccess, Expression.Add(depthAccess, Expression.Constant(1)));
                var decrementDepth = Expression.Assign(depthAccess, Expression.Subtract(depthAccess, Expression.Constant(1)));

                var tryFinally = Expression.TryFinally(
                    Expression.Assign(resultVar, mainMappingFlow),
                    decrementDepth
                );

                maxDepthCheck = Expression.IfThenElse(
                    depthCheck,
                    Expression.Assign(resultVar, Expression.Default(destination)),
                    Expression.Block(
                        incrementDepth,
                        tryFinally
                    )
                );

                mainMappingFlow = maxDepthCheck;
            }
            else
            {
                mainMappingFlow = Expression.Assign(resultVar, mainMappingFlow);
            }

            if (preserveReferencesCheck != null)
            {
                var labelReturn = Expression.Label(returnTarget!);

                var fullFlowBlock = Expression.Block(
                    preserveReferencesCheck,
                    mainMappingFlow,
                    labelReturn
                );

                return Expression.Block(variables, fullFlowBlock, resultVar);
            }
            else if (registration.MaxDepth > 0)
            {
                return Expression.Block(variables, mainMappingFlow, resultVar);
            }
        }

        return mapExpr;
    }

    /// <summary>
    /// Bir hedef property veya constructor parametresi için kaynak expression çözümler.
    /// Öncelik sırası: ForMember MapFrom → isim eşleştirme → flattening
    /// </summary>
    private static Expression? ResolvePropertyExpression(
        Expression sourceExpr,
        PropertyInfo[] sourceProps,
        Type sourceType,
        string destName,
        Type destType,
        MappingRegistration? registration,
        MapperConfiguration config,
        ParameterExpression? contextParam,
        string? profileName,
        MappingMode mode,
        Expression? destExpr = null)
    {
        // 1. ForMember MapFrom kontrolü
        if (registration != null &&
            registration.MemberRules.TryGetValue(destName, out var rule) &&
            rule.MapFromExpression != null)
        {
            // MapFrom lambda'sının parametresini sourceExpr ile değiştir
            var mapFromLambda = rule.MapFromExpression;
            var body = new ParameterReplacer(mapFromLambda.Parameters[0], sourceExpr)
                .Visit(mapFromLambda.Body);

            body = MakeNullSafe(body);

            // Tip uyumluluğu — gerekirse dönüşüm ekle
            if (body.Type != destType && destType.IsAssignableFrom(body.Type))
                return body;
            if (body.Type != destType)
                return Expression.Convert(body, destType);
            return body;
        }

        // 2. İsimle eşleşen kaynak property (prefix, postfix, naming convention destekli)
        var srcProp = sourceProps.FirstOrDefault(x =>
            NameMatchingHelper.IsMatch(x.Name, destName, config));

        if (srcProp != null && srcProp.CanRead)
        {
            var propAccess = Expression.Property(sourceExpr, srcProp);
            var safePropAccess = MakeNullSafe(propAccess);
            return BuildPropertyAssignment(safePropAccess, srcProp.PropertyType, destType, config, contextParam, profileName, mode);
        }

        // 2b. İsimle eşleşen kaynak field
        var sourceFields = sourceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(config.ShouldMapField)
            .ToArray();
        var srcField = sourceFields.FirstOrDefault(x =>
            NameMatchingHelper.IsMatch(x.Name, destName, config));

        if (srcField != null)
        {
            var fieldAccess = Expression.Field(sourceExpr, srcField);
            var safeFieldAccess = MakeNullSafe(fieldAccess);
            return BuildPropertyAssignment(safeFieldAccess, srcField.FieldType, destType, config, contextParam, profileName, mode);
        }

        // 3. Flattening denemesi (AddressCity → Address.City)
        var flattenExpr = FlatteningHelper.TryResolveFlattening(sourceExpr, sourceType, destName, config);
        if (flattenExpr != null)
        {
            var safeFlattenExpr = MakeNullSafe(flattenExpr);
            return BuildPropertyAssignment(safeFlattenExpr, flattenExpr.Type, destType, config, contextParam, profileName, mode);
        }

        return null;
    }

    /// <summary>
    /// Property atamasını resolver, condition, pre-condition ve null-substitute kurallarını uygulayarak oluşturur.
    /// </summary>
    private static Expression? BuildPropertyExpressionWithRules(
        Expression sourceExpr,
        Expression destVar,
        PropertyInfo[] sourceProps,
        Type sourceType,
        string destName,
        Type destType,
        MappingRegistration? registration,
        MapperConfiguration config,
        ParameterExpression? contextParam,
        MappingMode mode,
        MemberMappingRule? customRule = null,
        Expression? assignTarget = null)
    {
        var target = assignTarget ?? destVar;
        Expression destPropAccess;
        var pInfo = target.Type.GetProperty(destName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (pInfo != null)
            destPropAccess = Expression.Property(target, pInfo);
        else
        {
            var fInfo = target.Type.GetField(destName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fInfo != null)
                destPropAccess = Expression.Field(target, fInfo);
            else
                destPropAccess = Expression.Property(target, destName);
        }
        MemberMappingRule? rule = customRule;
        if (rule == null)
        {
            registration?.MemberRules.TryGetValue(destName, out rule);
        }

        // Değer Çözümleme
        Expression? resolvedValueExpr = null;

        if (rule != null)
        {
            if (rule.ResolverType != null && contextParam != null)
            {
                var resolveServiceMethod = typeof(ExpressionBuilder).GetMethod(nameof(ResolveService))!;
                var resolverInstanceExpr = Expression.Convert(
                    Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(rule.ResolverType)),
                    rule.ResolverType
                );
                var resolveMethod = rule.ResolverType.GetMethod("Resolve")!;
                var destValExpr = destPropAccess;
                resolvedValueExpr = Expression.Call(resolverInstanceExpr, resolveMethod, sourceExpr, destVar, destValExpr, contextParam);
            }
            else if (rule.MemberValueResolverType != null && contextParam != null)
            {
                var resolveServiceMethod = typeof(ExpressionBuilder).GetMethod(nameof(ResolveService))!;
                var resolverInstanceExpr = Expression.Convert(
                    Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(rule.MemberValueResolverType)),
                    rule.MemberValueResolverType
                );

                var sourceMemberLambda = rule.SourceMemberForResolver!;
                var sourceMemberVal = new ParameterReplacer(sourceMemberLambda.Parameters[0], sourceExpr)
                    .Visit(sourceMemberLambda.Body);
                sourceMemberVal = MakeNullSafe(sourceMemberVal);

                var resolveMethod = rule.MemberValueResolverType.GetMethod("Resolve")!;
                var destValExpr = destPropAccess;
                resolvedValueExpr = Expression.Call(resolverInstanceExpr, resolveMethod, sourceExpr, destVar, sourceMemberVal, destValExpr, contextParam);
            }
            else if ((rule.ValueConverterType != null || rule.ValueConverter != null) && contextParam != null)
            {
                var resolveServiceMethod = typeof(ExpressionBuilder).GetMethod(nameof(ResolveService))!;
                Expression converterInstanceExpr;
                Type converterType;
                if (rule.ValueConverterType != null)
                {
                    converterType = rule.ValueConverterType;
                    converterInstanceExpr = Expression.Convert(
                        Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(converterType)),
                        converterType
                    );
                }
                else
                {
                    converterType = rule.ValueConverter!.GetType();
                    converterInstanceExpr = Expression.Convert(
                        Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(converterType)),
                        converterType
                    );
                }

                var converterSourceLambda = rule.ValueConverterSourceMember!;
                var converterSourceVal = new ParameterReplacer(converterSourceLambda.Parameters[0], sourceExpr)
                    .Visit(converterSourceLambda.Body);
                converterSourceVal = MakeNullSafe(converterSourceVal);

                var convertMethod = converterType.GetMethod("Convert")!;
                resolvedValueExpr = Expression.Call(converterInstanceExpr, convertMethod, converterSourceVal, contextParam);
            }
            else if (rule.MapFromExpression != null)
            {
                var mapFromLambda = rule.MapFromExpression;
                var body = new ParameterReplacer(mapFromLambda.Parameters[0], sourceExpr)
                    .Visit(mapFromLambda.Body);

                body = MakeNullSafe(body);

                if (body.Type != destType)
                    resolvedValueExpr = Expression.Convert(body, destType);
                else
                    resolvedValueExpr = body;
            }
        }

        // Eğer henüz çözümlenmediyse standart çözümleme yap
        if (resolvedValueExpr == null)
        {
            var resolved = ResolvePropertyExpression(sourceExpr, sourceProps, sourceType, destName, destType, registration, config, contextParam, registration?.ProfileName, mode, destVar);
            if (resolved == null) return null;
            resolvedValueExpr = resolved;
        }

        // NullSubstitute kontrolü
        if (rule != null && rule.HasNullSubstitute)
        {
            if (!resolvedValueExpr.Type.IsValueType || Nullable.GetUnderlyingType(resolvedValueExpr.Type) != null)
            {
                var substituteConst = Expression.Constant(rule.NullSubstituteValue, resolvedValueExpr.Type);
                var isNullCheck = Expression.Equal(resolvedValueExpr, Expression.Constant(null, resolvedValueExpr.Type));
                resolvedValueExpr = Expression.Condition(isNullCheck, substituteConst, resolvedValueExpr);
            }
        }

        // Atama ifadesi
        Expression finalAssignExpr;
        if (rule != null && rule.KeepDestinationValue && contextParam != null)
        {
            if (CollectionExpressionHelper.IsCollectionType(destType))
            {
                var srcElementType = CollectionExpressionHelper.GetCollectionElementType(resolvedValueExpr.Type);
                var dstElementType = CollectionExpressionHelper.GetCollectionElementType(destType);

                if (srcElementType != null && dstElementType != null)
                {
                    var mergeMethod = typeof(CollectionExpressionHelper).GetMethod(nameof(CollectionExpressionHelper.MergeCollections))!
                        .MakeGenericMethod(srcElementType, dstElementType);
                    
                    var destCollectionType = typeof(ICollection<>).MakeGenericType(dstElementType);
                    var castedDest = Expression.Convert(destPropAccess, destCollectionType);
                    var mergeCall = Expression.Call(null, mergeMethod, resolvedValueExpr, castedDest, contextParam);
                    
                    finalAssignExpr = Expression.Block(
                        Expression.IfThenElse(
                            Expression.Equal(destPropAccess, Expression.Constant(null, destPropAccess.Type)),
                            Expression.Assign(destPropAccess, resolvedValueExpr), // null ise normal atama
                            mergeCall
                        )
                    );
                }
                else
                {
                    finalAssignExpr = Expression.Assign(destPropAccess, resolvedValueExpr);
                }
            }
            else if (!destType.IsValueType && destType != typeof(string))
            {
                var mapperProp = typeof(VeloxResolutionContext).GetProperty("Mapper")!;
                var mapperAccess = Expression.Property(contextParam, mapperProp);
                var mapMethod = typeof(Mapper).GetMethod("Map", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object), typeof(object), typeof(VeloxResolutionContext) }, null);
                
                if (mapMethod != null)
                {
                    var newDestExpr = Expression.New(destType);
                    var destInstance = Expression.Variable(destType, "destInstance");
                    var mapperConcrete = Expression.Convert(mapperAccess, typeof(Mapper));
                    var callMap = Expression.Call(mapperConcrete, mapMethod, 
                        Expression.Convert(resolvedValueExpr, typeof(object)), 
                        Expression.Convert(destInstance, typeof(object)), 
                        contextParam
                    );
                    
                    finalAssignExpr = Expression.Block(
                        new[] { destInstance },
                        Expression.Assign(destInstance, Expression.Coalesce(destPropAccess, Expression.Convert(newDestExpr, destType))),
                        Expression.IfThen(
                            Expression.NotEqual(resolvedValueExpr, Expression.Constant(null, resolvedValueExpr.Type)),
                            Expression.Block(
                                callMap,
                                Expression.Assign(destPropAccess, destInstance)
                            )
                        )
                    );
                }
                else
                {
                    finalAssignExpr = Expression.Assign(destPropAccess, resolvedValueExpr);
                }
            }
            else
            {
                finalAssignExpr = Expression.Assign(destPropAccess, resolvedValueExpr);
            }
        }
        else
        {
            finalAssignExpr = Expression.Assign(destPropAccess, resolvedValueExpr);
        }

        if (contextParam != null)
        {
            var currentMemberProp = typeof(VeloxResolutionContext).GetProperty("CurrentMember")!;
            var setCurrentMember = Expression.Assign(
                Expression.Property(contextParam, currentMemberProp),
                Expression.Constant(destName)
            );
            finalAssignExpr = Expression.Block(setCurrentMember, finalAssignExpr);
        }

        // Condition kontrolü
        if (rule != null && rule.ConditionDelegate != null)
        {
            var condConst = Expression.Constant(rule.ConditionDelegate);
            var tempVar = Expression.Variable(resolvedValueExpr.Type, "tempVal");
            var assignToTemp = Expression.Assign(tempVar, resolvedValueExpr);

            var invokeCondition = Expression.Invoke(condConst, sourceExpr, destVar, tempVar);
            var ifCondition = Expression.IfThen(invokeCondition, Expression.Assign(destPropAccess, tempVar));

            finalAssignExpr = Expression.Block(new[] { tempVar }, assignToTemp, ifCondition);
        }
        else if (registration?.ForAllMembersCondition != null)
        {
            var tempVar = Expression.Variable(resolvedValueExpr.Type, "tempVal");
            var assignToTemp = Expression.Assign(tempVar, resolvedValueExpr);

            var boxedSource = Expression.Convert(sourceExpr, typeof(object));
            var boxedDest = Expression.Convert(destVar, typeof(object));
            var boxedVal = Expression.Convert(tempVar, typeof(object));

            var conditionExpr = Expression.Constant(registration.ForAllMembersCondition);
            var invokeCondition = Expression.Invoke(conditionExpr, boxedSource, boxedDest, boxedVal);

            var ifCondition = Expression.IfThen(invokeCondition, Expression.Assign(destPropAccess, tempVar));

            finalAssignExpr = Expression.Block(new[] { tempVar }, assignToTemp, ifCondition);
        }

        // IgnoreNullValues kontrolü (yama modunda kaynak null ise hedefe yazmayı atla)
        if (config.IgnoreNullValues && !resolvedValueExpr.Type.IsValueType && (rule == null || !rule.HasNullSubstitute))
        {
            var isNotNull = Expression.NotEqual(resolvedValueExpr, Expression.Constant(null, resolvedValueExpr.Type));
            finalAssignExpr = Expression.IfThen(isNotNull, finalAssignExpr);
        }

        // PreCondition varsa saralım
        if (rule != null && rule.PreConditionDelegate != null)
        {
            var preCondConst = Expression.Constant(rule.PreConditionDelegate);
            var preConditionExpr = Expression.Invoke(preCondConst, sourceExpr);
            return Expression.IfThen(preConditionExpr, finalAssignExpr);
        }

        return finalAssignExpr;
    }

    /// <summary>
    /// Kaynak expression'ı hedef türe uygun hale getirir.
    /// Doğrudan atama, enum dönüşümü, nullable wrap/unwrap veya rekürsif mapping yapar.
    /// </summary>
    private static Expression? BuildPropertyAssignment(
        Expression sourceExpr, Type sourceType, Type destType, MapperConfiguration config, ParameterExpression? contextParam, string? profileName, MappingMode mode)
    {
        Expression? result = null;

        // Doğrudan atanabilir (Koleksiyon değilse)
        if (destType.IsAssignableFrom(sourceType) &&
            !(CollectionExpressionHelper.IsCollectionType(sourceType) && CollectionExpressionHelper.IsCollectionType(destType)))
        {
            var underlying = Nullable.GetUnderlyingType(destType);
            if (underlying != null && underlying == sourceType)
            {
                result = Expression.Convert(sourceExpr, destType);
            }
            else
            {
                result = sourceExpr;
            }
        }
        // Koleksiyon → Koleksiyon
        else if (CollectionExpressionHelper.IsCollectionType(sourceType) &&
            CollectionExpressionHelper.IsCollectionType(destType))
        {
            var srcElem = CollectionExpressionHelper.GetCollectionElementType(sourceType);
            var dstElem = CollectionExpressionHelper.GetCollectionElementType(destType);
            if (srcElem != null && dstElem != null)
            {
                result = CollectionExpressionHelper.BuildCollectionMapping(
                    sourceExpr, srcElem, dstElem, destType,
                    e => BuildPropertyAssignment(e, srcElem, dstElem, config, contextParam, profileName, mode)
                         ?? throw new VeloxMappingException($"Koleksiyon elemanı eşleştirilemedi: {srcElem.FullName} -> {dstElem.FullName}"),
                    config.AllowNullCollections,
                    mode);
            }
        }
        // Enum dönüşümü
        else if (TryBuildEnumConversion(sourceExpr, sourceType, destType) is Expression enumExpr)
        {
            result = enumExpr;
        }
        // Nullable dönüşümü
        else if (TryBuildNullableConversion(sourceExpr, sourceType, destType, config, contextParam, profileName, mode) is Expression nullableExpr)
        {
            result = nullableExpr;
        }
        // Karmaşık tip — rekürsif mapping
        else if (!sourceType.IsPrimitive && sourceType != typeof(string) &&
            !destType.IsPrimitive && destType != typeof(string))
        {
            if (contextParam != null && mode != MappingMode.ProjectTo)
            {
                var mapperProp = typeof(VeloxResolutionContext).GetProperty("Mapper")!;
                var mapperAccess = Expression.Property(contextParam, mapperProp);
                
                var mapMethod = typeof(IVeloxMapper).GetMethods()
                    .First(m => m.Name == "Map" && 
                                m.IsGenericMethod && 
                                m.GetGenericArguments().Length == 2 && 
                                m.GetParameters().Length == 2 && 
                                m.GetParameters()[1].ParameterType == typeof(VeloxResolutionContext));
                                
                var mapMethodGeneric = mapMethod.MakeGenericMethod(sourceType, destType);
                result = Expression.Call(mapperAccess, mapMethodGeneric, sourceExpr, contextParam);
            }
            else
            {
                result = BuildMapBody(sourceExpr, sourceType, destType, config, contextParam, mode);
                
                // Eğer in-memory çalışıyorsak (ProjectTo değilse) ve sourceExpr null olabiliyorsa,
                // tüm haritalamayı null kontrolü ile sarmala: sourceExpr == null ? null : result
                if (mode != MappingMode.ProjectTo && CanBeNull(sourceExpr.Type) && sourceExpr is not ParameterExpression)
                {
                    var isNull = IsNullExpression(sourceExpr);
                    result = Expression.Condition(isNull, Expression.Default(destType), result);
                }
            }
        }

        if (result != null)
        {
            result = ApplyValueTransformers(result, destType, config, profileName);
        }

        return result;
    }

    /// <summary>
    /// Enum dönüşüm expression'ı üretmeye çalışır.
    /// </summary>
    private static Expression? TryBuildEnumConversion(Expression sourceExpr, Type srcType, Type destType)
    {
        var srcUnderlying = Nullable.GetUnderlyingType(srcType) ?? srcType;
        var dstUnderlying = Nullable.GetUnderlyingType(destType) ?? destType;

        // Enum → Enum (farklı enum türleri — underlying value üzerinden cast)
        if (srcUnderlying.IsEnum && dstUnderlying.IsEnum)
        {
            var actualSource = srcType != srcUnderlying
                ? (Expression)Expression.Property(sourceExpr, "Value")
                : sourceExpr;

            var underlyingType = Enum.GetUnderlyingType(srcUnderlying);
            var toUnderlying = Expression.Convert(actualSource, underlyingType);
            var toDestEnum = Expression.Convert(toUnderlying, dstUnderlying);

            return destType != dstUnderlying
                ? Expression.Convert(toDestEnum, destType)
                : toDestEnum;
        }

        // Enum → string (ToString)
        if (srcUnderlying.IsEnum && dstUnderlying == typeof(string))
        {
            var actualSource = srcType != srcUnderlying
                ? (Expression)Expression.Property(sourceExpr, "Value")
                : sourceExpr;

            var toStringMethod = srcUnderlying.GetMethod("ToString", Type.EmptyTypes)!;
            return Expression.Call(actualSource, toStringMethod);
        }

        // string → Enum (Enum.Parse)
        if (srcUnderlying == typeof(string) && dstUnderlying.IsEnum)
        {
            var parseMethod = typeof(Enum).GetMethod("Parse", new[] { typeof(Type), typeof(string), typeof(bool) })!;
            var parsed = Expression.Call(null, parseMethod,
                Expression.Constant(dstUnderlying),
                sourceExpr,
                Expression.Constant(true)); // case-insensitive

            return Expression.Convert(parsed, destType);
        }

        // Enum → int / int → Enum (underlying type üzerinden cast)
        if (srcUnderlying.IsEnum && dstUnderlying == Enum.GetUnderlyingType(srcUnderlying))
        {
            return Expression.Convert(sourceExpr, destType);
        }
        if (dstUnderlying.IsEnum && srcUnderlying == Enum.GetUnderlyingType(dstUnderlying))
        {
            return Expression.Convert(sourceExpr, destType);
        }

        return null;
    }

    /// <summary>
    /// Nullable dönüşüm expression'ı üretmeye çalışır.
    /// int → int?, int? → int (null kontrolü ile), T? → T? gibi dönüşümler.
    /// </summary>
    private static Expression? TryBuildNullableConversion(
        Expression sourceExpr, Type srcType, Type destType, MapperConfiguration config, ParameterExpression? contextParam, string? profileName, MappingMode mode)
    {
        var srcUnderlying = Nullable.GetUnderlyingType(srcType);
        var dstUnderlying = Nullable.GetUnderlyingType(destType);

        // T → T? (non-nullable to nullable wrapper)
        if (srcUnderlying == null && dstUnderlying != null && srcType == dstUnderlying)
        {
            return Expression.Convert(sourceExpr, destType);
        }

        // T? → T (nullable to non-nullable — Value property erişimi)
        if (srcUnderlying != null && dstUnderlying == null && srcUnderlying == destType)
        {
            // source.HasValue ? source.Value : default(T)
            var hasValue = Expression.Property(sourceExpr, "HasValue");
            var value = Expression.Property(sourceExpr, "Value");
            return Expression.Condition(hasValue, value, Expression.Default(destType));
        }

        // T? → U? (nullable to nullable, farklı underlying tür)
        if (srcUnderlying != null && dstUnderlying != null && srcUnderlying != dstUnderlying)
        {
            var hasValue = Expression.Property(sourceExpr, "HasValue");
            var value = Expression.Property(sourceExpr, "Value");

            var convertedValue = BuildPropertyAssignment(value, srcUnderlying, dstUnderlying, config, contextParam, profileName, mode);
            if (convertedValue != null)
            {
                var wrappedValue = Expression.Convert(convertedValue, destType);
                return Expression.Condition(hasValue, wrappedValue, Expression.Default(destType));
            }
        }

        return null;
    }

    /// <summary>
    /// Constructor seçim algoritması:
    /// 1. [VeloxConstructor] özniteliği olan kazanır
    /// 2. Yoksa parametresiz kurucu varsa onu seç
    /// 3. Yoksa en çok parametreye sahip olanı seç
    /// 4. Birden fazla aday varsa VeloxAmbiguousConstructorException
    /// </summary>
    private static ConstructorInfo SelectConstructor(ConstructorInfo[] ctors, Type destination)
    {
        // [VeloxConstructor] kontrolü
        var veloxCtors = ctors
            .Where(c => c.GetCustomAttribute<VeloxConstructorAttribute>() != null)
            .ToArray();

        if (veloxCtors.Length == 1)
            return veloxCtors[0];

        if (veloxCtors.Length > 1)
        {
            throw new VeloxConfigurationException(
                $"'{destination.FullName}' türünde birden fazla [VeloxConstructor] özniteliği bulundu. " +
                $"Sadece bir kurucuya uygulanmalıdır.");
        }

        // Parametresiz kurucu varsa onu tercih et
        var parameterless = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterless != null)
            return parameterless;

        // En çok parametreye sahip kurucu
        var maxParams = ctors.Max(c => c.GetParameters().Length);
        var candidates = ctors.Where(c => c.GetParameters().Length == maxParams).ToArray();

        if (candidates.Length > 1)
            throw new VeloxAmbiguousConstructorException(destination);

        return candidates[0];
    }

    /// <summary>
    /// Patch (yama) modu için kaynak değerleri var olan hedef nesne üzerine yazar.
    /// </summary>
    private static Expression BuildPatchBody(
        Expression sourceExpr, Expression destExpr,
        Type source, Type destination,
        MapperConfiguration config,
        ParameterExpression? contextParam)
    {
        var registration = config.GetRegistration(source, destination);

        // Eğer tüm üyeler yoksayılacaksa hiçbir atama yapma
        if (registration != null && registration.ForAllMembersIgnored)
        {
            return Expression.Empty();
        }

        var sourceProps = source.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.CanRead && config.ShouldMapProperty(p))
            .ToArray();
        var destProps = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.CanWrite && config.ShouldMapProperty(p))
            .OrderBy(p => registration != null && registration.MemberRules.TryGetValue(p.Name, out var r) ? r.MappingOrder : 0)
            .ToArray();

        var expressions = new List<Expression>();

        // BeforeMap eylemleri eklenir
        if (registration != null && registration.BeforeMapActions.Count > 0 && contextParam != null)
        {
            var beforeMapExprs = BuildMappingActionsExpressions(
                registration.BeforeMapActions, sourceExpr, destExpr, contextParam, source, destination);
            expressions.AddRange(beforeMapExprs);
        }

        foreach (var destProp in destProps)
        {
            // Global ignore edilmişse atla
            if (config.GlobalIgnores.Contains(destProp.Name))
                continue;

            // Ignore edilmiş property'leri atla
            if (registration != null &&
                registration.MemberRules.TryGetValue(destProp.Name, out var ignoreRule) &&
                (ignoreRule.IsIgnored || ignoreRule.IsDoNotValidate))
                continue;

            var assignExpr = BuildPropertyExpressionWithRules(
                sourceExpr, destExpr, sourceProps.ToArray(), source, destProp.Name, destProp.PropertyType, registration, config, contextParam, MappingMode.Patch);

            if (assignExpr != null)
            {
                expressions.Add(assignExpr);
            }
        }

        var destFields = destination.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly && config.ShouldMapField(f))
            .OrderBy(f => registration != null && registration.MemberRules.TryGetValue(f.Name, out var r) ? r.MappingOrder : 0)
            .ToArray();

        foreach (var destField in destFields)
        {
            if (config.GlobalIgnores.Contains(destField.Name))
                continue;

            if (registration != null &&
                registration.MemberRules.TryGetValue(destField.Name, out var ignoreRule) &&
                (ignoreRule.IsIgnored || ignoreRule.IsDoNotValidate))
                continue;

            var assignExpr = BuildPropertyExpressionWithRules(
                sourceExpr, destExpr, sourceProps.ToArray(), source, destField.Name, destField.FieldType, registration, config, contextParam, MappingMode.Patch);

            if (assignExpr != null)
            {
                expressions.Add(assignExpr);
            }
        }

        // AfterMap eylemleri eklenir
        if (registration != null && registration.AfterMapActions.Count > 0 && contextParam != null)
        {
            var afterMapExprs = BuildMappingActionsExpressions(
                registration.AfterMapActions, sourceExpr, destExpr, contextParam, source, destination);
            expressions.AddRange(afterMapExprs);
        }

        return expressions.Count == 0
            ? Expression.Empty()
            : Expression.Block(expressions);
    }

    /// <summary>
    /// BeforeMap ve AfterMap eylemlerini çalıştıracak Expression listesini oluşturur.
    /// </summary>
    private static List<Expression> BuildMappingActionsExpressions(
        IReadOnlyList<object> actions,
        Expression sourceExpr,
        Expression destExpr,
        ParameterExpression contextParam,
        Type sourceType,
        Type destType)
    {
        var list = new List<Expression>();
        foreach (var action in actions)
        {
            if (action is Type actionType)
            {
                var resolveServiceMethod = typeof(ExpressionBuilder).GetMethod(nameof(ResolveService))!;
                var mappingActionType = typeof(IVeloxMappingAction<,>).MakeGenericType(sourceType, destType);
                
                var actionInstanceExpr = Expression.Convert(
                    Expression.Call(resolveServiceMethod, contextParam, Expression.Constant(actionType)),
                    mappingActionType
                );
                
                var processMethod = mappingActionType.GetMethod("Process")!;
                var callExpr = Expression.Call(actionInstanceExpr, processMethod, sourceExpr, destExpr, contextParam);
                list.Add(callExpr);
            }
            else if (action is Delegate inlineAction)
            {
                var invokeExpr = Expression.Invoke(Expression.Constant(inlineAction), sourceExpr, destExpr);
                list.Add(invokeExpr);
            }
        }
        return list;
    }

    /// <summary>
    /// Lambda expression parametresini başka bir expression ile değiştiren ziyaretçi.
    /// ForMember MapFrom lambda'larında kullanılır.
    /// </summary>
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam;
        private readonly Expression _newExpr;

        public ParameterReplacer(ParameterExpression oldParam, Expression newExpr)
        {
            _oldParam = oldParam;
            _newExpr = newExpr;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParam ? _newExpr : base.VisitParameter(node);
        }
    }

    /// <summary>
    /// Tipin kalıtım derinliğini hesaplar. Hiyerarşide en alttaki (derived) tipi en önce kontrol etmek için kullanılır.
    /// </summary>
    private static int GetInheritanceDepth(Type type)
    {
        int depth = 0;
        var parent = type.BaseType;
        while (parent != null)
        {
            depth++;
            parent = parent.BaseType;
        }
        return depth;
    }

    /// <summary>
    /// Değere uygun value transformer'ları (varsa) uygular.
    /// Önce profile düzeyindeki transformer'lar, yoksa global düzeydeki transformer'lar kontrol edilir.
    /// </summary>
    private static Expression ApplyValueTransformers(Expression expr, Type targetType, MapperConfiguration config, string? profileName)
    {
        Delegate? transformer = null;

        // 1. Profil düzeyindeki transformer'ları kontrol et
        if (profileName != null && config.ProfileValueTransformers.TryGetValue(profileName, out var profileCollection))
        {
            transformer = profileCollection.GetTransformer(targetType);
        }

        // 2. Profil düzeyinde yoksa global transformer'ları kontrol et
        if (transformer == null)
        {
            transformer = config.GlobalValueTransformers.GetTransformer(targetType);
        }

        // 3. Eğer transformer varsa, invoke et
        if (transformer != null)
        {
            var transformerConst = Expression.Constant(transformer);
            return Expression.Invoke(transformerConst, expr);
        }

        return expr;
    }

    /// <summary>
    /// ForPath kuralları için derin nesne grafiklerinde null kontrolleri yaparak atama ifadelerini oluşturur.
    /// </summary>
    private static List<Expression> BuildForPathExpressions(
        IReadOnlyList<ForPathRule> forPathRules,
        Expression sourceExpr,
        Expression destVar,
        MapperConfiguration config,
        ParameterExpression? contextParam,
        string? profileName,
        MappingMode mode)
    {
        var expressions = new List<Expression>();

        foreach (var rule in forPathRules)
        {
            var segments = rule.PathSegments;
            if (segments == null || segments.Length == 0) continue;

            // Zinciri oluştururken her adımda null kontrolü ve nesne oluşturma yapacağız.
            var stepExprs = new List<Expression>();
            Expression currentTarget = destVar;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                var prop = currentTarget.Type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead)
                {
                    throw new VeloxConfigurationException(
                        $"'{currentTarget.Type.FullName}' türü üzerinde '{segment}' adında okunabilir bir property bulunamadı.");
                }

                var propAccess = Expression.Property(currentTarget, prop);
                
                // Eğer bu alt nesne null ise, onu initialize et: if (currentTarget.Prop == null) currentTarget.Prop = new PropType();
                if (!prop.PropertyType.IsValueType)
                {
                    var ctor = prop.PropertyType.GetConstructor(Type.EmptyTypes);
                    if (ctor == null && prop.PropertyType != typeof(string))
                    {
                        throw new VeloxConfigurationException(
                            $"'{prop.PropertyType.FullName}' türü için parametresiz kurucu bulunamadı. ForPath ile otomatik nesne oluşturulabilmesi için parametresiz kurucu gereklidir.");
                    }

                    if (prop.CanWrite)
                    {
                        var isNull = Expression.Equal(propAccess, Expression.Constant(null, prop.PropertyType));
                        var createInstance = Expression.New(prop.PropertyType);
                        var assignNew = Expression.Assign(propAccess, createInstance);
                        var ifNullThenAssign = Expression.IfThen(isNull, assignNew);
                        stepExprs.Add(ifNullThenAssign);
                    }
                }

                currentTarget = propAccess;
            }

            // Son segment, yani değerin atanacağı yaprak property
            var leafSegment = segments[segments.Length - 1];
            var leafProp = currentTarget.Type.GetProperty(leafSegment, BindingFlags.Public | BindingFlags.Instance);
            if (leafProp == null || !leafProp.CanWrite)
            {
                throw new VeloxConfigurationException(
                    $"'{currentTarget.Type.FullName}' türü üzerinde '{leafSegment}' adında yazılabilir bir property bulunamadı.");
            }

            // Değeri MemberMappingRule kurallarına göre çöz
            var leafSourceProps = sourceExpr.Type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(config.ShouldMapProperty)
                .ToArray();
            
            // registration'ı da alalım
            var registration = config.GetRegistration(sourceExpr.Type, destVar.Type);

            var assignLeafExpr = BuildPropertyExpressionWithRules(
                sourceExpr,
                destVar,
                leafSourceProps,
                sourceExpr.Type,
                leafProp.Name,
                leafProp.PropertyType,
                registration,
                config,
                contextParam,
                mode,
                rule.MemberRule,
                currentTarget);

            if (assignLeafExpr != null)
            {
                stepExprs.Add(assignLeafExpr);
            }

            // Tüm adımları tek bir blok olarak ekle
            expressions.Add(Expression.Block(stepExprs));
        }

        return expressions;
    }

    /// <summary>
    /// Belirtilen tipin null değer alıp alamayacağını kontrol eder.
    /// </summary>
    private static bool CanBeNull(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// Nullable veya referans tipli expression'lar için null kontrol expression'ı üretir.
    /// </summary>
    private static Expression IsNullExpression(Expression step)
    {
        if (Nullable.GetUnderlyingType(step.Type) != null)
        {
            // Nullable<T> için !step.HasValue kontrolü
            return Expression.Not(Expression.Property(step, "HasValue"));
        }
        else
        {
            // Referans tipleri için step == null kontrolü
            return Expression.Equal(step, Expression.Constant(null, step.Type));
        }
    }

    /// <summary>
    /// Bir expression ağacındaki zincirleme üye erişimlerinde null olabilecek ara adımları toplar.
    /// </summary>
    private static void CollectNullableSteps(Expression? expr, List<Expression> steps)
    {
        if (expr == null) return;

        if (expr is MemberExpression memberExpr)
        {
            // Önce ata adımları rekürsif olarak incele
            CollectNullableSteps(memberExpr.Expression, steps);
            
            // Eğer doğrudan ata null olabilecek bir tip ise ve parametre değilse adımlara ekle
            if (memberExpr.Expression != null && 
                memberExpr.Expression is not ParameterExpression && 
                CanBeNull(memberExpr.Expression.Type))
            {
                // Aynı alt ifade daha önce eklenmediyse listeye ekle
                if (!steps.Any(s => s.ToString() == memberExpr.Expression.ToString()))
                {
                    steps.Add(memberExpr.Expression);
                }
            }
        }
        else if (expr is MethodCallExpression methodCallExpr)
        {
            if (methodCallExpr.Object != null)
            {
                CollectNullableSteps(methodCallExpr.Object, steps);
                if (methodCallExpr.Object is not ParameterExpression && CanBeNull(methodCallExpr.Object.Type))
                {
                    if (!steps.Any(s => s.ToString() == methodCallExpr.Object.ToString()))
                    {
                        steps.Add(methodCallExpr.Object);
                    }
                }
            }
            else if (methodCallExpr.Arguments.Count > 0)
            {
                var firstArg = methodCallExpr.Arguments[0];
                CollectNullableSteps(firstArg, steps);
                if (firstArg is not ParameterExpression && CanBeNull(firstArg.Type))
                {
                    if (!steps.Any(s => s.ToString() == firstArg.ToString()))
                    {
                        steps.Add(firstArg);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Verilen expression ağacını otomatik null propagation (güvenli üye erişimi) ile sarmalar.
    /// </summary>
    public static Expression MakeNullSafe(Expression expression)
    {
        if (expression == null) return null!;

        var steps = new List<Expression>();
        CollectNullableSteps(expression, steps);

        if (steps.Count == 0)
        {
            return expression;
        }

        var resultType = expression.Type;
        var defaultExpr = Expression.Default(resultType);
        var result = expression;

        // Adımları tersten (en yapraktan köke doğru) koşul ifadeleriyle sarmala
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            var step = steps[i];
            var isNull = IsNullExpression(step);
            result = Expression.Condition(isNull, defaultExpr, result);
        }

        return result;
    }

    public static object? GetCaseInsensitiveValue(System.Collections.IDictionary dict, string key)
    {
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }
        return null;
    }

    public static bool ContainsCaseInsensitive(System.Collections.IDictionary dict, string key)
    {
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static Expression? TryBuildDictionaryMapping(
        Expression sourceExpr, Type source, Type destination, MapperConfiguration config, ParameterExpression? contextParam, MappingMode mode)
    {
        var ctor = destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length == 0);
        if (ctor == null) return null;

        var destVar = Expression.Variable(destination, "dest");
        var blockExprs = new List<Expression>
        {
            Expression.Assign(destVar, Expression.New(ctor))
        };

        var destProps = destination.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.CanWrite && config.ShouldMapProperty(p))
            .ToArray();
        var destFields = destination.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly && config.ShouldMapField(f))
            .ToArray();

        var containsMethod = typeof(ExpressionBuilder).GetMethod(nameof(ContainsCaseInsensitive), BindingFlags.Public | BindingFlags.Static)!;
        var getValMethod = typeof(ExpressionBuilder).GetMethod(nameof(GetCaseInsensitiveValue), BindingFlags.Public | BindingFlags.Static)!;

        var castedDict = Expression.Convert(sourceExpr, typeof(System.Collections.IDictionary));

        // 1. Property'leri map et
        foreach (var destProp in destProps)
        {
            var key = destProp.Name;
            var keyExpr = Expression.Constant(key);

            var keyExists = Expression.Call(null, containsMethod, castedDict, keyExpr);
            var itemAccess = Expression.Call(null, getValMethod, castedDict, keyExpr);
            var destType = destProp.PropertyType;

            Expression valExpr;
            if (destType.IsPrimitive || destType == typeof(string) || destType == typeof(decimal) || destType.IsEnum)
            {
                if (destType.IsEnum)
                {
                    var stringToEnum = typeof(Enum).GetMethod("Parse", new[] { typeof(Type), typeof(string), typeof(bool) })!;
                    var parseCall = Expression.Call(null, stringToEnum, Expression.Constant(destType), Expression.Call(itemAccess, typeof(object).GetMethod("ToString", Type.EmptyTypes)!), Expression.Constant(true));
                    valExpr = Expression.Convert(parseCall, destType);
                }
                else
                {
                    valExpr = Expression.Convert(itemAccess, destType);
                }
            }
            else
            {
                if (contextParam != null)
                {
                    var mapperProp = typeof(VeloxResolutionContext).GetProperty("Mapper")!;
                    var mapperAccess = Expression.Property(contextParam, mapperProp);
                    var mapMethod = typeof(IVeloxMapper).GetMethods()
                        .First(m => m.Name == "Map" && 
                                    m.IsGenericMethod && 
                                    m.GetGenericArguments().Length == 1 && 
                                    m.GetParameters().Length == 1 && 
                                    m.GetParameters()[0].ParameterType == typeof(object))
                        .MakeGenericMethod(destType);

                    valExpr = Expression.Call(mapperAccess, mapMethod, itemAccess);
                }
                else
                {
                    valExpr = Expression.Convert(itemAccess, destType);
                }
            }

            var destPropAccess = Expression.Property(destVar, destProp);
            var assign = Expression.Assign(destPropAccess, valExpr);

            var conditionalAssign = Expression.IfThen(
                keyExists,
                assign
            );

            blockExprs.Add(conditionalAssign);
        }

        // 2. Field'ları map et
        foreach (var destField in destFields)
        {
            var key = destField.Name;
            var keyExpr = Expression.Constant(key);

            var keyExists = Expression.Call(null, containsMethod, castedDict, keyExpr);
            var itemAccess = Expression.Call(null, getValMethod, castedDict, keyExpr);
            var destType = destField.FieldType;

            Expression valExpr;
            if (destType.IsPrimitive || destType == typeof(string) || destType == typeof(decimal) || destType.IsEnum)
            {
                if (destType.IsEnum)
                {
                    var stringToEnum = typeof(Enum).GetMethod("Parse", new[] { typeof(Type), typeof(string), typeof(bool) })!;
                    var parseCall = Expression.Call(null, stringToEnum, Expression.Constant(destType), Expression.Call(itemAccess, typeof(object).GetMethod("ToString", Type.EmptyTypes)!), Expression.Constant(true));
                    valExpr = Expression.Convert(parseCall, destType);
                }
                else
                {
                    valExpr = Expression.Convert(itemAccess, destType);
                }
            }
            else
            {
                if (contextParam != null)
                {
                    var mapperProp = typeof(VeloxResolutionContext).GetProperty("Mapper")!;
                    var mapperAccess = Expression.Property(contextParam, mapperProp);
                    var mapMethod = typeof(IVeloxMapper).GetMethods()
                        .First(m => m.Name == "Map" && 
                                    m.IsGenericMethod && 
                                    m.GetGenericArguments().Length == 1 && 
                                    m.GetParameters().Length == 1 && 
                                    m.GetParameters()[0].ParameterType == typeof(object))
                        .MakeGenericMethod(destType);

                    valExpr = Expression.Call(mapperAccess, mapMethod, itemAccess);
                }
                else
                {
                    valExpr = Expression.Convert(itemAccess, destType);
                }
            }

            var destFieldAccess = Expression.Field(destVar, destField);
            var assign = Expression.Assign(destFieldAccess, valExpr);

            var conditionalAssign = Expression.IfThen(
                keyExists,
                assign
            );

            blockExprs.Add(conditionalAssign);
        }

        blockExprs.Add(destVar);
        return Expression.Block(new[] { destVar }, blockExprs);
    }
}
