$ErrorActionPreference='Stop'
$rtdRoot=Split-Path $PSScriptRoot
$rtdConfig=Get-Content -Raw (Join-Path $rtdRoot 'rtd.config.json')|ConvertFrom-Json
function RtdPath([string]$value){if([IO.Path]::IsPathRooted($value)){return $value};return Join-Path $rtdRoot $value}
$rtdDotnet=RtdPath $rtdConfig.tools.dotnet
$rtdPython=$rtdConfig.tools.python
if(!$rtdPython){$rtdPython=(Get-Command python.exe -ErrorAction Stop).Source}else{$rtdPython=RtdPath $rtdPython}
& $rtdPython (Join-Path $PSScriptRoot 'test_tools.py')
if($LASTEXITCODE -ne 0){throw 'Tool discovery checks failed'}
& $rtdPython (Join-Path $PSScriptRoot 'test_contract.py')
if($LASTEXITCODE -ne 0){throw 'Translation contract checks failed'}
& $rtdDotnet run --project (Join-Path $rtdRoot 'src/LoaderTests')
if($LASTEXITCODE -ne 0){throw 'Loader checks failed'}
& $rtdDotnet run "-p:RTDCecilPath=$(RtdPath $rtdConfig.tools.cecil)" --project (Join-Path $rtdRoot 'src/LocalizationTests') -- (Join-Path (RtdPath $rtdConfig.paths.build) 'original-managed/Assembly-CSharp.dll') (Join-Path (RtdPath $rtdConfig.paths.build) 'Assembly-CSharp.dll')
if($LASTEXITCODE -ne 0){throw 'IL checks failed'}
