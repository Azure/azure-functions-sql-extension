using namespace System.Net

# Trigger binding data passed in via param block
param($Request)
$totalUpserts = 100;
# Write to the Azure Functions log stream.
Write-Host "[QueueTrigger]: $Get-Date starting execution $queueMessage. Rows to generate=$totalUpserts."

# Update req_body with the body of the request
# Note that this expects the body to be a JSON object or array of objects 
# which have a property matching each of the columns in the table to upsert to.
$start = Get-Date

$products = @()
for ($i = 0; $i -lt 1000; $i++) {
    $products += [PSCustomObject]@{
        productId = $i;
        name = "test";
        cost = 100 * $i;
    }
}

$invalidProduct = @{ 
    productId=1000;
    name=$null;
    cost=1000;
};

$products += $invalidProduct

# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name products -Value $products

# Assign the value to return as the HTTP response. 
# The -Name value matches the name property in the function.json for the binding
Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $products
})