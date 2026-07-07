using System;
using System.Collections.Generic;
using System.Linq;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;

namespace VeloxMapper.Execution;

/// <summary>
/// Kaynak ve hedef özellik (property) isimlerini eşleştirirken prefix, postfix ve isimlendirme kurallarını uygulayan yardımcı sınıf.
/// </summary>
internal static class NameMatchingHelper
{
    /// <summary>
    /// Kaynak ve hedef property isimlerinin, yapılandırma kurallarına göre eşleşip eşleşmediğini kontrol eder.
    /// </summary>
    /// <param name="srcName">Kaynak özellik adı</param>
    /// <param name="destName">Hedef özellik adı</param>
    /// <param name="config">Mapper yapılandırması</param>
    /// <returns>Eşleşme varsa true, aksi halde false</returns>
    public static bool IsMatch(string srcName, string destName, MapperConfiguration config)
    {
        // Kaynak ve hedef için olası isim adaylarını üretelim
        var srcCandidates = GetNameCandidates(srcName, config.Prefixes, config.Postfixes, config.SourceMemberNamingConvention);
        var destCandidates = GetNameCandidates(destName, config.DestinationPrefixes, config.DestinationPostfixes, config.DestinationMemberNamingConvention);

        // Aday kümelerinden en az biri kesişiyorsa eşleşme vardır
        return srcCandidates.Overlaps(destCandidates);
    }

    /// <summary>
    /// Verilen property adı için prefix/postfix kırpılmış ve normalize edilmiş tüm olası eşleşme adlarını üretir.
    /// </summary>
    /// <param name="originalName">Orijinal özellik adı</param>
    /// <param name="prefixes">Ön ekler</param>
    /// <param name="postfixes">Son ekler</param>
    /// <param name="namingConvention">Özel isimlendirme kuralı</param>
    /// <returns>Olası tüm aday adların kümesi</returns>
    public static HashSet<string> GetNameCandidates(
        string originalName,
        IReadOnlyList<string> prefixes,
        IReadOnlyList<string> postfixes,
        ICustomNamingConvention? namingConvention)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { originalName };

        // 1. Sadece ön ek (prefix) kırpma
        foreach (var prefix in prefixes)
        {
            if (originalName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && originalName.Length > prefix.Length)
            {
                candidates.Add(originalName.Substring(prefix.Length));
            }
        }

        // 2. Sadece son ek (postfix) kırpma
        foreach (var postfix in postfixes)
        {
            if (originalName.EndsWith(postfix, StringComparison.OrdinalIgnoreCase) && originalName.Length > postfix.Length)
            {
                candidates.Add(originalName.Substring(0, originalName.Length - postfix.Length));
            }
        }

        // 3. Hem ön ek hem son ek kırpılmış versiyonlar
        var currentCandidates = candidates.ToList();
        foreach (var candidate in currentCandidates)
        {
            foreach (var prefix in prefixes)
            {
                if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && candidate.Length > prefix.Length)
                {
                    candidates.Add(candidate.Substring(prefix.Length));
                }
            }
            foreach (var postfix in postfixes)
            {
                if (candidate.EndsWith(postfix, StringComparison.OrdinalIgnoreCase) && candidate.Length > postfix.Length)
                {
                    candidates.Add(candidate.Substring(0, candidate.Length - postfix.Length));
                }
            }
        }

        // 4. İsimlendirme kuralı (Naming Convention) varsa, adayları normalize ederek ekle
        if (namingConvention != null)
        {
            var normalizedCandidates = new List<string>();
            foreach (var cand in candidates)
            {
                normalizedCandidates.Add(namingConvention.Normalize(cand));
            }
            foreach (var norm in normalizedCandidates)
            {
                candidates.Add(norm);
            }
        }

        return candidates;
    }
}
