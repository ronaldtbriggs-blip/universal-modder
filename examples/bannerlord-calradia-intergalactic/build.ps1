param(
    [string]$BannerlordBinDir = "$env:ProgramFiles (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $RootDir "src\CalradiaIntergalactic"
$OutDir = Join-Path $RootDir "Module\CalradiaIntergalactic\bin\Win64_Shipping_Client"
$CscPath = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$NetStandard = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll"

if (!(Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$Refs = @(
    "TaleWorlds.Core.dll",
    "TaleWorlds.Library.dll",
    "TaleWorlds.Localization.dll",
    "TaleWorlds.ObjectSystem.dll",
    "TaleWorlds.Engine.dll",
    "TaleWorlds.MountAndBlade.dll",
    "TaleWorlds.CampaignSystem.dll",
    "TaleWorlds.InputSystem.dll",
    "TaleWorlds.SaveSystem.dll",
    "TaleWorlds.DotNet.dll"
)

$RefArgs = @(
    "/reference:$NetStandard"
)
foreach ($r in $Refs) {
    $fullRef = Join-Path $BannerlordBinDir $r
    if (!(Test-Path $fullRef)) {
        throw "Missing reference assembly: $fullRef"
    }
    $RefArgs += "/reference:$fullRef"
}

$OutDll = Join-Path $OutDir "CalradiaIntergalactic.dll"
$Sources = Get-ChildItem -Path $SrcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

Write-Host "Compiling CalradiaIntergalactic.dll against Bannerlord v1.5.5..." -ForegroundColor Cyan
& $CscPath /target:library /optimize+ /platform:x64 /out:$OutDll $RefArgs $Sources

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

$DllInfo = Get-Item $OutDll
Write-Host ("SUCCESS: Built {0} ({1} bytes)" -f $DllInfo.FullName, $DllInfo.Length) -ForegroundColor Green
