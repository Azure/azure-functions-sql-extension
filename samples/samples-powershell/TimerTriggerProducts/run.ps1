using namespace System.Net

# Trigger binding data passed in via param block
$executionNumber = 0;
param($myTimer)
$totalUpserts = 100;
# Write to the Azure Functions log stream.
Write-Host "[QueueTrigger]: $Get-Date starting execution $executionNumber. Rows to generate=$totalUpserts."

# Update req_body with the body of the request
# Note that this expects the body to be a JSON object or array of objects 
# which have a property matching each of the columns in the table to upsert to.
$start = Get-Date

$products = @()
for ($i = 0; $i -lt $totalUpserts; $i++) {
    $products += [PSCustomObject]@{
        productId = $i;
        name = "test";
        cost = 100 * $i;
    }
}
$duration = Get-Date - $start;

# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name products -Value $products

Write-Host "[QueueTrigger]: $Get-Date finished execution $queueMessage. Total time to create $totalUpserts rows=$duration."

$executionNumber += 1;