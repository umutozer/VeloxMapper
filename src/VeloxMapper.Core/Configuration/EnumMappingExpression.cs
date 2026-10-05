using System;
using System.Collections.Generic;
using VeloxMapper.Configuration;

namespace VeloxMapper
{
    /// <summary>
    /// Enum'dan enum'a eşleştirmeyi özelleştirir. AutoMapper.Extensions.EnumMapping paketindeki
    /// <c>ConvertUsingEnumMapping(opt =&gt; opt.MapByName().MapValue(...))</c> API'si ile uyumludur.
    /// </summary>
    /// <typeparam name="TSource">Kaynak enum.</typeparam>
    /// <typeparam name="TDestination">Hedef enum.</typeparam>
    public sealed class EnumMappingExpression<TSource, TDestination>
    {
        private readonly Dictionary<object, object> _overrides = new();
        private bool _byName;
        private bool _ignoreCase;

        /// <summary>
        /// Yeni bir enum eşleştirme yapılandırması oluşturur.
        /// </summary>
        /// <exception cref="ArgumentException">Türlerden biri enum değilse.</exception>
        public EnumMappingExpression()
        {
            var sourceEnum = Nullable.GetUnderlyingType(typeof(TSource)) ?? typeof(TSource);
            var destEnum = Nullable.GetUnderlyingType(typeof(TDestination)) ?? typeof(TDestination);
            if (!sourceEnum.IsEnum || !destEnum.IsEnum)
            {
                throw new ArgumentException("ConvertUsingEnumMapping yalnızca enum türleri arasında kullanılabilir.");
            }
        }

        /// <summary>
        /// Enum üyelerini isme göre eşler (eşleşmeyen değerler sayısal değerle eşlenir).
        /// </summary>
        /// <param name="ignoreCase">İsim karşılaştırmasında büyük/küçük harf yok sayılsın mı.</param>
        public EnumMappingExpression<TSource, TDestination> MapByName(bool ignoreCase = false)
        {
            _byName = true;
            _ignoreCase = ignoreCase;
            return this;
        }

        /// <summary>
        /// Enum üyelerini sayısal değere göre eşler (varsayılan).
        /// </summary>
        public EnumMappingExpression<TSource, TDestination> MapByValue()
        {
            _byName = false;
            return this;
        }

        /// <summary>
        /// Belirli bir kaynak değerini belirli bir hedef değere eşler.
        /// </summary>
        /// <param name="source">Kaynak değer.</param>
        /// <param name="destination">Hedef değer.</param>
        public EnumMappingExpression<TSource, TDestination> MapValue(TSource source, TDestination destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _overrides[source] = destination!;
            return this;
        }

        internal EnumMappingSnapshot ToSnapshot()
            => new(Nullable.GetUnderlyingType(typeof(TSource)) ?? typeof(TSource),
                   Nullable.GetUnderlyingType(typeof(TDestination)) ?? typeof(TDestination),
                   _byName, _ignoreCase, new Dictionary<object, object>(_overrides));
    }
}

namespace VeloxMapper.Configuration
{
    /// <summary>
    /// Dondurulmuş enum eşleştirme kuralları; dönüştürme ve ters çevirme (ReverseMap) için kullanılır.
    /// </summary>
    internal sealed class EnumMappingSnapshot
    {
        private readonly Dictionary<object, object> _overrides;

        internal EnumMappingSnapshot(Type sourceEnum, Type destinationEnum, bool byName, bool ignoreCase, Dictionary<object, object> overrides)
        {
            SourceEnum = sourceEnum;
            DestinationEnum = destinationEnum;
            ByName = byName;
            IgnoreCase = ignoreCase;
            _overrides = overrides;
        }

        internal Type SourceEnum { get; }
        internal Type DestinationEnum { get; }
        internal bool ByName { get; }
        internal bool IgnoreCase { get; }

        internal object Convert(object source)
        {
            if (_overrides.TryGetValue(source, out var overridden)) return overridden;

            if (ByName)
            {
                var name = Enum.GetName(SourceEnum, source);
                if (name != null)
                {
                    var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    foreach (var candidate in Enum.GetNames(DestinationEnum))
                    {
                        if (string.Equals(candidate, name, comparison)) return Enum.Parse(DestinationEnum, candidate);
                    }
                }
            }

            return Enum.ToObject(DestinationEnum, System.Convert.ToInt64(source, System.Globalization.CultureInfo.InvariantCulture));
        }

        internal EnumMappingSnapshot Reverse()
        {
            var reversed = new Dictionary<object, object>();
            foreach (var kvp in _overrides)
            {
                if (!reversed.ContainsKey(kvp.Value)) reversed[kvp.Value] = kvp.Key;
            }

            return new EnumMappingSnapshot(DestinationEnum, SourceEnum, ByName, IgnoreCase, reversed);
        }
    }
}
