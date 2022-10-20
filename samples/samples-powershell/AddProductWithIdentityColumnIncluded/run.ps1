using namespace System.Net

# Trigger binding data passed in via param block
param($Request)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# Update req_body with the body of the request
# Note that this expects the body to be a JSON object or array of objects 
# which have a property matching each of the columns in the table to upsert to.
$req_body = @{
    productId=$Request.Body?.productId;
    name=$Request.Body?.name;
    cost=$Request.Body?.cost;
};

# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name product -Value $req_body

# Assign the value to return as the HTTP response. 
# The -Name value matches the name property in the function.json for the binding
Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $req_body
})