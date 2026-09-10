[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?$')]
    [string]$AllowedEmailDomain = 'example.com'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'
$apiProject = Join-Path $repositoryRoot 'src/OpsDesk.Api/OpsDesk.Api.csproj'
$environmentLines = [System.Collections.Generic.List[string]]::new()

if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in [System.IO.File]::ReadAllLines($environmentFile)) {
        $environmentLines.Add($line)
    }
}

function New-UrlSafeSecret {
    param([int]$ByteCount = 48)

    $bytes = [byte[]]::new($ByteCount)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-EnvironmentValue {
    param([string]$Key)

    $prefix = "$Key="
    foreach ($line in $environmentLines) {
        if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $line.Substring($prefix.Length)
        }
    }

    return $null
}

function Set-EnvironmentValue {
    param(
        [string]$Key,
        [string]$Value
    )

    $prefix = "$Key="
    for ($index = 0; $index -lt $environmentLines.Count; $index++) {
        if ($environmentLines[$index].StartsWith($prefix, [StringComparison]::Ordinal)) {
            $environmentLines[$index] = "$prefix$Value"
            return
        }
    }

    $environmentLines.Add("$prefix$Value")
}

function Get-OrCreateSecret {
    param([string]$Key)

    $existing = Get-EnvironmentValue -Key $Key
    if (-not [string]::IsNullOrWhiteSpace($existing) -and -not $existing.StartsWith('change-me')) {
        return $existing
    }

    return New-UrlSafeSecret
}

$adminUsername = Get-EnvironmentValue -Key 'KEYCLOAK_ADMIN_USERNAME'
if ([string]::IsNullOrWhiteSpace($adminUsername)) {
    $adminUsername = 'admin'
}

$adminPassword = Get-OrCreateSecret -Key 'KEYCLOAK_ADMIN_PASSWORD'
$clientSecret = Get-OrCreateSecret -Key 'OPSDESK_SSO_CLIENT_SECRET'
$demoPassword = Get-OrCreateSecret -Key 'KEYCLOAK_DEMO_USER_PASSWORD'

Set-EnvironmentValue -Key 'KEYCLOAK_ADMIN_USERNAME' -Value $adminUsername
Set-EnvironmentValue -Key 'KEYCLOAK_ADMIN_PASSWORD' -Value $adminPassword
Set-EnvironmentValue -Key 'OPSDESK_SSO_CLIENT_SECRET' -Value $clientSecret
Set-EnvironmentValue -Key 'KEYCLOAK_DEMO_USER_PASSWORD' -Value $demoPassword
[System.IO.File]::WriteAllLines(
    $environmentFile,
    $environmentLines,
    [System.Text.UTF8Encoding]::new($false))

function Set-OpsDeskUserSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Key,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    & dotnet user-secrets set $Key $Value --project $apiProject | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set .NET User Secret '$Key'."
    }
}

Set-OpsDeskUserSecret -Key 'Sso:Enabled' -Value 'false'
Set-OpsDeskUserSecret -Key 'Sso:Authority' -Value 'http://localhost:8180/realms/opsdesk'
Set-OpsDeskUserSecret -Key 'Sso:ClientId' -Value 'opsdesk-api'
Set-OpsDeskUserSecret -Key 'Sso:ClientSecret' -Value $clientSecret
Set-OpsDeskUserSecret -Key 'Sso:PublicOrigin' -Value 'http://localhost:5044'
Set-OpsDeskUserSecret -Key 'Sso:AllowedEmailDomains:0' -Value $AllowedEmailDomain.ToLowerInvariant()
Set-OpsDeskUserSecret -Key 'Sso:Enabled' -Value 'true'

Write-Host 'Local SSO configuration is ready.'
Write-Host 'Keycloak URL: http://localhost:8180'
Write-Host 'Demo user: sso.agent@example.com'
Write-Host 'Generated passwords and client secret are stored only in .env/User Secrets.'
