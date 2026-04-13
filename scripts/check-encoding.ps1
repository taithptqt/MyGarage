param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

$rootPath = (Resolve-Path $Root).Path

$extensions = @(
    "*.cs", "*.sql", "*.json", "*.js", "*.jsx", "*.ts", "*.tsx",
    "*.css", "*.scss", "*.md", "*.yml", "*.yaml", "*.html"
)

$excludeDirPattern = "\\(\\.git|node_modules|dist|bin|obj|_build_out|artifacts|coverage)\\"

$files = Get-ChildItem -Path $rootPath -Recurse -File -Include $extensions |
    Where-Object { $_.FullName -notmatch $excludeDirPattern }

# Detect common mojibake sequences (UTF-8 decoded as Latin-1/Windows-1252)
# while avoiding false positives on valid Vietnamese letters like "Ã" in "ĐÃ".
$patterns = @(
    "ï¿½",
    "Ã[¡¢£¤¥¦§¨©ª«¬®¯°±²³´µ¶·¸¹º»¼½¾¿]",
    "Ä‘",
    "Æ°",
    "Æ¡",
    "áº[\x80-\xBF]",
    "á»[\x80-\xBF]",
    "â€[\x80-\xBF]?"
)

$findings = @()
foreach ($pattern in $patterns) {
    $matches = $files | Select-String -Pattern $pattern -Encoding UTF8 -CaseSensitive
    if ($matches) {
        $findings += $matches
    }
}

if ($findings.Count -gt 0) {
    Write-Host ""
    Write-Host "Found possible encoding issues (mojibake):" -ForegroundColor Red
    $findings |
        Sort-Object Path, LineNumber -Unique |
        ForEach-Object {
            Write-Host ("{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim())
        }
    Write-Host ""
    Write-Host "Please fix these lines before commit." -ForegroundColor Yellow
    exit 1
}

Write-Host "Encoding check passed." -ForegroundColor Green
