  
# Install Sql Extension for the target

$FunctionAppName = "sql-function-20190419163130420"
$ExtensionVersion = "3.0.0"

pushd . 
cd target\azure-functions\${FunctionAppName}
# If you want to install extension, put the nuget package on this directory and uncomment this line and comment out the second one.
func extensions install --package Microsoft.Azure.WebJobs.Extensions.SQL --version ${ExtensionVersion} --source ..\..\.. --java
# func extensions install --package Microsoft.Azure.WebJobs.Extensions.SQL --version ${ExtensionVersion}
popd 