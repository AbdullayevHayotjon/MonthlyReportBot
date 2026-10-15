$ErrorActionPreference='Stop'
$secret=Read-Host "BotFather tokenini kiriting (ekranda ko‘rinmaydi)" -AsSecureString
$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try{
    $plain=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    if($plain -notmatch '^\d+:[A-Za-z0-9_-]{30,}$'){throw "Token shakli noto‘g‘ri."}
    @{Bot=@{Token=$plain}} | ConvertTo-Json -Compress | dotnet user-secrets set --project (Join-Path $PSScriptRoot 'src\MonthlyReportBot\MonthlyReportBot.csproj')
    if($LASTEXITCODE -ne 0){throw 'Token saqlanmadi.'}
    Write-Host 'Token mahalliy user-secrets ichida saqlandi. Git va ZIP ichiga kirmaydi.'
}finally{
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $plain=$null
}
