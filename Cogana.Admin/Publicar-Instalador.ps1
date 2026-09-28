# Publica y empaqueta el instalador de Cogana Admin con Velopack.
# Uso:
#   .\Publicar-Instalador.ps1                          -> publica y empaqueta (framework-dependent, paquetes pequenos)
#   .\Publicar-Instalador.ps1 -Version 1.4.0           -> usa una version concreta
#   .\Publicar-Instalador.ps1 -SinPublicar             -> reempaqueta sin recompilar
#   .\Publicar-Instalador.ps1 -Autocontenida           -> incluye el runtime .NET (paquetes >50 MB)
#   .\Publicar-Instalador.ps1 -Github "usuario/repo"   -> ademas sube la version a GitHub Releases
#
# Con -Github, tras empaquetar se publica automaticamente en GitHub Releases
# (requiere GitHub CLI: winget install GitHub.cli, y 'gh auth login').
# La app detecta GitHub como canal si la variable de entorno COGANA_GITHUB_REPO
# esta definida con el repositorio (ejemplo: COGANA_GITHUB_REPO=usuario/repo).
# Sin esa variable, la app sigue consultando el bucket 'actualizaciones' de Supabase.
param(
    [string]$Version = "",
    [switch]$SinPublicar,
    [switch]$Autocontenida,
    [string]$Github = "",
    [string]$GithubToken = ""
)

$ErrorActionPreference = "Stop"
$raiz = $PSScriptRoot

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "La herramienta vpk no esta instalada. Ejecuta: dotnet tool install -g vpk"
}

if (-not $Version) {
    $csproj = Join-Path $raiz "Cogana.Admin.Escritorio\Cogana.Admin.Escritorio.csproj"
    $contenido = Get-Content $csproj -Raw
    if ($contenido -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1].Trim()
    } else {
        $Version = "1.0.0"
    }
}

if ($Autocontenida) {
    $modo = "autocontenida (incluye runtime .NET)"
} else {
    $modo = "framework-dependent (requiere runtime .NET en el equipo)"
}

Write-Host "== Empaquetando Cogana Admin v$Version - modo $modo ==" -ForegroundColor Cyan

if (-not $SinPublicar) {
    Write-Host "-- Publicando (Release, win-x64) --"
    dotnet publish "$raiz\Cogana.Admin.Escritorio\Cogana.Admin.Escritorio.csproj" `
        -c Release -r win-x64 --self-contained $Autocontenida.IsPresent
    if ($LASTEXITCODE -ne 0) { throw "La publicacion de .NET fallo." }
}

$carpetaPublicacion = Join-Path $raiz "Cogana.Admin.Escritorio\bin\Release\net10.0-windows\win-x64\publish"
if (-not (Test-Path (Join-Path $carpetaPublicacion "Cogana.Admin.Escritorio.exe"))) {
    throw "No se encontro la publicacion en: $carpetaPublicacion. Ejecuta sin -SinPublicar."
}

Write-Host "-- Empaquetando con vpk --"
$salida = Join-Path $raiz "Releases"

# Si esta misma version ya fue empaquetada antes, retirarla para poder regenerarla
Get-ChildItem $salida -Filter "CoganaAdmin-$Version-*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force
Remove-Item (Join-Path $salida "RELEASES") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $salida "releases.win.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $salida "assets.win.json") -Force -ErrorAction SilentlyContinue

$argumentosVpk = @(
    'pack',
    '--packId', 'CoganaAdmin',
    '--packVersion', $Version,
    '--packDir', $carpetaPublicacion,
    '--mainExe', 'Cogana.Admin.Escritorio.exe',
    '--runtime', 'win-x64'
)

if (-not $Autocontenida) {
    # El Setup instala el runtime .NET Desktop si el equipo no lo tiene.
    $argumentosVpk += @('--framework', 'net10-x64-desktop')
}

$salida = Join-Path $raiz "Releases"

Push-Location $raiz
try {
    vpk @argumentosVpk
    if ($LASTEXITCODE -ne 0) { throw "El empaquetado con vpk fallo." }
}
finally {
    Pop-Location
}

if ($Github) {
    if (-not $GithubToken) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw "Para publicar en GitHub instala GitHub CLI: winget install GitHub.cli  y luego ejecuta: gh auth login"
        }
        $GithubToken = gh auth token
        if (-not $GithubToken) { throw "gh no esta autenticado. Ejecuta: gh auth login" }
    }

    Write-Host "-- Subiendo a GitHub Releases ($Github) --"
    vpk upload github `
        -o $salida `
        --repoUrl "https://github.com/$Github" `
        --token $GithubToken `
        --publish `
        --tag "v$Version" `
        --releaseName "Cogana Admin v$Version"
    if ($LASTEXITCODE -ne 0) { throw "La subida a GitHub fallo." }
}

Write-Host ""
Write-Host "Version $Version lista. Paquetes en: $salida" -ForegroundColor Green
Get-ChildItem $salida -Recurse -File | ForEach-Object {
    $pesoMB = [math]::Round($_.Length / 1MB, 1)
    $nombreRelativo = $_.FullName.Substring($salida.Length + 1)
    Write-Host "  $nombreRelativo  ($pesoMB MB)"
}
Write-Host ""
Write-Host "Siguientes pasos:" -ForegroundColor Yellow
if ($Github) {
    Write-Host "  Publicado en GitHub Releases. La app con COGANA_GITHUB_REPO definido lo detectara sola."
} else {
    Write-Host "  1. En Supabase Storage, vacia el bucket publico 'actualizaciones' (borra versiones anteriores)."
    Write-Host "  2. Sube TODO el contenido de la carpeta Releases a la RAIZ del bucket."
    Write-Host "  3. En cada equipo con la app instalada, pulsa 'Buscar actualizaciones' en el panel."
}
