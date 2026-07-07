using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace VeloxMapper.Configuration;

/// <summary>
/// Bir <c>CreateMap&lt;TSource, TDest&gt;()</c> çağrısıyla kaydedilen eşleştirme bilgisini tutar.
/// Kaynak-hedef tür çifti, ForMember kuralları ve tüm gelişmiş seçenekleri saklar.
/// MapperConfiguration oluşturulduktan sonra bu kayıt değiştirilemez (immutable).
/// </summary>
public sealed class MappingRegistration
{
    /// <summary>Kaynak (source) tür.</summary>
    public Type SourceType { get; }

    /// <summary>Hedef (destination) tür.</summary>
    public Type DestinationType { get; }

    /// <summary>
    /// ForMember ile tanımlanmış özel üye eşleştirme kuralları.
    /// Anahtar: hedef property adı.
    /// </summary>
    public IReadOnlyDictionary<string, MemberMappingRule> MemberRules { get; }

    /// <summary>
    /// Özel tip dönüştürücü (ConvertUsing ile atanmış). Null ise otomatik eşleştirme kullanılır.
    /// </summary>
    public object? CustomConverter { get; }

    /// <summary>Bu kaydı oluşturan profil adı (diagnostics için).</summary>
    public string? ProfileName { get; }

    // ─── AutoMapper Geçiş Uyumluluk Alanları ────────────────────────────────

    private bool _requiresContext;

    /// <summary>
    /// Eğer mapping işlemi bir IServiceProvider veya ResolutionContext gerektiriyorsa true döner.
    /// </summary>
    public bool RequiresContext => _requiresContext;

    /// <summary>
    /// ConstructUsing factory delegate.
    /// <c>(object source) → object destination</c> imzasıyla çalışır.
    /// Null ise ExpressionBuilder kendi constructor seçimini yapar.
    /// </summary>
    public Func<object, object>? FactoryDelegate { get; }

    /// <summary>
    /// ForAllMembers koşulu.
    /// <c>(object src, object dest, object? srcValue) → bool</c> imzasıyla çalışır.
    /// False dönerse ilgili property atanmaz.
    /// Null ise tüm property'ler koşulsuz atanır.
    /// </summary>
    public Func<object, object, object?, bool>? ForAllMembersCondition { get; }

    /// <summary>
    /// ForAllMembers().Ignore() çağrıldıysa true — tüm property atamaları atlanır.
    /// </summary>
    public bool ForAllMembersIgnored { get; }

    /// <summary>
    /// ReverseMap() çağrıldıysa true.
    /// MapperConfiguration bu bayrağı okuyarak ters yön kaydını otomatik ekler.
    /// </summary>
    internal bool IsReverseMapRequested { get; }

    /// <summary>
    /// Eşleştirme öncesinde (BeforeMap) çalıştırılacak eylemler.
    /// Eylem tipleri (Type) veya inline delegeler (Action) içerebilir.
    /// </summary>
    public IReadOnlyList<object> BeforeMapActions { get; }

    /// <summary>
    /// Eşleştirme sonrasında (AfterMap) çalıştırılacak eylemler.
    /// Eylem tipleri (Type) veya inline delegeler (Action) içerebilir.
    /// </summary>
    public IReadOnlyList<object> AfterMapActions { get; }

    /// <summary>Maksimum rekürsif mapping derinliği. 0 ise sınırsız (varsayılan).</summary>
    public int MaxDepth { get; }

    /// <summary>Döngüsel referans koruması etkin mi?</summary>
    public bool PreserveReferences { get; }

    private readonly List<(Type DerivedSource, Type DerivedDestination)> _includedDerivedTypes;

    /// <summary>Include ile tanımlanmış derived tür çiftleri.</summary>
    public IReadOnlyList<(Type DerivedSource, Type DerivedDestination)> IncludedDerivedTypes => _includedDerivedTypes;

    /// <summary>IncludeBase ile miras alınan base tür çifti.</summary>
    public (Type BaseSource, Type BaseDestination)? BaseTypeMapping { get; }

    /// <summary>
    /// Eğer true ise, alt sınıf eşleştirmeleri otomatik olarak polimorfik haritalamaya (Include) dahil edilir.
    /// </summary>
    public bool IsIncludeAllDerivedRequested { get; }

    /// <summary>ForPath ile tanımlanan derin yol eşleştirme kuralları.</summary>
    public IReadOnlyList<ForPathRule> ForPathRules { get; }

    /// <summary>ForCtorParam ile tanımlanan constructor parametre eşleştirme kuralları.</summary>
    public IReadOnlyList<CtorParamRule> CtorParamRules { get; }

    /// <summary>Yönlendirilecek hedef (redirect destination) tür.</summary>
    public Type? RedirectDestinationType { get; }

    internal MappingRegistration(
        Type sourceType,
        Type destinationType,
        Dictionary<string, MemberMappingRule> memberRules,
        object? customConverter,
        string? profileName,
        Func<object, object>? factoryDelegate = null,
        Func<object, object, object?, bool>? forAllMembersCondition = null,
        bool forAllMembersIgnored = false,
        bool isReverseMapRequested = false,
        IReadOnlyList<object>? beforeMapActions = null,
        IReadOnlyList<object>? afterMapActions = null,
        int maxDepth = 0,
        bool preserveReferences = false,
        IReadOnlyList<(Type DerivedSource, Type DerivedDestination)>? includedDerivedTypes = null,
        (Type BaseSource, Type BaseDestination)? baseTypeMapping = null,
        IReadOnlyList<ForPathRule>? forPathRules = null,
        IReadOnlyList<CtorParamRule>? ctorParamRules = null,
        bool isIncludeAllDerivedRequested = false,
        Type? redirectDestinationType = null)
    {
        SourceType         = sourceType         ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType    = destinationType    ?? throw new ArgumentNullException(nameof(destinationType));
        MemberRules        = new Dictionary<string, MemberMappingRule>(memberRules);
        CustomConverter    = customConverter;
        ProfileName        = profileName;
        FactoryDelegate    = factoryDelegate;
        ForAllMembersCondition = forAllMembersCondition;
        ForAllMembersIgnored   = forAllMembersIgnored;
        IsReverseMapRequested  = isReverseMapRequested;
        BeforeMapActions       = beforeMapActions != null ? new List<object>(beforeMapActions) : new List<object>();
        AfterMapActions        = afterMapActions != null ? new List<object>(afterMapActions) : new List<object>();
        MaxDepth               = maxDepth;
        PreserveReferences     = preserveReferences;
        _includedDerivedTypes  = includedDerivedTypes != null ? new List<(Type, Type)>(includedDerivedTypes) : new List<(Type, Type)>();
        BaseTypeMapping        = baseTypeMapping;
        ForPathRules           = forPathRules ?? Array.Empty<ForPathRule>();
        CtorParamRules         = ctorParamRules ?? Array.Empty<CtorParamRule>();
        IsIncludeAllDerivedRequested = isIncludeAllDerivedRequested;
        RedirectDestinationType = redirectDestinationType;

        // Context gereksinimi başlangıçta bir kez hesaplanır.
        _requiresContext = SelfRequiresContext();
    }

    /// <summary>
    /// Verilen ifadenin basit bir property veya field erişim zinciri olup olmadığını kontrol eder.
    /// </summary>
    private static bool IsSimplePropertyAccess(Expression expression)
    {
        var current = expression;
        while (current != null)
        {
            if (current is ParameterExpression)
            {
                return true;
            }
            if (current is MemberExpression memberExpr)
            {
                current = memberExpr.Expression;
            }
            else if (current is UnaryExpression unaryExpr && (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked))
            {
                current = unaryExpr.Operand;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Eşleştirmenin kendi kuralları gereği (nested bağımlılıklar hariç) context gerektirip gerektirmediğini hesaplar.
    /// </summary>
    internal bool SelfRequiresContext()
    {
        if (BeforeMapActions.Count > 0 || AfterMapActions.Count > 0 || MaxDepth > 0 || PreserveReferences || IncludedDerivedTypes.Count > 0 || CustomConverter != null || RedirectDestinationType != null)
            return true;

        if (MemberRules.Count > 0)
        {
            // Sadece basit MapFrom (örn: src => src.Prop) kuralları varsa context gerekmez.
            // Özel resolver, converter, condition vb. varsa context gerekir.
            foreach (var rule in MemberRules.Values)
            {
                if (rule.ResolverType != null ||
                    rule.MemberValueResolverType != null ||
                    rule.ValueConverterType != null ||
                    rule.ValueConverter != null ||
                    rule.ConditionDelegate != null ||
                    rule.PreConditionDelegate != null ||
                    rule.HasNullSubstitute ||
                    rule.KeepDestinationValue)
                {
                    return true;
                }

                // Eğer MapFromExpression basit bir property/field erişimi değilse (örn. metot çağrısı veya karmaşık mantık) context gerektirir
                if (rule.MapFromExpression != null && !IsSimplePropertyAccess(rule.MapFromExpression.Body))
                {
                    return true;
                }
            }
        }

        if (ForPathRules != null && ForPathRules.Count > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Eşleştirmenin context gereksinim durumunu manuel set eder (MapperConfiguration optimizasyonu için).
    /// </summary>
    internal void SetRequiresContext(bool value)
    {
        _requiresContext = value;
    }

    /// <summary>
    /// ForAllMaps veya diğer global konfigürasyon değişikliklerinden sonra context gereksinimini yeniden hesaplar.
    /// </summary>
    internal void RecalculateRequiresContext()
    {
        _requiresContext = SelfRequiresContext();
    }

    /// <summary>
    /// Dinamik olarak polimorfik eşleştirmeye derived tür çifti ekler.
    /// </summary>
    internal void AddIncludedDerivedType(Type source, Type destination)
    {
        if (!_includedDerivedTypes.Contains((source, destination)))
        {
            _includedDerivedTypes.Add((source, destination));
            RecalculateRequiresContext();
        }
    }

    /// <summary>
    /// Mevcut kaydın ters yönünü (Dest→Src) temsil eden yeni bir kayıt oluşturur.
    /// ReverseMap() desteği için kullanılır — Ignore ve basit MapFrom özel kuralları tersine çevrilerek aktarılır.
    /// </summary>
    internal static MappingRegistration CreateReverse(MappingRegistration original)
    {
        var reverseMemberRules = new Dictionary<string, MemberMappingRule>();
        var reverseForPathRules = new List<ForPathRule>();

        foreach (var rule in original.MemberRules.Values)
        {
            if (rule.IsIgnored)
            {
                // Orijinal haritalamada hedef üye yoksayılmışsa ve kaynak türde de aynı isimde üye varsa ters yönde de yoksayılır.
                var srcProp = original.SourceType.GetProperty(rule.DestinationMemberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (srcProp != null && srcProp.CanWrite)
                {
                    reverseMemberRules[rule.DestinationMemberName] = new MemberMappingRule(rule.DestinationMemberName);
                }
            }
            else if (rule.MapFromExpression != null)
            {
                // Basit property erişim ifadelerini (örn: src => src.SrcProp) tersine çevir.
                var body = rule.MapFromExpression.Body;

                // Tür dönüşüm (convert) ifadesini soy.
                if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                {
                    body = unary.Operand;
                }

                if (body is MemberExpression memberExpr &&
                    memberExpr.Expression == rule.MapFromExpression.Parameters[0] &&
                    memberExpr.Member is System.Reflection.PropertyInfo srcPropInfo)
                {
                    var srcPropName = srcPropInfo.Name;
                    var destPropName = rule.DestinationMemberName;

                    var destPropInfo = original.DestinationType.GetProperty(destPropName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (destPropInfo != null && destPropInfo.CanRead)
                    {
                        var param = Expression.Parameter(original.DestinationType, "dest");
                        var propertyAccess = Expression.Property(param, destPropInfo);

                        Expression lambdaBody = propertyAccess;
                        if (propertyAccess.Type != srcPropInfo.PropertyType)
                        {
                            lambdaBody = Expression.Convert(propertyAccess, srcPropInfo.PropertyType);
                        }

                        var reverseLambda = Expression.Lambda(lambdaBody, param);
                        reverseMemberRules[srcPropName] = new MemberMappingRule(srcPropName, reverseLambda);
                    }
                }
            }
        }

        // Otomatik Unflattening Çözümlemesi (Reverse of Flattening)
        var destProps = original.DestinationType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var sourceProps = original.SourceType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var destProp in destProps)
        {
            if (reverseMemberRules.ContainsKey(destProp.Name))
                continue;

            var matchProp = sourceProps.FirstOrDefault(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
            if (matchProp != null)
                continue;

            var path = ResolveUnflatteningPath(original.SourceType, destProp.Name);
            if (path != null)
            {
                var param = Expression.Parameter(original.DestinationType, "src");
                var propAccess = Expression.Property(param, destProp);
                Expression lambdaBody = propAccess;

                // Hedef nested property tipini bul
                var targetType = original.SourceType;
                foreach (var seg in path)
                {
                    var p = targetType.GetProperty(seg, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (p != null)
                        targetType = p.PropertyType;
                }

                if (propAccess.Type != targetType)
                {
                    lambdaBody = Expression.Convert(propAccess, targetType);
                }

                var reverseLambda = Expression.Lambda(lambdaBody, param);
                var memberRule = new MemberMappingRule(path[path.Length - 1], reverseLambda);
                reverseForPathRules.Add(new ForPathRule(path, memberRule));
            }
        }

        return new MappingRegistration(
            sourceType:      original.DestinationType,
            destinationType: original.SourceType,
            memberRules:     reverseMemberRules,
            customConverter: null,
            profileName:     original.ProfileName + " [Reverse]",
            factoryDelegate: null,
            forAllMembersCondition: null,
            forAllMembersIgnored: false,
            isReverseMapRequested: false,
            beforeMapActions: Array.Empty<object>(),
            afterMapActions: Array.Empty<object>(),
            maxDepth: original.MaxDepth,
            preserveReferences: original.PreserveReferences,
            includedDerivedTypes: null,
            baseTypeMapping: null,
            forPathRules: reverseForPathRules,
            ctorParamRules: null,
            isIncludeAllDerivedRequested: false,
            redirectDestinationType: null);
    }

    private static string[]? ResolveUnflatteningPath(Type sourceType, string destPropName)
    {
        var segments = new List<string>();
        var currentType = sourceType;
        var remaining = destPropName;

        while (!string.IsNullOrEmpty(remaining))
        {
            var props = currentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            System.Reflection.PropertyInfo? matchProp = null;

            for (int i = remaining.Length; i >= 1; i--)
            {
                var candidate = remaining.Substring(0, i);
                var prop = props.FirstOrDefault(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (prop != null)
                {
                    matchProp = prop;
                    remaining = remaining.Substring(i);
                    break;
                }
            }

            if (matchProp == null)
            {
                return null;
            }

            segments.Add(matchProp.Name);
            currentType = matchProp.PropertyType;
        }

        return segments.Count > 1 ? segments.ToArray() : null;
    }
}
