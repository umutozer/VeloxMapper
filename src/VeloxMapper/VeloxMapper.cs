using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;
using VeloxMapper.Execution;

namespace VeloxMapper;

/// <summary>
/// <see cref="IMapper"/> implementasyonu. AutoMapper'daki <c>new Mapper(configuration)</c> ile aynı şekilde oluşturulur.
/// Derlenmiş eşleştirmeler <see cref="MapperConfiguration"/> üzerinde paylaşıldığından örnek oluşturmak ucuzdur ve thread-safe'dir.
/// </summary>
public sealed class Mapper : IVeloxMapper
{
    [ThreadStatic]
    private static Stack<VeloxResolutionContext>? _contextPool;

    private readonly MapperConfiguration _configuration;
    private readonly IServiceProvider? _serviceProvider;
    private readonly Func<Type, object>? _serviceCtor;

    /// <summary>
    /// Yeni bir mapper oluşturur.
    /// </summary>
    /// <param name="configurationProvider">Yapılandırma (<see cref="MapperConfiguration"/>).</param>
    /// <param name="serviceProvider">Resolver/converter/action örneklerini çözmek için servis sağlayıcı (isteğe bağlı).</param>
    public Mapper(IConfigurationProvider configurationProvider, IServiceProvider? serviceProvider = null)
    {
        _configuration = configurationProvider as MapperConfiguration
            ?? throw new ArgumentException($"Yapılandırma bir {nameof(MapperConfiguration)} olmalıdır.", nameof(configurationProvider));
        _serviceProvider = serviceProvider;
        _serviceCtor = _configuration.ServiceCtor;
    }

    /// <summary>
    /// Resolver/converter/action örneklerini verilen fabrika ile oluşturan yeni bir mapper oluşturur.
    /// </summary>
    /// <param name="configurationProvider">Yapılandırma.</param>
    /// <param name="serviceCtor">Tip alıp örnek döndüren fabrika.</param>
    public Mapper(IConfigurationProvider configurationProvider, Func<Type, object> serviceCtor)
        : this(configurationProvider, (IServiceProvider?)null)
    {
        _serviceCtor = serviceCtor ?? throw new ArgumentNullException(nameof(serviceCtor));
    }

    /// <inheritdoc />
    public IConfigurationProvider ConfigurationProvider => _configuration;

    // ─── Map (yeni nesne) ───────────────────────────────────────────────────

    /// <inheritdoc />
    public TDestination Map<TDestination>(object? source) => MapUntyped<TDestination>(source, null);

    /// <inheritdoc />
    public TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions<object, TDestination>> opts)
    {
        if (opts == null) throw new ArgumentNullException(nameof(opts));
        var options = new MappingOperationOptions<object, TDestination>();
        opts(options);
        return MapUntyped(source, options);
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source) => Execute<TSource, TDestination>(source, default!, false, null);

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        if (opts == null) throw new ArgumentNullException(nameof(opts));
        var options = new MappingOperationOptions<TSource, TDestination>();
        opts(options);
        return Execute<TSource, TDestination>(source, default!, false, options);
    }

    // ─── Map (mevcut nesneye) ───────────────────────────────────────────────

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) => Execute(source, destination, true, null);

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        if (opts == null) throw new ArgumentNullException(nameof(opts));
        var options = new MappingOperationOptions<TSource, TDestination>();
        opts(options);
        return Execute(source, destination, true, options);
    }

    // ─── Map (çalışma zamanı türleri) ───────────────────────────────────────

    /// <inheritdoc />
    public object Map(object? source, Type sourceType, Type destinationType) => MapUntyped(source, null, false, sourceType, destinationType, null)!;

    /// <inheritdoc />
    public object Map(object? source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
        => MapUntyped(source, null, false, sourceType, destinationType, BuildOptions(opts))!;

    /// <inheritdoc />
    public object Map(object? source, object? destination, Type sourceType, Type destinationType)
        => MapUntyped(source, destination, true, sourceType, destinationType, null)!;

    /// <inheritdoc />
    public object Map(object? source, object? destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
        => MapUntyped(source, destination, true, sourceType, destinationType, BuildOptions(opts))!;

    private static MappingOperationOptions<object, object> BuildOptions(Action<IMappingOperationOptions<object, object>> opts)
    {
        if (opts == null) throw new ArgumentNullException(nameof(opts));
        var options = new MappingOperationOptions<object, object>();
        opts(options);
        return options;
    }

    // ─── ProjectTo ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object? parameters = null, params Expression<Func<TDestination, object?>>[] membersToExpand)
        => (IQueryable<TDestination>)_configuration.Project(source, typeof(TDestination), ProjectionParameters.FromObject(parameters), ProjectionParameters.Paths(membersToExpand));

    /// <inheritdoc />
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand)
        => (IQueryable<TDestination>)_configuration.Project(source, typeof(TDestination), parameters, membersToExpand);

    /// <inheritdoc />
    public IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object>? parameters = null, params string[] membersToExpand)
        => _configuration.Project(source, destinationType, parameters, membersToExpand);

    // ─── Çekirdek ───────────────────────────────────────────────────────────

    private TDestination Execute<TSource, TDestination>(TSource source, TDestination destination, bool hasDestination, MappingOperationOptions<TSource, TDestination>? options)
    {
        var context = Acquire(options);
        try
        {
            options?.RunBeforeMap(source, destination);
            var result = MapCore(source, destination, hasDestination, context, null, isRoot: true);
            options?.RunAfterMap(source, result);
            return result;
        }
        catch (Exception ex) when (ex is not VeloxException)
        {
            throw MappingError(typeof(TSource), typeof(TDestination), context, ex);
        }
        finally
        {
            Release(context);
        }
    }

    private TDestination MapUntyped<TDestination>(object? source, MappingOperationOptions<object, TDestination>? options)
    {
        if (source == null)
        {
            var nullResult = MapUntyped(null, null, false, typeof(object), typeof(TDestination), options);
            return nullResult == null ? default! : (TDestination)nullResult;
        }

        var sourceType = GetUnproxiedType(source.GetType());
        var result = MapUntyped(source, null, false, sourceType, typeof(TDestination), options);
        return result == null ? default! : (TDestination)result;
    }

    private object? MapUntyped(object? source, object? destination, bool hasDestination, Type sourceType, Type destinationType, IMappingOperationOptionsInternal? options)
    {
        if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

        var context = Acquire(options);
        try
        {
            options?.RunBeforeMap(source, destination);
            object? result;
            var registration = _configuration.FindRegistration(sourceType, destinationType);
            if (source == null && registration?.HasTypeConverter != true)
            {
                result = hasDestination && destination != null ? destination : NullValue(destinationType, _configuration.GetProfile(registration), null);
            }
            else if (hasDestination && destination != null)
            {
                result = _configuration.GetUntypedPatchDelegate(sourceType, destinationType)(source!, destination, context);
            }
            else
            {
                result = _configuration.GetUntypedMapDelegate(sourceType, destinationType)(source!, context);
            }

            options?.RunAfterMap(source, result);
            return result;
        }
        catch (Exception ex) when (ex is not VeloxException)
        {
            throw MappingError(sourceType, destinationType, context, ex);
        }
        finally
        {
            Release(context);
        }
    }

    /// <summary>
    /// Kök ve iç içe çağrılar için ortak eşleştirme: null kaynak, tip dönüştürücü ve mevcut hedef kurallarını uygular.
    /// </summary>
    internal TDestination MapCore<TSource, TDestination>(TSource source, TDestination destination, bool hasDestination, VeloxResolutionContext context, bool? allowNullDestination, bool isRoot)
    {
        if (source == null)
        {
            var registration = _configuration.FindRegistration(typeof(TSource), typeof(TDestination));
            if (registration?.HasTypeConverter == true)
            {
                return hasDestination && destination != null
                    ? _configuration.GetPatchDelegate<TSource, TDestination>()(source!, destination, context)
                    : _configuration.GetMapDelegate<TSource, TDestination>()(source!, context);
            }

            if (isRoot && hasDestination && destination != null) return destination;
            var value = NullValue(typeof(TDestination), _configuration.GetProfile(registration), allowNullDestination);
            return value == null ? default! : (TDestination)value;
        }

        return hasDestination && destination != null
            ? _configuration.GetPatchDelegate<TSource, TDestination>()(source, destination, context)
            : _configuration.GetMapDelegate<TSource, TDestination>()(source, context);
    }

    /// <summary>
    /// Üretilen ifadelerin iç içe karmaşık türler için çağırdığı giriş noktası (aynı bağlam kullanılır).
    /// </summary>
    internal static TDestination MapNested<TSource, TDestination>(TSource source, TDestination destination, bool hasDestination, VeloxResolutionContext context, bool allowNullDestination)
        => ((Mapper)context.Mapper).MapCore(source, destination, hasDestination, context, allowNullDestination, isRoot: false);

    /// <summary>
    /// Kaynak türü çalışma zamanında belirlenen iç içe eşleştirme (sözlük → nesne).
    /// </summary>
    internal TDestination MapRuntime<TDestination>(object source, VeloxResolutionContext context)
    {
        var result = _configuration.GetUntypedMapDelegate(GetUnproxiedType(source.GetType()), typeof(TDestination))(source, context);
        return (TDestination)result;
    }

    private object? NullValue(Type destinationType, ProfileMap profile, bool? allowNullDestination)
    {
        if (CollectionExpressionHelper.IsCollectionType(destinationType))
        {
            return profile.AllowNullCollections ? null : CollectionExpressionHelper.CreateEmptyCollection(destinationType);
        }

        if (destinationType.IsValueType) return Activator.CreateInstance(destinationType);

        var allowNull = allowNullDestination ?? profile.AllowNullDestinationValues;
        if (!allowNull && ExpressionBuilder.IsComplex(destinationType) && !destinationType.IsAbstract && destinationType.GetConstructor(Type.EmptyTypes) != null)
        {
            return Activator.CreateInstance(destinationType);
        }

        return null;
    }

    // ─── Bağlam havuzu ──────────────────────────────────────────────────────

    private VeloxResolutionContext Acquire(IMappingOperationOptionsInternal? options)
    {
        var pool = _contextPool ??= new Stack<VeloxResolutionContext>();
        VeloxResolutionContext context;
        if (pool.Count > 0)
        {
            context = pool.Pop();
            context.Reset(this, _serviceProvider);
        }
        else
        {
            context = new VeloxResolutionContext(this, _serviceProvider);
        }

        context.ServiceCtor = options?.ServiceCtor ?? _serviceCtor;
        if (options != null)
        {
            context.SetItems(options.ItemsOrNull);
            context.State = options.State;
        }

        return context;
    }

    private static void Release(VeloxResolutionContext context)
    {
        context.Reset(context.Mapper, null);
        var pool = _contextPool ??= new Stack<VeloxResolutionContext>();
        if (pool.Count < 16) pool.Push(context);
    }

    private static VeloxMappingException MappingError(Type sourceType, Type destinationType, ResolutionContext context, Exception inner)
        => new($"Eşleştirme hatası: {sourceType.Name} -> {destinationType.Name}" +
               (context.CurrentMember != null ? $" (üye: {context.CurrentMember})" : string.Empty) +
               $". {inner.GetType().Name}: {inner.Message}", inner);

    /// <summary>
    /// EF Core lazy-loading ve Castle DynamicProxy proxy türlerini gerçek varlık türüne çözer.
    /// Yalnızca bilinen proxy ad alanları dikkate alınır; adı "Proxy" ile biten normal sınıflar etkilenmez.
    /// </summary>
    /// <param name="type">Tür.</param>
    /// <returns>Proxy ise taban tür, değilse türün kendisi.</returns>
    public static Type GetUnproxiedType(Type type)
    {
        if (type == null) return null!;
        var ns = type.Namespace;
        if (type.BaseType != null && type.BaseType != typeof(object) &&
            (ns == "Castle.Proxies" || ns == "System.Data.Entity.DynamicProxies"))
        {
            return type.BaseType;
        }

        return type;
    }
}

/// <summary>
/// <see cref="IMappingOperationOptions{TSource,TDestination}"/> implementasyonu.
/// </summary>
internal sealed class MappingOperationOptions<TSource, TDestination> : IMappingOperationOptions<TSource, TDestination>, IMappingOperationOptionsInternal
{
    private IDictionary<string, object>? _items;
    private Action<TSource, TDestination>? _before;
    private Action<TSource, TDestination>? _after;

    public IDictionary<string, object> Items => _items ??= new Dictionary<string, object>();

    public IDictionary<string, object>? ItemsOrNull => _items;

    public object? State { get; set; }

    public Func<Type, object>? ServiceCtor { get; private set; }

    public void ConstructServicesUsing(Func<Type, object> constructor) => ServiceCtor = constructor ?? throw new ArgumentNullException(nameof(constructor));

    public void BeforeMap(Action<TSource, TDestination> beforeFunction) => _before += beforeFunction ?? throw new ArgumentNullException(nameof(beforeFunction));

    public void AfterMap(Action<TSource, TDestination> afterFunction) => _after += afterFunction ?? throw new ArgumentNullException(nameof(afterFunction));

    public void RunBeforeMap(TSource source, TDestination destination) => _before?.Invoke(source, destination);

    public void RunAfterMap(TSource source, TDestination destination) => _after?.Invoke(source, destination);

    void IMappingOperationOptionsInternal.RunBeforeMap(object? source, object? destination) => _before?.Invoke((TSource)source!, (TDestination)destination!);

    void IMappingOperationOptionsInternal.RunAfterMap(object? source, object? destination) => _after?.Invoke((TSource)source!, (TDestination)destination!);
}

/// <summary>Seçeneklere tür bağımsız erişim.</summary>
internal interface IMappingOperationOptionsInternal
{
    IDictionary<string, object>? ItemsOrNull { get; }
    object? State { get; }
    Func<Type, object>? ServiceCtor { get; }
    void RunBeforeMap(object? source, object? destination);
    void RunAfterMap(object? source, object? destination);
}

/// <summary>
/// ProjectTo parametre ve genişletme yardımcıları.
/// </summary>
internal static class ProjectionParameters
{
    /// <summary>Anonim nesnenin property'lerini sözlüğe çevirir.</summary>
    internal static IDictionary<string, object>? FromObject(object? parameters)
    {
        if (parameters == null) return null;
        if (parameters is IDictionary<string, object> dictionary) return dictionary;
        return parameters.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(parameters)!);
    }

    /// <summary><c>d =&gt; d.Customer.Name</c> veya <c>d =&gt; d.Items.Select(i =&gt; i.Product)</c> ifadelerini noktalı yollara çevirir.</summary>
    internal static IEnumerable<string> Paths<TDestination>(Expression<Func<TDestination, object?>>[]? expressions)
    {
        if (expressions == null) yield break;
        foreach (var expression in expressions)
        {
            if (expression == null) continue;
            var path = PathOf(expression.Body);
            if (!string.IsNullOrEmpty(path)) yield return path!;
        }
    }

    private static string? PathOf(Expression expression)
    {
        expression = ExpressionUtil.StripConvert(expression);
        switch (expression)
        {
            case MemberExpression member:
                var parent = member.Expression == null ? null : PathOf(member.Expression);
                return string.IsNullOrEmpty(parent) ? member.Member.Name : parent + "." + member.Member.Name;
            case MethodCallExpression call when call.Method.Name == nameof(Enumerable.Select) && call.Arguments.Count == 2:
                var collection = PathOf(call.Arguments[0]);
                var selector = (LambdaExpression)ExpressionUtil.StripConvert(call.Arguments[1] is UnaryExpression quote && quote.NodeType == ExpressionType.Quote ? quote.Operand : call.Arguments[1]);
                var inner = PathOf(selector.Body);
                return string.IsNullOrEmpty(inner) ? collection : collection + "." + inner;
            default:
                return null;
        }
    }
}
