using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Belirli bir tipe ait tüm değerlere otomatik dönüşüm uygulayan transformer (dönüştürücü) koleksiyonu.
/// </summary>
public sealed class ValueTransformerCollection
{
    private readonly Dictionary<Type, List<Delegate>> _transformers = new();

    /// <summary>
    /// Belirli bir tip için transformer (dönüştürücü) fonksiyonu ekler.
    /// </summary>
    /// <typeparam name="TValue">Dönüştürülecek değerin tipi</typeparam>
    /// <param name="transformer">Dönüştürücü fonksiyon</param>
    public void Add<TValue>(Func<TValue, TValue> transformer)
    {
        if (transformer == null) throw new ArgumentNullException(nameof(transformer));

        var type = typeof(TValue);
        if (!_transformers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _transformers[type] = list;
        }
        list.Add(transformer);
    }

    /// <summary>
    /// Verilen tip için kayıtlı olan tüm transformer delegelerini sırayla çalıştıran tek bir delege döndürür.
    /// Kayıt yoksa null döner.
    /// </summary>
    internal Delegate? GetTransformer(Type type)
    {
        if (!_transformers.TryGetValue(type, out var list) || list.Count == 0)
        {
            return null;
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        return BuildChainedTransformer(type, list);
    }

    /// <summary>
    /// Kayıtlı tüm transformer'ları zincirleyerek tek bir delege haline getirir.
    /// </summary>
    private static Delegate BuildChainedTransformer(Type type, List<Delegate> list)
    {
        var param = Expression.Parameter(type, "val");
        Expression current = param;

        foreach (var transformer in list)
        {
            var transformerConst = Expression.Constant(transformer);
            current = Expression.Invoke(transformerConst, current);
        }

        var funcType = typeof(Func<,>).MakeGenericType(type, type);
        var lambda = Expression.Lambda(funcType, current, param);
        return lambda.Compile();
    }
}
