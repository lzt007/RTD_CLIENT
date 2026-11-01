param([ValidateSet('ja','zh-TW','zh-CN')][string]$Language='ja')
$ErrorActionPreference='Stop'
$rtdConfig = Get-Content -Raw (Join-Path $PSScriptRoot 'rtd.config.json') | ConvertFrom-Json
$rtdPython = $rtdConfig.tools.python
if (!$rtdPython) { $rtdPython = (Get-Command python.exe -ErrorAction Stop).Source }
elseif (![IO.Path]::IsPathRooted($rtdPython)) { $rtdPython = Join-Path $PSScriptRoot $rtdPython }
& $rtdPython (Join-Path $PSScriptRoot 'scripts/build.py') --language $Language
if($LASTEXITCODE -ne 0){throw 'Client build failed'}
