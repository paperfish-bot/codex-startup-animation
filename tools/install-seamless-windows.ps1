#Requires -Version 5.1
[CmdletBinding()]
param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$homeDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenAI\ChatGPTFix'
$animation = Join-Path $homeDir 'Animation'
$exe = Join-Path $animation 'SeamlessAgent.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'CodexSeamlessAnimation'
$taskName = 'CodexSeamlessAnimation'
$command = '"' + $exe + '" "' + $animation + '"'
$installed = (Test-Path -LiteralPath $exe -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $animation 'wallpaper.html') -PathType Leaf) -and
    ((-not (Test-Path -LiteralPath (Join-Path $repo 'assets\wallpaper.mp4') -PathType Leaf)) -or
     (Test-Path -LiteralPath (Join-Path $animation 'assets\wallpaper.mp4') -PathType Leaf))
$registered = (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue).$runName -eq $command
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue | Select-Object -First 1
$taskRegistered = $null -ne $task -and $task.State -ne 'Disabled' -and
    $task.Actions.Execute -ieq $exe -and $task.Actions.Arguments -eq ('"' + $animation + '"') -and
    @($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' -and $_.Enabled }).Count -gt 0
if ($CheckOnly) {
    [pscustomobject]@{ installed=$installed; registered=$registered; taskRegistered=$taskRegistered; path=$exe } | ConvertTo-Json -Compress
    exit 0
}
if (-not (Get-AppxPackage -Name OpenAI.Codex -ErrorAction SilentlyContinue | Select-Object -First 1)) {
    throw '未找到官方 Codex App，安装已停止。'
}
$files = @(
    'index.html','wallpaper.html','style.css','animation.js','image-settings.js',
    'assets\avatar.jpg','assets\artwork.jpg','assets\contours.js','assets\config.js',
    'windows\SeamlessAgent.cs','windows\SeamlessOverlay.cs','windows\SeamlessWallpaper.cs',
    'windows\Repair-ChatGPT.ps1','windows\Startup-Repair-Menu.ps1',
    'windows\webview2\Microsoft.Web.WebView2.Core.dll',
    'windows\webview2\Microsoft.Web.WebView2.WinForms.dll',
    'windows\webview2\WebView2Loader.dll'
)
if (Test-Path -LiteralPath (Join-Path $repo 'assets\wallpaper.mp4') -PathType Leaf) {
    $files += 'assets\wallpaper.mp4'
}
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $repo $relative) -PathType Leaf)) { throw "安装文件缺失：$relative" }
}
if (-not (Test-Path -LiteralPath (Join-Path $repo 'windows\Repair-Seamless.ps1') -PathType Leaf)) {
    throw '安装文件缺失：windows\Repair-Seamless.ps1'
}
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) { throw '未找到 Windows C# 编译器。' }
foreach ($process in @(Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue)) {
    if ($process.Path -ieq $exe) { Stop-Process -Id $process.Id; $process.WaitForExit(5000) | Out-Null }
}
$backup = Join-Path $homeDir ('Backups\Seamless-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$null = New-Item -ItemType Directory -Path $backup -Force
$null = New-Item -ItemType Directory -Path $animation -Force
foreach ($relative in $files) {
    $targetRelative = if ($relative -like 'windows\webview2\*' -or $relative -like 'windows\Seamless*.cs') { [IO.Path]::GetFileName($relative) } else { $relative }
    $target = Join-Path $animation $targetRelative
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        $saved = Join-Path $backup $targetRelative
        $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($saved)) -Force
        Copy-Item -LiteralPath $target -Destination $saved -Force
    }
    $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $target -Force
}
$sourceVideo = Join-Path $repo 'assets\wallpaper.mp4'
$installedVideo = Join-Path $animation 'assets\wallpaper.mp4'
if (-not (Test-Path -LiteralPath $sourceVideo -PathType Leaf) -and
    (Test-Path -LiteralPath $installedVideo -PathType Leaf)) {
    $savedVideo = Join-Path $backup 'assets\wallpaper.mp4'
    $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($savedVideo)) -Force
    Move-Item -LiteralPath $installedVideo -Destination $savedVideo
}
$repairName = 'Repair-Seamless.ps1'
$repairTarget = Join-Path $homeDir $repairName
if (Test-Path -LiteralPath $repairTarget -PathType Leaf) {
    Copy-Item -LiteralPath $repairTarget -Destination (Join-Path $backup $repairName) -Force
}
Copy-Item -LiteralPath (Join-Path $repo ('windows\' + $repairName)) -Destination $repairTarget -Force
$repairShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) '启动动画修复.lnk'
if (-not (Test-Path -LiteralPath $repairShortcut -PathType Leaf)) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($repairShortcut)
    $shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $animation 'windows\Startup-Repair-Menu.ps1') + '"'
    $shortcut.WorkingDirectory = $homeDir
    $shortcut.Description = '需要时检查或修复官方 App 与启动动画'
    $shortcut.Save()
}
Set-Content -LiteralPath (Join-Path $animation 'source-repo.txt') -Value $repo -NoNewline
$built = Join-Path $backup 'SeamlessAgent.exe'
$core = '/reference:' + (Join-Path $animation 'Microsoft.Web.WebView2.Core.dll')
$forms = '/reference:' + (Join-Path $animation 'Microsoft.Web.WebView2.WinForms.dll')
& $csc /nologo /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $core $forms ('/out:'+$built) (Join-Path $animation 'SeamlessOverlay.cs') (Join-Path $animation 'SeamlessWallpaper.cs') (Join-Path $animation 'SeamlessAgent.cs')
if ($LASTEXITCODE -ne 0) { throw '监听程序编译失败，旧版本备份已保留。' }
foreach ($process in @(Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue)) {
    if ($process.Path -ieq $exe) { Stop-Process -Id $process.Id; $process.WaitForExit(5000) | Out-Null }
}
if (Test-Path -LiteralPath $exe -PathType Leaf) { Copy-Item -LiteralPath $exe -Destination (Join-Path $backup 'SeamlessAgent-previous.exe') -Force }
Copy-Item -LiteralPath $built -Destination $exe -Force
New-ItemProperty -Path $runKey -Name $runName -Value $command -PropertyType String -Force | Out-Null
$userId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $exe -Argument ('"' + $animation + '"')
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $userId
$trigger.Delay = 'PT2S'
$principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
$agent = $null
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    Start-Sleep -Milliseconds 250
    $agent = Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -ieq $exe } | Select-Object -First 1
    if ($agent) { break }
}
if (-not $agent) { throw '计划任务未能启动动画监听程序，请检查日志。' }
[pscustomobject]@{ installed=$true; registered=$true; taskRegistered=$true; running=$true; backup=$backup; repairShortcut=$repairShortcut; log=(Join-Path $homeDir 'Logs\SeamlessAgent.log') } | ConvertTo-Json -Compress
