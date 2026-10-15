param(
    [string]$BannerlordBinDir = "$env:ProgramFiles (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
)

$ErrorActionPreference = "Stop"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$netstandard = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll"

$modVerifierOut = Join-Path $PSScriptRoot "bin\ModVerifier"
$moduleBinOut = Join-Path $PSScriptRoot "Module\SteamDeckOverhaul\bin\Win64_Shipping_Client"
New-Item -ItemType Directory -Force -Path $modVerifierOut, $moduleBinOut | Out-Null

Write-Host "Building ModVerifier.exe..."
& $csc /target:exe /optimize+ /out:"$modVerifierOut\ModVerifier.exe" `
    (Join-Path $PSScriptRoot "src\ModVerifier\ModVerifier.cs")

Write-Host "Building SteamDeckOverhaul.dll..."
$refs = @(
    $netstandard,
    (Join-Path $BannerlordBinDir "TaleWorlds.DotNet.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.Core.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.Library.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.Localization.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.ObjectSystem.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.CampaignSystem.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.MountAndBlade.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.Engine.dll"),
    (Join-Path $BannerlordBinDir "TaleWorlds.SaveSystem.dll")
) | ForEach-Object { "/r:`"$_`"" }

& $csc /target:library /optimize+ /out:"$moduleBinOut\SteamDeckOverhaul.dll" $refs `
    (Join-Path $PSScriptRoot "src\SteamDeckOverhaul\SteamDeckOverhaulSubModule.cs") `
    (Join-Path $PSScriptRoot "src\SteamDeckOverhaul\ArmorSmithingBehavior.cs") `
    (Join-Path $PSScriptRoot "src\SteamDeckOverhaul\ClanArmyPointSystemBehavior.cs") `
    (Join-Path $PSScriptRoot "src\SteamDeckOverhaul\SteamDeckCampaignAndCombatOverhaul.cs")

Write-Host "Build succeeded!"
