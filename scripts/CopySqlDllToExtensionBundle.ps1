# Run this script from the root of the repository to copy the latest
# SQL extension dll to the extension bundle. This is required to test
# the latest extension changes when running samples/test.
#
# To revert this run `func GetExtensionBundlePath` manually from the
# `samples-js` folder and delete the folder it returns. The next time a
# function referencing the bundle is launched the host will download 
# a clean copy of the bundle to use.

# Run `func GetExtensionBundlePath` from samples-js to get the correct path to the extension bundle. 
# This assumes that the same bundle will be used for all non-.NET sample functions which should 
# generally be the case, if not run the command above from the same folder of the function you're
# testing with.
cd ./samples/samples-js
$extensionBundlePath = func GetExtensionBundlePath
$extensionBundleBinPath = Join-Path -Path $extensionBundlePath -ChildPath "bin"
cd ../..
$sqlDllPath = "./src/bin/Debug/netstandard2.0/Microsoft.Azure.WebJobs.Extensions.Sql.dll"
Copy-Item -Path $sqlDllPath -Destination $extensionBundleBinPath
Write-Host "Copied $sqlDllPath to $extensionBundleBinPath"
