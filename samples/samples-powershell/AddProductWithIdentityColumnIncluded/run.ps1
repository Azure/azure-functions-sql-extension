using namespace System.Net

# Trigger binding data passed in via param block
param($Request)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# Update req_query with the body of the request
$req_query = @{
    productId= if($Request.QUERY.productId) { $Request.QUERY.productId } else { $null };
    name=$Request.QUERY.name;
    cost=$Request.QUERY.cost;
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