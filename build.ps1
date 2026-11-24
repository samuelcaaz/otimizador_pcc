$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outExe = Join-Path $dir "OtimizadorPC.exe"

Write-Host "Compilando OtimizadorPC.exe (64-bit, WinForms, UAC requireAdministrator)..." -ForegroundColor Cyan

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
    /win32manifest:"$dir\app.manifest" `
    /out:"$outExe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Management.dll `
    "$dir\Engine.cs" "$dir\MainForm.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Falha na compilacao (ExitCode $LASTEXITCODE)"
}

$fi = Get-Item $outExe
Write-Host ("SUCESSO! Gerado: {0} ({1:N1} KB)" -f $fi.FullName, ($fi.Length / 1KB)) -ForegroundColor Green
