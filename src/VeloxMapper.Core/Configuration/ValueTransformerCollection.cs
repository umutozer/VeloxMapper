using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Belirli bir türdeki tüm hedef değerlere otomatik uygulanan dönüşümlerin (value transformer) listesi.
/// AutoMapper'ın <c>ValueTransformers.Add&lt;string&gt;(val =&gt; val.Trim())</c> kullanımı ile uyumludur.
/// </summary>
public sealed class ValueTransformerCollection
{
    private readonly List<KeyValuePair<Type, LambdaExpression>> _transformers = new();

    /// <summary>
    /// Verilen türdeki (ve bu türe atanabilir) hedef üyelere uygulanacak bir dönüşüm ekler.
    /// Dönüşümler eklenme sırasıyla zincirlenir.
    /// </summary>
    /// <typeparam name="TValue">Dönüştürülecek değerin türü.</typeparam>
    /// <param name="transformer">Dönüşüm ifadesi, ör. <c>val =&gt; val.Trim()</c>.</param>
    public void Add<TValue>(Expression<Func<TValue, TValue>> transformer)
    {
        if (transformer == null) throw new ArgumentNullException(nameof(transformer));
        _transformers.Add(new(typeof(TValue), transformer));
    }

    /// <summary>Kayıtlı dönüşüm sayısı.</summary>
    public int Count => _transformers.Count;

    /// <summary>
    /// Verilen hedef türe uygulanabilir dönüşümleri eklenme sırasıyla döndürür.
    /// </summary>
    internal IEnumerable<LambdaExpression> GetTransformers(Type destinationType)
    {
        foreach (var kvp in _transformers)
        {
            if (kvp.Key.IsAssignableFrom(destinationType)) yield return kvp.Value;
        }
    }
}
