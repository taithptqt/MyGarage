param(
    [string]$ManifestPath = ".\\scripts\\partshop-image-manifest.json",
    [string]$AppSettingsPath = ".\\appsettings.json",
    [string]$CloudName = $env:CLOUDINARY_CLOUD_NAME,
    [string]$ApiKey = $env:CLOUDINARY_API_KEY,
    [string]$ApiSecret = $env:CLOUDINARY_API_SECRET,
    [string]$Folder = $env:CLOUDINARY_FOLDER,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Sha1Hex {
    param([string]$Value)

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha1.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash) -replace "-", "").ToLowerInvariant()
    }
    finally {
        $sha1.Dispose()
    }
}

function Get-CloudinarySignature {
    param(
        [hashtable]$Parameters,
        [string]$Secret
    )

    $filtered = $Parameters.GetEnumerator() |
        Where-Object { $_.Value -ne $null -and $_.Value -ne "" } |
        Sort-Object Name |
        ForEach-Object { "{0}={1}" -f $_.Name, $_.Value }

    $toSign = ($filtered -join "&") + $Secret
    return Get-Sha1Hex -Value $toSign
}

function Invoke-CloudinaryUpload {
    param(
        [pscustomobject]$Item,
        [string]$CloudName,
        [string]$ApiKey,
        [string]$ApiSecret,
        [string]$Folder
    )

    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $publicId = "partshop/{0}" -f $Item.sku.ToLowerInvariant()
    $uploadParams = @{
        folder    = $Folder
        public_id = $publicId
        timestamp = $timestamp
    }

    $signature = Get-CloudinarySignature -Parameters $uploadParams -Secret $ApiSecret
    $uri = "https://api.cloudinary.com/v1_1/$CloudName/image/upload"

    Add-Type -AssemblyName System.Net.Http
    $httpClient = [System.Net.Http.HttpClient]::new()

    try {
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        foreach ($key in $uploadParams.Keys) {
            if ($uploadParams[$key]) {
                $content.Add([System.Net.Http.StringContent]::new([string]$uploadParams[$key]), $key)
            }
        }

        $content.Add([System.Net.Http.StringContent]::new($signature), "signature")
        $content.Add([System.Net.Http.StringContent]::new($ApiKey), "api_key")

        if ($Item.localFile) {
            $resolvedPath = Resolve-Path -LiteralPath $Item.localFile
            $stream = [System.IO.File]::OpenRead($resolvedPath)
            $fileContent = [System.Net.Http.StreamContent]::new($stream)
            $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")
            $content.Add($fileContent, "file", [System.IO.Path]::GetFileName($resolvedPath))
        }
        elseif ($Item.sourceUrl) {
            $content.Add([System.Net.Http.StringContent]::new([string]$Item.sourceUrl), "file")
        }
        else {
            throw "Item $($Item.sku) is missing sourceUrl/localFile."
        }

        $response = $httpClient.PostAsync($uri, $content).GetAwaiter().GetResult()
        $payload = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if (-not $response.IsSuccessStatusCode) {
            throw "Cloudinary upload failed for $($Item.sku): $payload"
        }

        return $payload | ConvertFrom-Json
    }
    finally {
        $httpClient.Dispose()
    }
}

function Invoke-DbCommand {
    param(
        [string]$ConnectionString,
        [string]$CommandText,
        [hashtable]$Parameters
    )

    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $CommandText
        foreach ($key in $Parameters.Keys) {
            $parameter = $command.Parameters.Add("@$key", [System.Data.SqlDbType]::Variant)
            $parameter.Value = if ($null -eq $Parameters[$key]) { [DBNull]::Value } else { $Parameters[$key] }
        }
        [void]$command.ExecuteNonQuery()
    }
    finally {
        $connection.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $AppSettingsPath)) {
    throw "appsettings not found: $AppSettingsPath"
}

$config = Get-Content -LiteralPath $AppSettingsPath -Raw | ConvertFrom-Json
$connectionString = $config.ConnectionStrings.GarageDbConnection

if (-not $connectionString) {
    throw "GarageDbConnection was not found in $AppSettingsPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json

if (-not $manifest) {
    throw "Manifest is empty: $ManifestPath"
}

if (-not $WhatIf) {
    if (-not $CloudName -or -not $ApiKey -or -not $ApiSecret) {
        throw "Cloudinary credentials are required. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET."
    }
}

foreach ($item in $manifest) {
    if (-not $item.sourceUrl -and -not $item.localFile) {
        Write-Host "Skipping $($item.sku): no sourceUrl/localFile provided."
        continue
    }

    if ($WhatIf) {
        Write-Host "[WhatIf] Would upload and update DB for $($item.sku)"
        continue
    }

    $upload = Invoke-CloudinaryUpload -Item $item -CloudName $CloudName -ApiKey $ApiKey -ApiSecret $ApiSecret -Folder $Folder
    $imageUrl = [string]$upload.secure_url
    $description = if ($item.description) { [string]$item.description } else { "Anh Part Shop - $($item.sku)" }

    if ($item.currentProductImageId) {
        $sql = @"
UPDATE dbo.ProductImages
SET ImageUrl = @ImageUrl,
    IsMain = 1,
    SortOrder = 0,
    Description = @Description
WHERE Id = @ProductImageId;
"@
        Invoke-DbCommand -ConnectionString $connectionString -CommandText $sql -Parameters @{
            ImageUrl = $imageUrl
            Description = $description
            ProductImageId = [long]$item.currentProductImageId
        }
    }
    else {
        $sql = @"
INSERT INTO dbo.ProductImages (ProductId, ImageUrl, IsMain, SortOrder, Description)
VALUES (@ProductId, @ImageUrl, 1, 0, @Description);
"@
        Invoke-DbCommand -ConnectionString $connectionString -CommandText $sql -Parameters @{
            ProductId = [long]$item.productId
            ImageUrl = $imageUrl
            Description = $description
        }
    }

    Write-Host "Updated image for $($item.sku): $imageUrl"
}
