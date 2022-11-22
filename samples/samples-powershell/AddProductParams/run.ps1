using namespace System.Net

# Trigger binding data passed in via param block
param($Request, $TriggerMetadata)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# Update req_query with the query of the request
# Currently use a workaround through the TriggerMetadata for PowerShell Azure Functions
# Issue link: https://github.com/Azure/azure-functions-powershell-worker/issues/895
$req_query = @{ 
    "productId"= $TriggerMetadata["productId"];
    "name"= $TriggerMetadata["name"];
    "cost"= $TriggerMetadata["cost"];
};

# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name product -Value $req_query

# Assign the value to return as the HTTP response. 
# The -Name value matches the name property in the function.json for the binding
Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $req_query
})