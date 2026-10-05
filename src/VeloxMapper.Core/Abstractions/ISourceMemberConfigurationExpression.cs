namespace VeloxMapper;

/// <summary>
/// <c>ForSourceMember</c> çağrısında bir kaynak üyeyi yapılandırır. AutoMapper'ın
/// <c>ISourceMemberConfigurationExpression</c> arayüzü ile uyumludur.
/// </summary>
public interface ISourceMemberConfigurationExpression
{
    /// <summary>
    /// Kaynak üyeyi <c>MemberList.Source</c> doğrulamasından muaf tutar.
    /// </summary>
    void DoNotValidate();

    /// <summary>
    /// Kaynak üyeyi <c>MemberList.Source</c> doğrulamasından muaf tutar (<see cref="DoNotValidate"/> ile aynıdır).
    /// </summary>
    void Ignore();
}
