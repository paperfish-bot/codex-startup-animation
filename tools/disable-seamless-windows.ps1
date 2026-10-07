#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$homeDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenAI\ChatGPTFix'
$animation = Join-Path $homeDir 'Animation'
$exe = Join-Path $animation 'SeamlessAgent.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'CodexSeamlessAnimation'
$expectedCommand = '"' + $exe + '" "' + $animation + '"'

$runValue = (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue).$runName
if ($runValue -eq $expectedCommand) {
    Remove-ItemProperty -Path $runKey -Name $runName
}

$task = Get-ScheduledTask -TaskName $runName -ErrorAction SilentlyContinue | Select-Object -First 1
if ($task -and $task.Actions.Execute -ieq $exe -and
    $task.Actions.Arguments -eq ('"' + $animation + '"')) {
    Unregister-ScheduledTask -TaskName $runName -Confirm:$false
}

foreach ($process in @(Get-Process -Name SeamlessAgent -ErrorAction SilentlyContinue)) {
    if ($process.Path -ieq $exe) { Stop-Process -Id $process.Id }
}

Write-Host '启动动画监听已停用。安装文件和备份仍保留；重新运行安装脚本即可启用。'
