// Polyfill for record support in .NET Standard 2.0

#if NETSTANDARD2_0 || NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
#endif
