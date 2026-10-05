<#
.SYNOPSIS
    Bir .NET çözümünü AutoMapper'dan VeloxMapper'a taşır.

.DESCRIPTION
    - .csproj / Directory.Packages.props / packages.config içindeki AutoMapper paket referanslarını VeloxMapper ile değiştirir
      (AutoMapper.Extensions.Microsoft.DependencyInjection ve AutoMapper.Extensions.EnumMapping kaldırılır; işlevleri VeloxMapper'da yerleşiktir).
    - .cs dosyalarındaki AutoMapper namespace'lerini VeloxMapper karşılıklarıyla değiştirir.
    - AddAutoMapper(...) çağrılarını AddVeloxMapper(...) olarak günceller (AddAutoMapper takma adı da çalışır).
    - AutoMapper'a özgü istisna adlarını VeloxMapper karşılıklarıyla değiştirir.
    - Elle gözden geçirilmesi gereken API kullanımlarını raporlar.

    Betik yalnızca metin değişikliği yapar; dosya kodlamasını (BOM dahil) ve satır sonlarını korur.
    Çalıştırmadan önce değişikliklerinizi commit edin; ardından "git diff" ile sonucu inceleyin.

.PARAMETER Path
    Taranacak kök klasör (varsayılan: geçerli klasör).

.PARAMETER Version
    Eklenecek VeloxMapper paket sürümü (varsayılan: 6.0.0).

.PARAMETER DryRun
    Dosyaları değiştirmeden yalnızca yapılacak değişiklikleri listeler.

.EXAMPLE
    pwsh ./migrate-from-automapper.ps1 -Path ./src
.EXAMPLE
    pwsh ./migrate-from-automapper.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Path = ".",
    [string]$Version = "6.0.0",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $Path).Path
$excludedDirs = @("bin", "obj", ".git", ".vs", "node_modules", "packages")

function Get-SourceFiles([string[]]$patterns) {
    # Not: Windows PowerShell 5.1'de -LiteralPath ile -Include yok sayılır; ad filtresi açıkça uygulanır.
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $name = $_.Name
        ($patterns | Where-Object { $name -like $_ }).Count -gt 0
    } | Where-Object {
        $segments = $_.FullName.Substring($root.Length).Split([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        -not ($segments | Where-Object { $excludedDirs -contains $_ })
    }
}

function Read-TextFile([string]$file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    $text = $encoding.GetString($bytes)
    if ($hasBom) { $text = $text.Substring(1) }
    return @{ Text = $text; Encoding = $encoding }
}

function Write-TextFile([string]$file, [string]$text, $encoding) {
    if (-not $DryRun) { [IO.File]::WriteAllText($file, $text, $encoding) }
}

$changedFiles = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

# ── 1. Paket referansları ────────────────────────────────────────────────
$projectPatterns = @("*.csproj", "*.fsproj", "*.vbproj", "Directory.Packages.props", "Directory.Build.props", "packages.config")
foreach ($file in Get-SourceFiles $projectPatterns) {
    $content = Read-TextFile $file.FullName
    $text = $content.Text
    $original = $text

    # Yerleşik hale gelen eklenti paketlerini kaldır
    $text = [regex]::Replace($text, '\s*<Package(Reference|Version)\s+Include="AutoMapper\.Extensions\.(Microsoft\.DependencyInjection|EnumMapping)"[^>]*?(/>|>[\s\S]*?</Package(Reference|Version)>)', '')
    $text = [regex]::Replace($text, '\s*<package\s+id="AutoMapper\.Extensions\.(Microsoft\.DependencyInjection|EnumMapping)"[^>]*/>', '')

    # AutoMapper → VeloxMapper
    $text = [regex]::Replace($text, '<Package(Reference|Version)(\s+)Include="AutoMapper"(\s+)Version="[^"]*"', "<Package`$1`$2Include=`"VeloxMapper`"`$3Version=`"$Version`"")
    $text = [regex]::Replace($text, '<Package(Reference|Version)(\s+)Include="AutoMapper"(\s*/?>)', "<Package`$1`$2Include=`"VeloxMapper`"`$3")
    $text = [regex]::Replace($text, '<package\s+id="AutoMapper"\s+version="[^"]*"', "<package id=`"VeloxMapper`" version=`"$Version`"")
    # Version özniteliği Include'dan önce yazılmışsa
    $text = [regex]::Replace($text, '<Package(Reference|Version)(\s+)Version="[^"]*"(\s+)Include="AutoMapper"', "<Package`$1`$2Version=`"$Version`"`$3Include=`"VeloxMapper`"")
    # Sürüm alt öğe olarak yazılmışsa: <PackageReference Include="AutoMapper"><Version>...</Version>
    $text = [regex]::Replace($text, '(<PackageReference\s+Include="VeloxMapper"\s*>\s*<Version>)[^<]*(</Version>)', "`${1}$Version`${2}")

    if ($text -match 'Include="AutoMapper\.(Collection|EF6|Data|Extensions\.ExpressionMapping|AspNetCore\.OData)') {
        $warnings.Add("$($file.FullName): '$($Matches[0])' paketi için VeloxMapper karşılığı yok; kullanımını elle gözden geçirin.")
    }

    if ($text -ne $original) {
        Write-TextFile $file.FullName $text $content.Encoding
        $changedFiles.Add($file.FullName)
    }
}

# ── 2. C# kaynak kodu ────────────────────────────────────────────────────
$replacements = [ordered]@{
    '(?m)^(\s*)using\s+AutoMapper\.Extensions\.EnumMapping\s*;\s*\r?\n' = ''
    '(?m)^(\s*)using\s+AutoMapper\.Extensions\.ExpressionMapping\s*;' = '$1using VeloxMapper; // TODO: ExpressionMapping karşılığı yok, kullanımı gözden geçirin'
    '\busing\s+AutoMapper\.QueryableExtensions\s*;' = 'using VeloxMapper.QueryableExtensions;'
    '\busing\s+AutoMapper\.Configuration\.Annotations\s*;' = 'using VeloxMapper.Configuration.Annotations;'
    '\busing\s+AutoMapper\.Configuration\s*;' = 'using VeloxMapper.Configuration;'
    '\busing\s+AutoMapper\s*;' = 'using VeloxMapper;'
    '\bglobal\s+using\s+AutoMapper\s*;' = 'global using VeloxMapper;'
    '\bAutoMapperMappingException\b' = 'VeloxMapper.Exceptions.VeloxMappingException'
    '\bAutoMapperConfigurationException\b' = 'VeloxMapper.Exceptions.VeloxValidationException'
    '\bDuplicateTypeMapConfigurationException\b' = 'VeloxMapper.Exceptions.VeloxConfigurationException'
    '\.AddAutoMapper\(' = '.AddVeloxMapper('
    '\bAutoMapper\.QueryableExtensions\.' = 'VeloxMapper.QueryableExtensions.'
    '\bAutoMapper\.(IMapper|Mapper|Profile|MapperConfiguration|IConfigurationProvider|ResolutionContext|IValueResolver|IMemberValueResolver|IValueConverter|ITypeConverter|IMappingAction|IMappingExpression|IMemberConfigurationExpression|IMapperConfigurationExpression|IProfileExpression|MemberList|AutoMapAttribute|AutoMap)\b' = 'VeloxMapper.$1'
}

$reviewPatterns = [ordered]@{
    '\.Internal\(\)' = "cfg.Internal() AutoMapper'ın iç API'sidir; VeloxMapper'da ForAllPropertyMaps/ForAllMaps kullanın."
    '\bUseAsDataSource\b' = "UseAsDataSource (AutoMapper.Extensions.ExpressionMapping) desteklenmez; ProjectTo kullanın."
    '\bEqualityComparison\b' = "EqualityComparison (AutoMapper.Collection) desteklenmez; koleksiyonlar Clear+Add ile eşlenir."
    '\.AsProxy\(\)' = "AsProxy arayüz proxy'leri üretmez; somut bir hedef tür kullanın."
}

foreach ($file in Get-SourceFiles @("*.cs")) {
    $content = Read-TextFile $file.FullName
    $text = $content.Text
    $original = $text

    foreach ($pattern in $replacements.Keys) {
        $text = [regex]::Replace($text, $pattern, $replacements[$pattern])
    }

    foreach ($pattern in $reviewPatterns.Keys) {
        if ($text -match $pattern) {
            $warnings.Add("$($file.FullName): $($reviewPatterns[$pattern])")
        }
    }

    if ($text -ne $original) {
        Write-TextFile $file.FullName $text $content.Encoding
        $changedFiles.Add($file.FullName)
    }
}

# ── 3. Rapor ─────────────────────────────────────────────────────────────
$mode = if ($DryRun) { "DEĞİŞTİRİLECEK" } else { "DEĞİŞTİRİLDİ" }
Write-Host ""
Write-Host "VeloxMapper geçiş betiği — $($changedFiles.Count) dosya $mode" -ForegroundColor Cyan
$changedFiles | ForEach-Object { Write-Host "  ~ $($_.Substring($root.Length).TrimStart('\','/'))" }

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

# Elle gözden geçirilecek madde varsa CI'da fark edilmesi için 2 ile çık
if ($warnings.Count -gt 0) { exit 2 }
