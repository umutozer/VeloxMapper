using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;
using VeloxMapper.Caching;
using VeloxMapper.Configuration;
using VeloxMapper.Execution;
using VeloxMapper.Exceptions;

namespace VeloxMapper;

/// <summary>
/// 100% thread-safe, singleton yaşam döngüsüne sahip ana Mapper implementasyonu.
/// ConcurrentDictionary + Lazy ile lock-free önbellekleme sağlar.
/// </summary>
public sealed class Mapper : IVeloxMapper
{
    private readonly MapperConfiguration _configuration;
    private readonly IServiceProvider? _serviceProvider;

    // Strongly-typed delegate cache — DynamicInvoke yerine doğrudan Invoke kullanılır (sıfır-allocation).
    private readonly ConcurrentDictionary<MapperCacheKey, Lazy<Delegate>> _delegateCache = new();

    // ProjectTo için LambdaExpression cache — IQueryable.Select() parametresi olarak kullanılır.
    private readonly ConcurrentDictionary<MapperCacheKey, Lazy<LambdaExpression>> _expressionCache = new();

    // ProjectTo'daki reflection maliyetini düşürmek için Queryable.Select metodu ve özelleştirilmiş metot önbelleği.
    private static readonly System.Reflection.MethodInfo QueryableSelectMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == "Select" && m.GetParameters().Length == 2);

    private static readonly ConcurrentDictionary<(Type, Type), System.Reflection.MethodInfo> _selectMethodCache = new();

    // ThreadStatic ResolutionContext Havuzu (GC allocation ve GC duraksamalarını sıfırlamak için)
    [ThreadStatic]
    private static Stack<VeloxResolutionContext>? _contextPool;

    /// <summary>
    /// Thread-local havuzdan sıfırlanmış bir context döndürür.
    /// </summary>
    private VeloxResolutionContext AcquireContext()
    {
        _contextPool ??= new Stack<VeloxResolutionContext>();
        if (_contextPool.Count == 0)
        {
            return new VeloxResolutionContext(this, _serviceProvider);
        }
        var context = _contextPool.Pop();
        context.Reset(this, _serviceProvider);
        return context;
    }

    /// <summary>
    /// Kullanılan context'i thread-local havuza geri kazandırır.
    /// </summary>
    private void ReleaseContext(VeloxResolutionContext context)
    {
        _contextPool?.Push(context);
    }

    /// <summary>
    /// Yeni bir <see cref="Mapper"/> örneği oluşturur.
    /// </summary>
    /// <param name="configuration">Mapper yapılandırması</param>
    /// <param name="serviceProvider">DI servis sağlayıcısı (isteğe bağlı)</param>
    public Mapper(MapperConfiguration configuration, IServiceProvider? serviceProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public TDestination Map<TDestination>(object source)
    {
        if (source == null)
        {
            return HandleNullSource<TDestination>();
        }

        var sourceType = GetUnproxiedType(source.GetType());
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Map);

        if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
        {
            lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                new Lazy<Delegate>(() =>
                    ExpressionBuilder.BuildMapDelegate(k.Source, k.Destination, cfg)),
                _configuration
            );
        }

        var del = lazyDelegate.Value;
        if (del is Func<object, object> simpleFunc)
        {
            return (TDestination)simpleFunc(source);
        }
        else if (del is Func<object, VeloxResolutionContext, object> contextFunc)
        {
            var context = AcquireContext();
            try
            {
                return (TDestination)contextFunc(source, context);
            }
            catch (Exception ex)
            {
                if (ex is VeloxMappingException) throw;
                throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        throw new InvalidOperationException($"Desteklenmeyen delegate tipi: {del?.GetType()}");
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source == null)
        {
            return HandleNullSource<TDestination>();
        }

        var sourceType = GetUnproxiedType(typeof(TSource));
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Map);

        if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
        {
            lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
            {
                var precompiled = cfg.GetPrecompiledMapper(k.Source, k.Destination);
                if (precompiled != null)
                {
                    return new Lazy<Delegate>(() => precompiled);
                }
                return new Lazy<Delegate>(() =>
                    ExpressionBuilder.BuildStronglyTypedMapDelegate(k.Source, k.Destination, cfg));
            },
            _configuration
            );
        }

        var del = lazyDelegate.Value;
        if (del is Func<TSource, TDestination> simpleFunc)
        {
            return simpleFunc(source);
        }
        else if (del is Func<TSource, VeloxResolutionContext, TDestination> contextFunc)
        {
            var context = AcquireContext();
            try
            {
                return contextFunc(source, context);
            }
            catch (Exception ex)
            {
                if (ex is VeloxMappingException) throw;
                throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        throw new InvalidOperationException($"Desteklenmeyen delegate tipi: {del?.GetType()}");
    }

    /// <inheritdoc />
    public void Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var sourceType = GetUnproxiedType(typeof(TSource));
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Patch);

        if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
        {
            lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                new Lazy<Delegate>(() =>
                    ExpressionBuilder.BuildPatchDelegate(k.Source, k.Destination, cfg)),
                _configuration
            );
        }

        var del = lazyDelegate.Value;
        if (del is Action<TSource, TDestination> simpleAction)
        {
            simpleAction(source, destination);
        }
        else if (del is Action<TSource, TDestination, VeloxResolutionContext> contextAction)
        {
            var context = AcquireContext();
            try
            {
                contextAction(source, destination, context);
            }
            catch (Exception ex)
            {
                if (ex is VeloxMappingException) throw;
                throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
            }
            finally
            {
                ReleaseContext(context);
            }
        }
        else
        {
            throw new InvalidOperationException($"Desteklenmeyen delegate tipi: {del?.GetType()}");
        }
    }



    /// <inheritdoc />
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sourceType = GetUnproxiedType(source.ElementType);
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.ProjectTo);

        if (!_expressionCache.TryGetValue(key, out var lazyExpression))
        {
            lazyExpression = _expressionCache.GetOrAdd(key, static (k, cfg) =>
                new Lazy<LambdaExpression>(() =>
                    ExpressionBuilder.BuildProjectToExpression(k.Source, k.Destination, cfg)),
                _configuration
            );
        }

        // Queryable.Select<TSource, TDest>(source, expression) metodunu önbellekten alarak reflection yükünü sıfırlıyoruz.
        var selectMethod = _selectMethodCache.GetOrAdd((sourceType, destType), static types =>
            QueryableSelectMethod.MakeGenericMethod(types.Item1, types.Item2));

        var projected = (IQueryable<TDestination>)selectMethod.Invoke(null, [source, lazyExpression.Value])!;
        return projected;
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, Action<VeloxResolutionContext> contextConfig)
    {
        ArgumentNullException.ThrowIfNull(contextConfig);

        if (source == null)
        {
            return HandleNullSource<TDestination>();
        }

        var sourceType = GetUnproxiedType(typeof(TSource));
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Map);

        var context = AcquireContext();
        try
        {
            contextConfig(context);

            if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
            {
                lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                    new Lazy<Delegate>(() =>
                        ExpressionBuilder.BuildStronglyTypedMapDelegate(k.Source, k.Destination, cfg)),
                    _configuration
                );
            }

            var del = lazyDelegate.Value;
            if (del is Func<TSource, VeloxResolutionContext, TDestination> contextFunc)
            {
                return contextFunc(source, context);
            }
            else if (del is Func<TSource, TDestination> simpleFunc)
            {
                return simpleFunc(source);
            }
        }
        catch (Exception ex)
        {
            if (ex is VeloxMappingException) throw;
            throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
        }
        finally
        {
            ReleaseContext(context);
        }

        throw new InvalidOperationException($"Desteklenmeyen delegate tipi.");
    }

    /// <inheritdoc />
    public void Map<TSource, TDestination>(TSource source, TDestination destination, Action<VeloxResolutionContext> contextConfig)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(contextConfig);

        var sourceType = GetUnproxiedType(typeof(TSource));
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Patch);

        var context = AcquireContext();
        try
        {
            contextConfig(context);

            if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
            {
                lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                    new Lazy<Delegate>(() =>
                        ExpressionBuilder.BuildPatchDelegate(k.Source, k.Destination, cfg)),
                    _configuration
                );
            }

            var del = lazyDelegate.Value;
            if (del is Action<TSource, TDestination, VeloxResolutionContext> contextAction)
            {
                contextAction(source, destination, context);
            }
            else if (del is Action<TSource, TDestination> simpleAction)
            {
                simpleAction(source, destination);
            }
            else
            {
                throw new InvalidOperationException($"Desteklenmeyen delegate tipi: {del?.GetType()}");
            }
        }
        catch (Exception ex)
        {
            if (ex is VeloxMappingException) throw;
            throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
        }
        finally
        {
            ReleaseContext(context);
        }
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, VeloxResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source == null)
        {
            return HandleNullSource<TDestination>();
        }

        var sourceType = GetUnproxiedType(typeof(TSource));
        var destType = GetUnproxiedType(typeof(TDestination));
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Map);

        if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
        {
            lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                new Lazy<Delegate>(() =>
                    ExpressionBuilder.BuildStronglyTypedMapDelegate(k.Source, k.Destination, cfg)),
                _configuration
            );
        }

        var del = lazyDelegate.Value;
        try
        {
            if (del is Func<TSource, VeloxResolutionContext, TDestination> contextFunc)
            {
                return contextFunc(source, context);
            }
            else if (del is Func<TSource, TDestination> simpleFunc)
            {
                return simpleFunc(source);
            }
        }
        catch (Exception ex)
        {
            if (ex is VeloxMappingException) throw;
            throw new VeloxMappingException($"Haritalama sırasında hata oluştu. Hata noktası: {context.CurrentMember ?? "Kök"}", ex);
        }

        throw new InvalidOperationException($"Desteklenmeyen delegate tipi: {del?.GetType()}");
    }

    /// <summary>
    /// Dahili kullanım için: Belirtilen bağlam nesnesini (ResolutionContext) kullanarak kaynak verilerini var olan hedef nesne üzerine yazar.
    /// </summary>
    internal void Map(object source, object destination, VeloxResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(context);

        var sourceType = GetUnproxiedType(source.GetType());
        var destType = GetUnproxiedType(destination.GetType());
        var key = new MapperCacheKey(sourceType, destType, MappingMode.Patch);

        if (!_delegateCache.TryGetValue(key, out var lazyDelegate))
        {
            lazyDelegate = _delegateCache.GetOrAdd(key, static (k, cfg) =>
                new Lazy<Delegate>(() =>
                    ExpressionBuilder.BuildPatchDelegate(k.Source, k.Destination, cfg)),
                _configuration
            );
        }

        var del = lazyDelegate.Value;
        
        if (del is Action<object, object, VeloxResolutionContext> genericContextAction)
        {
            genericContextAction(source, destination, context);
        }
        else
        {
            var delType = del.GetType();
            var isThreeParam = delType.IsGenericType && delType.GetGenericTypeDefinition() == typeof(Action<,,>);
            if (isThreeParam)
            {
                del.DynamicInvoke(source, destination, context);
            }
            else
            {
                del.DynamicInvoke(source, destination);
            }
        }
    }

    /// <summary>
    /// Kaynak nesnenin null olduğu durumları yönetir ve hedef türe göre varsayılan/boş değer döner.
    /// </summary>
    /// <typeparam name="TDestination">Hedef haritalama türü</typeparam>
    /// <returns>Hedef tür için null, varsayılan değer veya boş bir koleksiyon</returns>
    private TDestination HandleNullSource<TDestination>()
    {
        var destType = typeof(TDestination);
        
        // Eğer hedef tip koleksiyon ise ve null koleksiyonlara izin verilmiyorsa boş koleksiyon oluşturulur.
        if (Execution.CollectionExpressionHelper.IsCollectionType(destType))
        {
            if (!_configuration.AllowNullCollections)
            {
                return (TDestination)Execution.CollectionExpressionHelper.CreateEmptyCollection(destType);
            }
        }
        
        return default!;
    }

    /// <summary>
    /// Castle DynamicProxy ve EF Core Lazy Loading proxy sınıflarını algılayıp
    /// bunları sarmalayan gerçek ata sınıfı (POCO entity) geri döndürür.
    /// </summary>
    public static Type GetUnproxiedType(Type type)
    {
        if (type == null) return null!;
        
        // Proxy tip tespiti: Castle proxy'leri veya EF proxy'leri genellikle "Proxy" ile biter veya adında barındırır.
        if (type.Namespace == "System.Data.Entity.DynamicProxies" ||
            type.Namespace == "Castle.Proxies" ||
            type.FullName?.StartsWith("Castle.Proxies.") == true ||
            type.FullName?.Contains(".Proxies.") == true ||
            type.Name.EndsWith("Proxy"))
        {
            return type.BaseType ?? type;
        }

        return type;
    }
}
