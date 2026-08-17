param(
	[string]$Version,
	[string]$VsixPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

$account = az account show --query user.name -o tsv
if ($account -ne 'fgilde@gmail.com') {
	throw "az is logged in as '$account' - run: az login (fgilde@gmail.com)"
}

if (-not $VsixPath) {
	$manifest = Join-Path $repoRoot 'VisualStudio/CheckoutAndBuild.VisualStudio/source.extension.vsixmanifest'
	$original = Get-Content $manifest -Raw
	if ($Version) {
		($original -replace '(<Identity[^>]*Version=")[^"]+', "`${1}$Version") | Set-Content $manifest -NoNewline
	}
	try {
		$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
		& $msbuild "$repoRoot/VisualStudio/CheckoutAndBuild.VisualStudio/CheckoutAndBuild.VisualStudio.csproj" /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false /v:m /nologo
		if ($LASTEXITCODE -ne 0) { throw "build failed" }
	}
	finally {
		if ($Version) { $original | Set-Content $manifest -NoNewline }
	}
	$VsixPath = Join-Path $repoRoot 'VisualStudio/CheckoutAndBuild.VisualStudio/bin/Release/CheckoutAndBuild.VisualStudio.vsix'
}

$token = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
$installation = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
$publisher = Join-Path $installation 'VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe'

& $publisher publish `
	-payload $VsixPath `
	-publishManifest (Join-Path $repoRoot 'publishManifest.json') `
	-personalAccessToken $token `
	-ignoreWarnings 'VSIXValidatorWarning01,VSIXValidatorWarning02'
if ($LASTEXITCODE -ne 0) { throw "VsixPublisher failed with $LASTEXITCODE" }
Write-Host "Published $VsixPath to the marketplace (publisher fgilde)."
