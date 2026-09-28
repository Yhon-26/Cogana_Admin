# Publica y empaqueta el instalador de Cogana Admin con Velopack.
# Uso:
#   .\Publicar-Instalador.ps1                     -> publica y empaqueta (framework-dependent, paquetes pequenos)
#   .\Publicar-Instalador.ps1 -Version 1.2.0      -> publica y empaqueta con una version concreta
#   .\Publicar-Instalador.ps1 -SinPublicar        -> reempaqueta sin recompilar (usa la ultima publicacion)
#   .\Publicar-Instalador.ps1 -Autocontenida      -> incluye el runtime .NET (paquetes >50 MB; NO caben en Supabase gratis)
#
# Modo normal (framework-dependent): los paquetes de actualizacion son pequenos y
# caben en el limite de 50 MB por archivo del plan gratuito de Supabase. El
# instalador Setup.exe configura el runtime .NET automaticamente en equipos nuevos.
#
# Al terminar, sube el contenido de .\Releases a la raiz del bucket publico
# "actualizaciones" de Supabase Storage para que la app lo detecte.
param(
    [string]$Version = "",
    [switch]$SinPublicar,
    [switch]$Autocontenida
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

Push-Location $raiz
try {
    vpk @argumentosVpk
    if ($LASTEXITCODE -ne 0) { throw "El empaquetado con vpk fallo." }
}
finally {
    Pop-Location
}

$salida = Join-Path $raiz "Releases"
Write-Host ""
Write-Host "Instalador y paquetes generados en: $salida" -ForegroundColor Green
Get-ChildItem $salida -Recurse -File | ForEach-Object {
    $pesoMB = [math]::Round($_.Length / 1MB, 1)
    $nombreRelativo = $_.FullName.Substring($salida.Length + 1)
    Write-Host "  $nombreRelativo  ($pesoMB MB)"
}
Write-Host ""
Write-Host "Siguientes pasos:" -ForegroundColor Yellow
if ($Autocontenida) {
    Write-Host "  ATENCION: los paquetes autocontenidos superan los 50 MB del plan gratuito de Supabase."
    Write-Host "  Este modo es para instalar directamente con el Setup.exe, no para el bucket de actualizaciones."
} else {
    Write-Host "  1. En Supabase Storage, vacia el bucket publico 'actualizaciones' (borra versiones anteriores)."
    Write-Host "  2. Sube TODO el contenido de la carpeta Releases a la RAIZ del bucket."
    Write-Host "  3. En cada equipo con la app instalada, pulsa 'Buscar actualizaciones' en el panel."
}
