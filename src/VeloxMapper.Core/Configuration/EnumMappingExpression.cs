using System;
using System.Collections.Generic;

namespace VeloxMapper
{
    /// <summary>
    /// Enum'lar arası eşleştirmeyi özelleştirmek için kullanılan yapılandırma ifadesi.
    /// </summary>
    public sealed class EnumMappingExpression<TSource, TDestination>
    {
        internal bool MapByNameRequested { get; private set; }

        // CS8714: TSource, ctor içinde IsEnum ile doğrulanır; enum'lar değer tipidir ve asla null olamaz,
        // dolayısıyla Dictionary'nin 'notnull' TKey kısıtı çalışma zamanında her zaman karşılanır.
#pragma warning disable CS8714
        internal Dictionary<TSource, TDestination> ValueOverrides { get; } = new();
#pragma warning restore CS8714

        /// <summary>
        /// EnumMappingExpression için yeni bir örnek oluşturur.
        /// </summary>
        public EnumMappingExpression()
        {
            if (!typeof(TSource).IsEnum || !typeof(TDestination).IsEnum)
            {
                throw new ArgumentException("Both TSource and TDestination must be Enum types.");
            }
        }

        /// <summary>
        /// Enum üyelerini isimlerine göre eşleştirir.
        /// </summary>
        public EnumMappingExpression<TSource, TDestination> MapByName()
        {
            MapByNameRequested = true;
            return this;
        }

        /// <summary>
        /// Belirli bir kaynak enum değerini belirli bir hedef enum değerine eşler.
        /// </summary>
        public EnumMappingExpression<TSource, TDestination> MapValue(TSource sourceValue, TDestination destinationValue)
        {
            ValueOverrides[sourceValue] = destinationValue;
            return this;
        }
    }
}
