# Run this script from the root of the repository to copy the latest
# SQL extension dll to the extension bundle. This is required to test
# the latest extension changes when running samples/test.
#
# To revert this run `func bundles download --force` manually from the
# `samples-js` folder to download a clean copy of the bundle.

# Run `func bundles path` from samples-js to get the correct path to the extension bundle.
# This assumes that the same bundle will be used for all non-.NET sample functions which should 
# generally be the case, if not run the command above from the same folder of the function you're
# testing with.
$ErrorActionPreference = "Stop"
Push-Location .\samples\samples-js
try {
    func bundles download
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to download the Azure Functions extension bundle."
    }

    $bundlePathOutput = func bundles path
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to resolve the Azure Functions extension bundle path."
    }

    $extensionBundlePath = $bundlePathOutput | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($extensionBundlePath)) {
        throw "Azure Functions Core Tools returned an empty extension bundle path."
    }

    $extensionBundleBinPath = Join-Path -Path $extensionBundlePath -ChildPath "bin"
    if (-not (Test-Path -LiteralPath $extensionBundleBinPath -PathType Container)) {
        throw "Extension bundle bin directory not found at '$extensionBundleBinPath'."
    }
}
finally {
    Pop-Location
}

$sqlDllPath = "./src/bin/Debug/netstandard2.0/Microsoft.Azure.WebJobs.Extensions.Sql.dll"
Copy-Item -Path $sqlDllPath -Destination $extensionBundleBinPath
Write-Host "Copied $sqlDllPath to $extensionBundleBinPath"
