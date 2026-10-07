#Requires -Version 5.1
[CmdletBinding()]
param([switch]$CheckOnly, [switch]$NoPause)
$ErrorActionPreference = 'Stop'
$animation = Join-Path $PSScriptRoot 'Animation'
$exe = Join-Path $animation 'SeamlessAgent.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$taskName = 'CodexSeamlessAnimation'
$command = '"' + $exe + '" "' + $animation + '"'
$installed = (Test-Path -LiteralPath $exe -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $animation 'wallpaper.html') -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $animation 'assets\artwork.jpg') -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $animation 'assets\contours.js') -PathType Leaf)
$sourceFile = Join-Path $animation 'source-repo.txt'
if ($installed -and (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
    $sourceVideo = Join-Path (Get-Content -LiteralPath $sourceFile -Raw) 'assets\wallpaper.mp4'
    if ((Test-Path -LiteralPath $sourceVideo -PathType Leaf) -and
        -not (Test-Path -LiteralPath (Join-Path $animation 'assets\wallpaper.mp4') -PathType Leaf)) {
        $installed = $false
    }
}
$registered = (Get-ItemProperty -Path $runKey -Name 'CodexSeamlessAnimation' -ErrorAction SilentlyContinue).CodexSeamlessAnimation -eq $command
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue | Select-Object -First 1
$taskRegistered = $null -ne $task -and $task.State -ne 'Disabled' -and
    $task.Actions.Execute -ieq $exe -and $task.Actions.Arguments -eq ('"' + $animation + '"') -and
    @($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' -and $_.Enabled }).Count -gt 0
$running = @(Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $exe }).Count -gt 0
$enabled = $installed -and $registered -and $taskRegistered -and $running
$status = [pscustomobject]@{
    enabled = $enabled
    reason = if ($enabled) { '启动动画监听正常；无需修复。' } elseif (-not $installed) { '启动动画文件缺失。' } elseif (-not $registered -or -not $taskRegistered) { '启动动画登录入口缺失。' } else { '启动动画监听未运行。' }
}
if ($CheckOnly) { $status | ConvertTo-Json -Compress; exit 0 }
if ($enabled) { Write-Host $status.reason; if (-not $NoPause) { $null = Read-Host '按回车关闭' }; exit 0 }
try {
    if (-not $installed) {
        $sourceFile = Join-Path $animation 'source-repo.txt'
        if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { throw '动画文件和安装来源均缺失。' }
        $installer = Join-Path (Get-Content -LiteralPath $sourceFile -Raw) 'tools\install-seamless-windows.ps1'
        if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw '未找到动画安装来源。' }
        & $installer
    } else {
        New-ItemProperty -Path $runKey -Name 'CodexSeamlessAnimation' -Value $command -PropertyType String -Force | Out-Null
        if (-not $taskRegistered) {
            $userId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
            $action = New-ScheduledTaskAction -Execute $exe -Argument ('"' + $animation + '"')
            $trigger = New-ScheduledTaskTrigger -AtLogOn -User $userId
            $trigger.Delay = 'PT2S'
            $principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited
            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
        }
        if (-not $running) {
            Start-ScheduledTask -TaskName $taskName
            Start-Sleep -Milliseconds 750
            $afterStart = @(Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -ieq $exe })
            if ($afterStart.Count -eq 0) { throw '计划任务未能启动动画监听程序。' }
        }
    }
    Write-Host '启动动画监听已修复。'
} catch { Write-Host ('修复失败：' + $_.Exception.Message) -ForegroundColor Red; if (-not $NoPause) { $null = Read-Host '按回车关闭' }; exit 2 }
if (-not $NoPause) { $null = Read-Host '按回车关闭' }
