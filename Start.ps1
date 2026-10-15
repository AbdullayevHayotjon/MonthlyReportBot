param([switch]$Check,[switch]$Migrate)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_CONTENTROOT=Join-Path $PSScriptRoot 'publish'
$botDll=Join-Path $PSScriptRoot 'publish\MonthlyReportBot.dll'
if(-not (Test-Path -LiteralPath $botDll)){
    dotnet publish (Join-Path $PSScriptRoot 'src\MonthlyReportBot\MonthlyReportBot.csproj') -c Release -r win-x64 --self-contained false -o (Join-Path $PSScriptRoot 'publish')
    if($LASTEXITCODE -ne 0){throw 'Publish muvaffaqiyatsiz.'}
}
if($Check){dotnet $botDll --check}
elseif($Migrate){dotnet $botDll --migrate}
else{dotnet $botDll}
exit $LASTEXITCODE
