using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Tek bir hedef üye için toplanan eşleştirme kuralı (<c>ForMember</c>, <c>ForPath</c>, <c>ForAllMembers</c> çıktısı).
/// Yapılandırma dondurulduktan sonra salt-okunur olarak kullanılır.
/// </summary>
public sealed class MemberMappingRule
{
    private List<LambdaExpression>? _transformers;

    internal MemberMappingRule(string destinationMemberName)
    {
        DestinationMemberName = destinationMemberName ?? throw new ArgumentNullException(nameof(destinationMemberName));
    }

    internal MemberMappingRule(string destinationMemberName, LambdaExpression mapFromExpression)
        : this(destinationMemberName)
    {
        MapFromExpression = mapFromExpression ?? throw new ArgumentNullException(nameof(mapFromExpression));
    }

    /// <summary>Hedef üye adı.</summary>
    public string DestinationMemberName { get; internal set; }

    /// <summary>Üye eşleştirme ve doğrulama dışında bırakıldıysa <c>true</c>.</summary>
    public bool IsIgnored { get; internal set; }

    /// <summary>Üye yalnızca doğrulamadan muaf tutulduysa <c>true</c> (<c>DoNotValidate</c>).</summary>
    public bool IsDoNotValidate { get; internal set; }

    /// <summary><c>MapFrom(src =&gt; ...)</c> ile verilen kaynak ifadesi.</summary>
    public LambdaExpression? MapFromExpression { get; internal set; }

    /// <summary><c>MapFrom("A.B")</c> ile verilen kaynak üye yolu.</summary>
    public string? SourceMemberPath { get; internal set; }

    /// <summary><c>MapFrom((src, dest, ...) =&gt; ...)</c> ile verilen fonksiyon.</summary>
    public Delegate? MapFromFunc { get; internal set; }

    /// <summary><see cref="MapFromFunc"/> parametre sayısı (2, 3 veya 4).</summary>
    public int MapFromFuncArity { get; internal set; }

    /// <summary>Value resolver türü.</summary>
    public Type? ResolverType { get; internal set; }

    /// <summary>Value resolver örneği.</summary>
    public object? ResolverInstance { get; internal set; }

    /// <summary>Member value resolver türü.</summary>
    public Type? MemberValueResolverType { get; internal set; }

    /// <summary>Member value resolver örneği.</summary>
    public object? MemberValueResolverInstance { get; internal set; }

    /// <summary>Member value resolver girdisi olan kaynak üye ifadesi.</summary>
    public LambdaExpression? SourceMemberForResolver { get; internal set; }

    /// <summary>Member value resolver girdisi olan kaynak üye adı.</summary>
    public string? SourceMemberNameForResolver { get; internal set; }

    /// <summary>Value converter örneği.</summary>
    public object? ValueConverter { get; internal set; }

    /// <summary>Value converter türü.</summary>
    public Type? ValueConverterType { get; internal set; }

    /// <summary>Value converter girdisi olan kaynak üye ifadesi.</summary>
    public LambdaExpression? ValueConverterSourceMember { get; internal set; }

    /// <summary>Value converter girdisi olan kaynak üye adı (null ise hedef üye adı kullanılır).</summary>
    public string? ValueConverterSourceMemberName { get; internal set; }

    /// <summary>True ise value converter tanımlıdır.</summary>
    public bool HasValueConverter => ValueConverter != null || ValueConverterType != null;

    /// <summary>Koşul delegesi (<c>Condition</c>).</summary>
    public Delegate? ConditionDelegate { get; internal set; }

    /// <summary>Ön koşul delegesi (<c>PreCondition</c>).</summary>
    public Delegate? PreConditionDelegate { get; internal set; }

    /// <summary><c>NullSubstitute</c> değeri.</summary>
    public object? NullSubstituteValue { get; internal set; }

    /// <summary><c>NullSubstitute</c> tanımlıysa <c>true</c>.</summary>
    public bool HasNullSubstitute { get; internal set; }

    /// <summary><c>UseDestinationValue</c> (true) / <c>DoNotUseDestinationValue</c> (false) tercihi; <c>null</c> ise varsayılan davranış.</summary>
    public bool? UseDestinationValue { get; internal set; }

    /// <summary>Hedef üyenin mevcut değeri korunacaksa <c>true</c>.</summary>
    public bool KeepDestinationValue => UseDestinationValue == true;

    /// <summary>Atanma sırası (<c>SetMappingOrder</c>); varsayılan 0.</summary>
    public int MappingOrder { get; internal set; }

    /// <summary><c>ExplicitExpansion</c> ile işaretlendiyse <c>true</c>.</summary>
    public bool ExplicitExpansion { get; internal set; }

    /// <summary><c>AllowNull</c> (true) / <c>DoNotAllowNull</c> (false) tercihi; <c>null</c> ise profil ayarı geçerlidir.</summary>
    public bool? AllowNull { get; internal set; }

    /// <summary>Üyeye özel value transformer ifadeleri.</summary>
    public IReadOnlyList<LambdaExpression> Transformers => (IReadOnlyList<LambdaExpression>?)_transformers ?? Array.Empty<LambdaExpression>();

    /// <summary>Üyenin değeri için açık bir kaynak (MapFrom, resolver veya converter) belirtildiyse <c>true</c>.</summary>
    public bool HasValueSource =>
        MapFromExpression != null || SourceMemberPath != null || MapFromFunc != null ||
        ResolverType != null || ResolverInstance != null ||
        MemberValueResolverType != null || MemberValueResolverInstance != null ||
        HasValueConverter;

    internal void AddTransformer(LambdaExpression transformer)
    {
        (_transformers ??= new List<LambdaExpression>()).Add(transformer);
    }

    /// <summary>Tüm değer kaynaklarını temizler (yeni bir MapFrom/ConvertUsing çağrısından önce).</summary>
    internal void ClearValueSource()
    {
        MapFromExpression = null;
        SourceMemberPath = null;
        MapFromFunc = null;
        MapFromFuncArity = 0;
        ResolverType = null;
        ResolverInstance = null;
        MemberValueResolverType = null;
        MemberValueResolverInstance = null;
        SourceMemberForResolver = null;
        SourceMemberNameForResolver = null;
        ValueConverter = null;
        ValueConverterType = null;
        ValueConverterSourceMember = null;
        ValueConverterSourceMemberName = null;
        IsIgnored = false;
    }

    /// <summary>Kuralın bağımsız bir kopyasını üretir.</summary>
    internal MemberMappingRule Clone(string? newName = null)
    {
        var copy = (MemberMappingRule)MemberwiseClone();
        if (newName != null) copy.DestinationMemberName = newName;
        copy._transformers = _transformers != null ? new List<LambdaExpression>(_transformers) : null;
        return copy;
    }
}
