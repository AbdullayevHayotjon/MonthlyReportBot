param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
# Stop bot first so SQLite DB and image directory form a consistent snapshot.
$botProcess=Get-CimInstance Win32_Process | Where-Object {$_.CommandLine -like '*MonthlyReportBot.dll*' -or $_.Name -eq 'MonthlyReportBot.exe'}
if($botProcess){throw "Avval botni Ctrl+C bilan yoki Stop-Service MonthlyReportBot bilan to‘xtating."}
$storage=Join-Path $PSScriptRoot 'publish\storage'
if(-not (Test-Path -LiteralPath $storage)){throw 'publish/storage topilmadi.'}
$target=[IO.Path]::GetFullPath($Destination)
Compress-Archive -LiteralPath $storage -DestinationPath $target -Force
Write-Host "Zaxira yaratildi. Bu arxiv shaxsiy ma’lumot saqlaydi, uni maxfiy saqlang."
