namespace VeloxMapper
{
    /// <summary>
    /// Doğrulama sırasında hangi tarafın üye listesinin denetleneceğini belirtir.
    /// AutoMapper'ın <c>MemberList</c> enum'u ile birebir uyumludur (aynı sayısal değerler).
    /// </summary>
    public enum MemberList
    {
        /// <summary>Hedef (destination) üyeleri doğrulanır (varsayılan).</summary>
        Destination = 0,

        /// <summary>Kaynak (source) üyeleri doğrulanır.</summary>
        Source = 1,

        /// <summary>Doğrulama yapılmaz.</summary>
        None = 2
    }
}
