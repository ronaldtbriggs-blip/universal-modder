$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $RootDir "src\CalradiaIntergalactic"
$OutDir = Join-Path $RootDir "Module\CalradiaIntergalactic\bin\Win64_Shipping_Client"
$RefDir = "C:\Users\gilde\.gemini\antigravity\brain\99193bd1-6603-4c5d-98b9-ed1e80565727\scratch\bannerlord-refs"
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

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
    "TaleWorlds.DotNet.dll",
    "TaleWorlds.ScreenSystem.dll",
    "TaleWorlds.MountAndBlade.View.dll",
    "SandBox.dll",
    "SandBox.View.dll",
    "System.Numerics.Vectors.dll",
    "System.ValueTuple.dll"
)

$RefArgs = @(
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll"
)
foreach ($r in $Refs) {
    $fullRef = Join-Path $RefDir $r
    if (!(Test-Path $fullRef)) {
        throw "Missing reference assembly: $fullRef"
    }
    $RefArgs += "/reference:$fullRef"
}

$OutDll = Join-Path $OutDir "CalradiaIntergalactic.dll"
$Sources = Get-ChildItem -Path $SrcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

Write-Host "Compiling CalradiaIntergalactic.dll against Bannerlord v1.5.5.109536..." -ForegroundColor Cyan
& $CscPath /target:library /optimize+ /platform:x64 /out:$OutDll $RefArgs $Sources

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

$DllInfo = Get-Item $OutDll
Write-Host ("SUCCESS: Built {0} ({1} bytes)" -f $DllInfo.FullName, $DllInfo.Length) -ForegroundColor Green

[xml]$sub = Get-Content (Join-Path $RootDir "Module\CalradiaIntergalactic\SubModule.xml")
[xml]$items = Get-Content (Join-Path $RootDir "Module\CalradiaIntergalactic\ModuleData\cig_sci_fi_items.xml")
[xml]$troops = Get-Content (Join-Path $RootDir "Module\CalradiaIntergalactic\ModuleData\cig_void_troops.xml")
Write-Host ("XML Verified: SubModule Id={0}, Sci-Fi Items={1}, Void Troops={2}" -f $sub.Module.Id.value, $items.Items.Item.Count, $troops.NPCCharacters.NPCCharacter.Count) -ForegroundColor Cyan

Add-Type -Path (Join-Path $RefDir "Mono.Cecil.dll")
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($OutDll)
foreach ($t in $asm.MainModule.Types) {
    if ($t.Name -ne "<Module>") {
        Write-Host ("Cecil Verified Type: {0} (Methods: {1}, NestedTypes: {2})" -f $t.FullName, $t.Methods.Count, $t.NestedTypes.Count) -ForegroundColor Gray
    }
}

$PreviewPath = Join-Path $RootDir "Preview.png"
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(800, 450)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect = New-Object System.Drawing.Rectangle(0, 0, 800, 450)
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 6, 10, 28),
    [System.Drawing.Color]::FromArgb(255, 22, 8, 42),
    45.0)
$g.FillRectangle($bgBrush, $rect)
$rng = New-Object System.Random(42)
$starBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 200, 240, 255))
for ($i = 0; $i -lt 160; $i++) {
    $sx = $rng.Next(0, 800)
    $sy = $rng.Next(0, 450)
    $sz = $rng.Next(1, 4)
    $g.FillEllipse($starBrush, $sx, $sy, $sz, $sz)
}
$cyanPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 0, 240, 255), 3)
$goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 255, 195, 0), 2)
$plasmaPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 255, 70, 20), 4)
$g.DrawEllipse($cyanPen, 480, 65, 260, 260)
$g.DrawEllipse($goldPen, 520, 105, 180, 180)
$g.DrawLine($cyanPen, 120, 310, 560, 180)
$g.DrawLine($plasmaPen, 640, 150, 240, 290)
$titleFont = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Bold)
$subFont = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$bodyFont = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Regular)
$cyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 240, 255))
$goldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 210, 60))
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 235, 245, 255))
$g.DrawString("CALRADIA: INTERGALACTIC", $titleFont, $cyanBrush, 38, 42)
$g.DrawString("TOTAL SCI-FI CONVERSION FOR BANNERLORD", $subFont, $goldBrush, 42, 95)
$g.DrawString("* Star Systems & Colony Moons (Towns = System Capitals, Villages = Colonies)", $bodyFont, $whiteBrush, 42, 145)
$g.DrawString("* 3D Directional Zero-G Space Battles: PEW-PEW Plasma Lances & WHHHAAAAM Torpedoes!", $bodyFont, $whiteBrush, 42, 175)
$g.DrawString("* Modular Voidship Designer + Star Traders Range-Band 5-to-1 Ship Combat", $bodyFont, $whiteBrush, 42, 205)
$g.DrawString("* S-Class Archeotech Drydock Upgrades & Starfield Reactor Power Pip Routing", $bodyFont, $whiteBrush, 42, 235)
$g.DrawString("* Walkable 3D Flagship Bridge with Rotating Holographic Starmap & Warp Core", $bodyFont, $whiteBrush, 42, 265)
$g.DrawString("* Stellaris System Edicts, Megastructures, Galactic Crises & 40k Vox-Hail Dialogues", $bodyFont, $whiteBrush, 42, 295)
$g.DrawString("ENGINEERED FOR STEAM DECK (STEAMOS / PROTON) & PC - v1.5.5", $subFont, $cyanBrush, 42, 385)
$bmp.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
$pInfo = Get-Item $PreviewPath
Write-Host ("Generated Preview.png ({0} bytes)" -f $pInfo.Length) -ForegroundColor Green


