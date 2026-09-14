param([switch]$IncludeTests)

$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw '未找到 Windows .NET Framework C# 编译器。'
}

$speechPath = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter 'System.Speech.dll' -File -Recurse -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $speechPath) {
    throw 'Windows 未安装 System.Speech 本地语音组件。'
}

$targetPath = Join-Path $PSScriptRoot '轻译.exe'
$targetType = '/target:winexe'
$entryPoint = @()
$sourceFiles = @(
    (Join-Path $PSScriptRoot 'Program.cs'),
    (Join-Path $PSScriptRoot 'Core.cs'),
    (Join-Path $PSScriptRoot 'MainForm.cs')
)

if ($IncludeTests) {
    $targetPath = Join-Path $PSScriptRoot '轻译-测试.exe'
    $targetType = '/target:exe'
    $entryPoint = @('/main:QingYi.Tests')
    $sourceFiles += (Join-Path $PSScriptRoot 'Tests.cs')
}

& $compilerPath /nologo $targetType /platform:x64 /optimize+ /codepage:65001 "/out:$targetPath" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Security.dll "/reference:$speechPath" /reference:System.Web.Extensions.dll $entryPoint $sourceFiles
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output $targetPath
