#Requires -Version 5.1
[CmdletBinding()]
param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'

$homeDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenAI\ChatGPTFix'
$installedAppRepair = Join-Path $homeDir 'Repair-ChatGPT.ps1'
$bundledAppRepair = Join-Path $PSScriptRoot 'Repair-ChatGPT.ps1'
$animationRepair = Join-Path $homeDir 'Repair-Seamless.ps1'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Get-Status {
    $appRepair = if (Test-Path -LiteralPath $installedAppRepair -PathType Leaf) {
        $installedAppRepair
    } else { $bundledAppRepair }
    $animation = [pscustomobject]@{ enabled = $false; reason = '尚未安装启动动画。' }
    if (Test-Path -LiteralPath $animationRepair -PathType Leaf) {
        try {
            $animation = (& $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $animationRepair -CheckOnly |
                Select-Object -Last 1) | ConvertFrom-Json
        } catch { $animation = [pscustomobject]@{ enabled = $false; reason = $_.Exception.Message } }
    }
    [pscustomobject]@{ appRepair = $appRepair; appRepairAvailable = (Test-Path -LiteralPath $appRepair -PathType Leaf); animation = $animation }
}

if ($CheckOnly) { Get-Status | ConvertTo-Json -Depth 5 -Compress; exit 0 }

while ($true) {
    $status = Get-Status
    Clear-Host
    Write-Host 'ChatGPT 与启动动画修复' -ForegroundColor Cyan
    Write-Host ('官方 App 修复：' + $(if ($status.appRepairAvailable) { '可用' } else { '不可用' }))
    Write-Host ('启动动画：' + $status.animation.reason)
    Write-Host ''
    Write-Host '1  修复官方 App 无法打开'
    Write-Host '2  检查或修复启动动画'
    Write-Host '3  打开官方 App'
    Write-Host '0  退出'
    $choice = Read-Host '请选择'
    switch ($choice) {
        '1' {
            if ($status.appRepairAvailable) {
                & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $status.appRepair
            } else { Write-Host '未找到官方 App 修复脚本。' -ForegroundColor Yellow }
        }
        '2' {
            if (Test-Path -LiteralPath $animationRepair -PathType Leaf) {
                & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $animationRepair -NoPause
            } else { Write-Host '启动动画尚未安装。' -ForegroundColor Yellow }
        }
        '3' {
            $package = Get-AppxPackage -Name OpenAI.Codex -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($package) { Start-Process explorer.exe ('shell:AppsFolder\' + $package.PackageFamilyName + '!App') }
            else { Write-Host '未找到官方 App。' -ForegroundColor Yellow }
        }
        '0' { exit 0 }
        default { Write-Host '请输入 0–3。' }
    }
    $null = Read-Host '按回车返回菜单'
}
