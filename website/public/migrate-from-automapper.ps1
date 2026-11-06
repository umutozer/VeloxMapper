<#
.SYNOPSIS
    Bir .NET çözümünü AutoMapper'dan VeloxMapper'a taşır.

.DESCRIPTION
    - .csproj / .fsproj / .vbproj / Directory.Packages.props / Directory.Build.props / packages.config içindeki AutoMapper
      paket referanslarını VeloxMapper ile değiştirir (nitelik sırası ve PrivateAssets gibi ek nitelikler korunur).
    - AutoMapper.Extensions.Microsoft.DependencyInjection ve AutoMapper.Extensions.EnumMapping paketlerini kaldırır
      (işlevleri VeloxMapper'da yerleşiktir). Projede başka AutoMapper referansı yoksa yerine VeloxMapper eklenir.
    - .cs dosyalarındaki AutoMapper namespace'lerini, tam nitelikli tür adlarını, AddAutoMapper(...) çağrılarını ve
      AutoMapper'a özgü istisna adlarını VeloxMapper karşılıklarıyla değiştirir. String sabitleri ve yorumlar değiştirilmez.
    - .razor / .cshtml dosyalarındaki @using ve @inject satırlarını günceller.
    - Elle gözden geçirilmesi gereken API kullanımlarını raporlar; VB / F# kaynak dosyaları yalnızca raporlanır.

    Betik yalnızca metin değişikliği yapar; UTF-8 (BOM'lu/BOM'suz) ve UTF-16 dosyaların kodlaması ile satır sonları korunur.
    UTF-8 veya UTF-16 olmayan dosyalar değiştirilmez ve raporlanır. Çalıştırmadan önce değişikliklerinizi commit edin;
    ardından "git diff" ile sonucu inceleyin.

.PARAMETER Path
    Taranacak kök klasör (varsayılan: geçerli klasör).

.PARAMETER Version
    Eklenecek VeloxMapper paket sürümü (varsayılan: 6.1.0).

.PARAMETER DryRun
    Dosyaları değiştirmeden yalnızca yapılacak değişiklikleri listeler.

.OUTPUTS
    Çıkış kodu: 0 başarılı, 1 bir veya daha fazla dosya yazılamadı, 2 elle gözden geçirilecek madde var.

.EXAMPLE
    pwsh ./migrate-from-automapper.ps1 -Path ./src
.EXAMPLE
    pwsh ./migrate-from-automapper.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Path = ".",
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version = "6.1.0",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $Path).Path
$excludedDirs = @("bin", "obj", ".git", ".vs", "node_modules")
$packagesDirCache = @{}

# ── Yardımcılar ──────────────────────────────────────────────────────────
function Test-NuGetPackagesDir([string]$dir) {
    # "packages" adlı bir klasör yalnızca NuGet paket klasörüyse (repositories.config veya .nupkg içeriyorsa) atlanır;
    # kaynak kod içeren aynı adlı klasörler taranır.
    if (-not $packagesDirCache.ContainsKey($dir)) {
        $isNuGet = (Test-Path -LiteralPath (Join-Path $dir "repositories.config")) -or
                   ((Get-ChildItem -LiteralPath $dir -Filter "*.nupkg" -Recurse -File -Depth 1 -ErrorAction SilentlyContinue | Select-Object -First 1) -ne $null)
        $packagesDirCache[$dir] = $isNuGet
    }
    return $packagesDirCache[$dir]
}

function Get-SourceFiles([string[]]$patterns) {
    # Not: Windows PowerShell 5.1'de -LiteralPath ile -Include yok sayılır; ad filtresi açıkça uygulanır.
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $name = $_.Name
        ($patterns | Where-Object { $name -like $_ }).Count -gt 0
    } | Where-Object {
        $relative = $_.FullName.Substring($root.Length)
        $segments = $relative.Split([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        $excluded = $false
        $current = $root
        foreach ($segment in $segments) {
            if ($segment -eq "" -or $segment -eq $_.Name) { continue }
            $current = Join-Path $current $segment
            if ($excludedDirs -contains $segment) { $excluded = $true; break }
            if ($segment -eq "packages" -and (Test-NuGetPackagesDir $current)) { $excluded = $true; break }
        }
        -not $excluded
    }
}

function Read-TextFile([string]$file) {
    # Dönüş: @{ Text; Encoding } veya kodlama tanınmıyorsa $null (dosya atlanır).
    $bytes = [IO.File]::ReadAllBytes($file)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        $encoding = New-Object System.Text.UnicodeEncoding($false, $true)
        return @{ Text = $encoding.GetString($bytes, 2, $bytes.Length - 2); Encoding = $encoding }
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        $encoding = New-Object System.Text.UnicodeEncoding($true, $true)
        return @{ Text = $encoding.GetString($bytes, 2, $bytes.Length - 2); Encoding = $encoding }
    }

    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $strict = New-Object System.Text.UTF8Encoding($hasBom, $true)
    try {
        $offset = 0
        if ($hasBom) { $offset = 3 }
        $text = $strict.GetString($bytes, $offset, $bytes.Length - $offset)
        return @{ Text = $text; Encoding = $strict }
    }
    catch [System.Text.DecoderFallbackException] {
        return $null
    }
}

function Get-CSharpCodeSegments([string]$text) {
    # Metni kod / kod-dışı (yorum, string ve karakter sabiti) parçalara ayırır. Değişiklikler yalnızca kod parçalarına uygulanır.
    $segments = New-Object System.Collections.Generic.List[object]
    $n = $text.Length
    $i = 0
    $codeStart = 0

    $add = {
        param($start, $length, $isCode)
        if ($length -gt 0) { $segments.Add(@{ Start = $start; Length = $length; IsCode = $isCode }) }
    }

    while ($i -lt $n) {
        $c = $text[$i]
        $next = [char]0
        if ($i + 1 -lt $n) { $next = $text[$i + 1] }

        if ($c -eq '/' -and $next -eq '/') {
            $end = $text.IndexOf("`n", $i)
            if ($end -lt 0) { $end = $n }
            & $add $codeStart ($i - $codeStart) $true
            & $add $i ($end - $i) $false
            $i = $end; $codeStart = $end
            continue
        }

        if ($c -eq '/' -and $next -eq '*') {
            $end = $text.IndexOf("*/", $i + 2)
            if ($end -lt 0) { $end = $n } else { $end += 2 }
            & $add $codeStart ($i - $codeStart) $true
            & $add $i ($end - $i) $false
            $i = $end; $codeStart = $end
            continue
        }

        if ($c -eq '"') {
            $end = $n
            if ($next -eq '"' -and $i + 2 -lt $n -and $text[$i + 2] -eq '"') {
                # Ham string sabiti ("""...""")
                $quotes = 0
                while ($i + $quotes -lt $n -and $text[$i + $quotes] -eq '"') { $quotes++ }
                $closing = New-Object string ('"', $quotes)
                $close = $text.IndexOf($closing, $i + $quotes)
                if ($close -ge 0) { $end = $close + $quotes }
            }
            else {
                $verbatim = $false
                $j = $i - 1
                while ($j -ge 0 -and ($text[$j] -eq '@' -or $text[$j] -eq '$')) {
                    if ($text[$j] -eq '@') { $verbatim = $true }
                    $j--
                }
                $k = $i + 1
                while ($k -lt $n) {
                    $ch = $text[$k]
                    if ($verbatim) {
                        if ($ch -eq '"') {
                            if ($k + 1 -lt $n -and $text[$k + 1] -eq '"') { $k += 2; continue }
                            $end = $k + 1; break
                        }
                    }
                    else {
                        if ($ch -eq '\') { $k += 2; continue }
                        if ($ch -eq '"') { $end = $k + 1; break }
                        if ($ch -eq "`n") { $end = $k; break }
                    }
                    $k++
                }
            }
            & $add $codeStart ($i - $codeStart) $true
            & $add $i ($end - $i) $false
            $i = $end; $codeStart = $end
            continue
        }

        if ($c -eq "'") {
            $k = $i + 1
            if ($k -lt $n -and $text[$k] -eq '\') { $k += 2 } else { $k++ }
            if ($k -lt $n -and $text[$k] -eq "'") {
                $end = $k + 1
                & $add $codeStart ($i - $codeStart) $true
                & $add $i ($end - $i) $false
                $i = $end; $codeStart = $end
                continue
            }
        }

        $i++
    }

    & $add $codeStart ($n - $codeStart) $true
    return $segments
}

function Apply-Replacements([string]$text, $replacements, [bool]$maskLiterals) {
    if (-not $maskLiterals) {
        foreach ($pattern in $replacements.Keys) {
            $text = [regex]::Replace($text, $pattern, $replacements[$pattern])
        }
        return $text
    }

    $builder = New-Object System.Text.StringBuilder
    foreach ($segment in Get-CSharpCodeSegments $text) {
        $part = $text.Substring($segment.Start, $segment.Length)
        if ($segment.IsCode) {
            foreach ($pattern in $replacements.Keys) {
                $part = [regex]::Replace($part, $pattern, $replacements[$pattern])
            }
        }
        [void]$builder.Append($part)
    }
    return $builder.ToString()
}

function Test-CentralPackageManagement([string]$projectFile) {
    $dir = [IO.Path]::GetDirectoryName($projectFile)
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir "Directory.Packages.props")) { return $true }
        $parent = [IO.Path]::GetDirectoryName($dir)
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    return $false
}

$changedFiles = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$failures = New-Object System.Collections.Generic.List[string]
$pendingWrites = New-Object System.Collections.Generic.List[object]

function Queue-Write([string]$file, [string]$text, $encoding) {
    $pendingWrites.Add(@{ File = $file; Text = $text; Encoding = $encoding })
    $changedFiles.Add($file)
}

# ── 1. Paket referansları ────────────────────────────────────────────────
$projectPatterns = @("*.csproj", "*.fsproj", "*.vbproj", "Directory.Packages.props", "Directory.Build.props", "packages.config")
$builtInPackages = 'AutoMapper\.Extensions\.(Microsoft\.DependencyInjection|EnumMapping)'
$removeElementPattern = "(?m)^[ \t]*<Package(Reference|Version)\b[^>]*?\bInclude=`"$builtInPackages`"[^>]*?(/>|>[\s\S]*?</Package\1>)[ \t]*\r?\n?"
$removeElementInlinePattern = "<Package(Reference|Version)\b[^>]*?\bInclude=`"$builtInPackages`"[^>]*?(/>|>[\s\S]*?</Package\1>)"
$removePackagesConfigPattern = "(?m)^[ \t]*<package\s+id=`"$builtInPackages`"[^>]*/>[ \t]*\r?\n?"
$removePackagesConfigInlinePattern = "<package\s+id=`"$builtInPackages`"[^>]*/>"

foreach ($file in Get-SourceFiles $projectPatterns) {
    $content = Read-TextFile $file.FullName
    if ($content -eq $null) {
        $warnings.Add("$($file.FullName): dosya kodlaması UTF-8/UTF-16 değil; dosya değiştirilmedi, elle güncelleyin.")
        continue
    }
    $text = $content.Text
    $original = $text
    $isPackagesConfig = $file.Name -ieq "packages.config"
    $isPropsFile = $file.Name -ieq "Directory.Packages.props" -or $file.Name -ieq "Directory.Build.props"

    $hasMapperReference = ($text -match '<Package(Reference|Version)\b[^>]*?\bInclude="AutoMapper"') -or ($text -match '<package\s+id="AutoMapper"')
    $hasBuiltInPackage = $text -match $builtInPackages

    if ($isPackagesConfig) {
        # Yerleşik hale gelen eklenti paketlerini kaldır; AutoMapper referansı yoksa ilkini VeloxMapper ile değiştir
        if ($hasBuiltInPackage -and -not $hasMapperReference) {
            $text = [regex]::Replace($text, $removePackagesConfigInlinePattern, "<package id=`"VeloxMapper`" version=`"$Version`" />", 1)
        }
        $text = [regex]::Replace($text, $removePackagesConfigPattern, '')
        $text = [regex]::Replace($text, '<package\s+id="AutoMapper"\s+version="[^"]*"', "<package id=`"VeloxMapper`" version=`"$Version`"")
    }
    else {
        if ($hasBuiltInPackage -and -not $hasMapperReference) {
            $elementName = "PackageReference"
            if ($file.Name -ieq "Directory.Packages.props") { $elementName = "PackageVersion" }
            $versionAttribute = " Version=`"$Version`""
            if ($elementName -eq "PackageReference" -and -not $isPropsFile -and (Test-CentralPackageManagement $file.FullName)) { $versionAttribute = "" }
            $text = [regex]::Replace($text, $removeElementInlinePattern, "<$elementName Include=`"VeloxMapper`"$versionAttribute />", 1)
        }
        $text = [regex]::Replace($text, $removeElementPattern, '')

        # AutoMapper → VeloxMapper: açılış etiketindeki Include ve (varsa) Version nitelikleri, diğer nitelikler korunur
        $openTagPattern = '<Package(Reference|Version)\b([^>]*?)\bInclude="AutoMapper"([^>]*?)(/?>)'
        $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
            param($m)
            $before = [regex]::Replace($m.Groups[2].Value, '\bVersion="[^"]*"', "Version=`"$Version`"")
            $after = [regex]::Replace($m.Groups[3].Value, '\bVersion="[^"]*"', "Version=`"$Version`"")
            return "<Package$($m.Groups[1].Value)$before" + "Include=`"VeloxMapper`"$after$($m.Groups[4].Value)"
        }
        $text = [regex]::Replace($text, $openTagPattern, $evaluator)
        # Sürüm alt öğe olarak yazılmışsa: <PackageReference Include="VeloxMapper"><Version>...</Version>
        $text = [regex]::Replace($text, '(<PackageReference\b[^>]*\bInclude="VeloxMapper"[^>]*>\s*<Version>)[^<]*(</Version>)', "`${1}$Version`${2}")
    }

    foreach ($m in [regex]::Matches($text, '(?:Include|id)="(AutoMapper\.(?:Collection(?:\.[\w.]+)?|EF6|Data|Extensions\.ExpressionMapping|AspNetCore\.OData|Contrib\.[\w.]+))"')) {
        $warnings.Add("$($file.FullName): '$($m.Groups[1].Value)' paketi için VeloxMapper karşılığı yok; kullanımını elle gözden geçirin ve referansı kaldırın.")
    }

    if ($text -ne $original) { Queue-Write $file.FullName $text $content.Encoding }
}

# ── 2. C# ve Razor kaynak kodu ───────────────────────────────────────────
$typeNames = 'IMapper|Mapper|Profile|MapperConfiguration|MapperConfigurationExpression|IConfigurationProvider|ResolutionContext|IRuntimeMapper|' +
             'IValueResolver|IMemberValueResolver|IValueConverter|ITypeConverter|IMappingAction|IMappingExpression|IMemberConfigurationExpression|' +
             'IMapperConfigurationExpression|IProfileExpression|IMappingOperationOptions|ISourceMemberConfigurationExpression|' +
             'ICtorParamConfigurationExpression|IPathConfigurationExpression|MemberList|AutoMapAttribute|AutoMap|TypeMap|PropertyMap|' +
             'INamingConvention|PascalCaseNamingConvention|LowerUnderscoreNamingConvention|ExactMatchNamingConvention'

$replacements = [ordered]@{
    '(?m)^[ \t]*(global\s+)?using\s+AutoMapper\.Extensions\.EnumMapping\s*;[ \t]*\r?\n' = ''
    '(?m)^([ \t]*)(global\s+)?using\s+AutoMapper\.Extensions\.ExpressionMapping\s*;' = '$1$2using VeloxMapper; // TODO: ExpressionMapping karşılığı yok, kullanımı gözden geçirin'
    '\busing\s+AutoMapper\.QueryableExtensions\s*;' = 'using VeloxMapper.QueryableExtensions;'
    '\busing\s+AutoMapper\.Configuration\.Annotations\s*;' = 'using VeloxMapper.Configuration.Annotations;'
    '\busing\s+AutoMapper\.Configuration\s*;' = 'using VeloxMapper.Configuration;'
    '\busing\s+AutoMapper\s*;' = 'using VeloxMapper;'
    '\busing\s+(\w+)\s*=\s*AutoMapper\s*;' = 'using $1 = VeloxMapper;'
    '(?<![\w.])(?:global::)?(?:AutoMapper\.)?AutoMapperMappingException\b' = 'VeloxMapper.Exceptions.VeloxMappingException'
    '(?<![\w.])(?:global::)?(?:AutoMapper\.)?AutoMapperConfigurationException\b' = 'VeloxMapper.Exceptions.VeloxValidationException'
    '(?<![\w.])(?:global::)?(?:AutoMapper\.)?DuplicateTypeMapConfigurationException\b' = 'VeloxMapper.Exceptions.VeloxConfigurationException'
    '\.AddAutoMapper\(' = '.AddVeloxMapper('
    '(?<![\w.])AutoMapper\.QueryableExtensions\.' = 'VeloxMapper.QueryableExtensions.'
    '(?<![\w.])AutoMapper\.Configuration\.Annotations\.' = 'VeloxMapper.Configuration.Annotations.'
    "(?<![\w.])AutoMapper\.($typeNames)\b" = 'VeloxMapper.$1'
}

$razorReplacements = [ordered]@{
    '(?m)^(\s*)@using\s+AutoMapper\.QueryableExtensions\s*$' = '$1@using VeloxMapper.QueryableExtensions'
    '(?m)^(\s*)@using\s+AutoMapper\s*$' = '$1@using VeloxMapper'
    '(?m)^(\s*)@using\s+AutoMapper\.Extensions\.EnumMapping\s*\r?\n' = ''
    '(?<![\w.])AutoMapper\.QueryableExtensions\.' = 'VeloxMapper.QueryableExtensions.'
    "(?<![\w.])AutoMapper\.($typeNames)\b" = 'VeloxMapper.$1'
    '\.AddAutoMapper\(' = '.AddVeloxMapper('
}

$reviewPatterns = [ordered]@{
    '\.Internal\(\)' = "cfg.Internal() AutoMapper'ın iç API'sidir; VeloxMapper'da ForAllPropertyMaps/ForAllMaps kullanın."
    '\bUseAsDataSource\b' = "UseAsDataSource (AutoMapper.Extensions.ExpressionMapping) desteklenmez; ProjectTo kullanın."
    '\bEqualityComparison\b' = "EqualityComparison (AutoMapper.Collection) desteklenmez; koleksiyonlar Clear+Add ile eşlenir."
    '\bAddCollectionMappers\b' = "AddCollectionMappers (AutoMapper.Collection) desteklenmez; çağrıyı kaldırın."
    '\.AsProxy\(\)' = "AsProxy arayüz proxy'leri üretmez; somut bir hedef tür kullanın."
    '(?<![\w.])Mapper\.(Initialize|Reset|Configuration|AssertConfigurationIsValid)\b' = "Statik Mapper API'si (AutoMapper 8 ve öncesi) desteklenmez; MapperConfiguration ve IMapper kullanın."
    '(?<![\w.])Mapper\.Map\s*[<(]' = "Statik Mapper.Map çağrısı desteklenmez; IMapper örneği enjekte edin."
    '\busing\s+(static\s+)?(\w+\s*=\s*)?AutoMapper\.(Internal|Execution|Features|Mappers)\b' = "AutoMapper.Internal/Execution/Features/Mappers namespace'lerinin VeloxMapper karşılığı yoktur; satırı ve kullanımları elle kaldırın."
    '\bINamingConvention\b' = "AutoMapper'ın INamingConvention arayüzü yoktur; özel isimlendirme kuralları için VeloxMapper.Abstractions.ICustomNamingConvention (Normalize) uygulayın."
    '\bEnableEnumMappingValidation\s*\(' = "EnableEnumMappingValidation (AutoMapper.Extensions.EnumMapping) yoktur; enum eşlemeleri ConvertUsingEnumMapping ile tanımlanır, çağrıyı kaldırın."
}

foreach ($file in Get-SourceFiles @("*.cs", "*.razor", "*.cshtml")) {
    $content = Read-TextFile $file.FullName
    if ($content -eq $null) {
        $warnings.Add("$($file.FullName): dosya kodlaması UTF-8/UTF-16 değil; dosya değiştirilmedi, elle güncelleyin.")
        continue
    }
    $text = $content.Text
    if ($text.IndexOf("AutoMapper", [StringComparison]::Ordinal) -lt 0) { continue }
    $original = $text
    $isCSharp = $file.Extension -ieq ".cs"

    if ($isCSharp) {
        $text = Apply-Replacements $text $replacements $true
    }
    else {
        $text = Apply-Replacements $text $razorReplacements $false
    }

    foreach ($pattern in $reviewPatterns.Keys) {
        if ($text -match $pattern) {
            $warnings.Add("$($file.FullName): $($reviewPatterns[$pattern])")
        }
    }

    if ($text -ne $original) { Queue-Write $file.FullName $text $content.Encoding }
}

# ── 3. Diğer diller: yalnızca rapor ──────────────────────────────────────
foreach ($file in Get-SourceFiles @("*.vb", "*.fs", "*.fsx")) {
    $content = Read-TextFile $file.FullName
    if ($content -eq $null) { continue }
    if ($content.Text -match '(?m)^\s*(Imports|open)\s+AutoMapper\b' -or $content.Text -match '\bAutoMapper\.') {
        $warnings.Add("$($file.FullName): VB/F# dosyaları otomatik değiştirilmez; 'AutoMapper' namespace ve tür adlarını elle 'VeloxMapper' olarak güncelleyin.")
    }
}

# ── 4. Yazma ─────────────────────────────────────────────────────────────
if (-not $DryRun) {
    foreach ($write in $pendingWrites) {
        try {
            $attributes = [IO.File]::GetAttributes($write.File)
            if (($attributes -band [IO.FileAttributes]::ReadOnly) -ne 0) {
                $failures.Add("$($write.File): dosya salt okunur; değişiklik yazılmadı.")
                continue
            }
            [IO.File]::WriteAllText($write.File, $write.Text, $write.Encoding)
        }
        catch {
            $failures.Add("$($write.File): yazılamadı ($($_.Exception.Message))")
        }
    }
}

# ── 5. Rapor ─────────────────────────────────────────────────────────────
$mode = if ($DryRun) { "DEĞİŞTİRİLECEK" } else { "DEĞİŞTİRİLDİ" }
Write-Host ""
Write-Host "VeloxMapper geçiş betiği — $($changedFiles.Count) dosya $mode" -ForegroundColor Cyan
$changedFiles | ForEach-Object { Write-Host "  ~ $($_.Substring($root.Length).TrimStart('\','/'))" }

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Yazılamayan dosyalar ($($failures.Count)):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  x $_" -ForegroundColor Red }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "Elle gözden geçirilmesi gerekenler ($($warnings.Count)):" -ForegroundColor Yellow
    $warnings | Sort-Object -Unique | ForEach-Object { Write-Host "  ! $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "Sonraki adımlar:" -ForegroundColor Cyan
Write-Host "  1. dotnet restore && dotnet build"
Write-Host "  2. Testlerinizde config.AssertConfigurationIsValid() çağrısını çalıştırın"
Write-Host "  3. Davranış farkları: https://veloxmapper-website.netlify.app/docs/behavior-differences"

if ($failures.Count -gt 0) { exit 1 }
# Elle gözden geçirilecek madde varsa CI'da fark edilmesi için 2 ile çık
if ($warnings.Count -gt 0) { exit 2 }
exit 0
