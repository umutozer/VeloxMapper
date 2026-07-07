using System;
using System.Text;
using VeloxMapper.Abstractions;

namespace VeloxMapper
{
    /// <summary>
    /// PascalCase isimlendirme kuralı.
    /// AutoMapper'ın <c>PascalCaseNamingConvention</c> ile birebir uyumludur.
    /// </summary>
    public sealed class PascalCaseNamingConvention : ICustomNamingConvention
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static PascalCaseNamingConvention Instance { get; } = new();

        /// <summary>
        /// İsmi normalize eder.
        /// </summary>
        public string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace(" ", "");
        }
    }

    /// <summary>
    /// snake_case (lower_underscore) isimlendirme kuralı.
    /// AutoMapper'ın <c>LowerUnderscoreNamingConvention</c> ile birebir uyumludur.
    /// </summary>
    public sealed class LowerUnderscoreNamingConvention : ICustomNamingConvention
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static LowerUnderscoreNamingConvention Instance { get; } = new();

        /// <summary>
        /// snake_case formatındaki ismi PascalCase'e normalize eder.
        /// </summary>
        public string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            
            var parts = name.Split('_');
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                        sb.Append(part.Substring(1));
                }
            }
            return sb.ToString();
        }
    }
}
