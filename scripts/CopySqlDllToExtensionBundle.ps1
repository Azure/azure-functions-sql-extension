# Run this script from the root of the repository to copy the latest
# SQL extension dll to the extension bundle.

# Run `func GetExtensionBundlePath` from samples-js to get the correct path to the extension bundle.
cd ./samples/samples-js
$extensionBundlePath = func GetExtensionBundlePath
$extensionBundleBinPath = Join-Path -Path $extensionBundlePath -ChildPath "bin"
cd ../..
$sqlDllPath = "./src/bin/Debug/netstandard2.0/Microsoft.Azure.WebJobs.Extensions.Sql.dll"
Copy-Item -Path $sqlDllPath -Destination $extensionBundleBinPath
Write-Host "Copied $sqlDllPath to $extensionBundleBinPath"