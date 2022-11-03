using namespace System.Net

# Trigger binding data passed in via param block
param($queueMessage)
$totalUpserts = 100;
# Write to the Azure Functions log stream.
Write-Host "[QueueTrigger]: $Get-Date starting execution $queueMessage. Rows to generate=$totalUpserts."

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
# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name products -Value $products
$end = Get-Date
$duration = New-TimeSpan -Start $start -End $end

Write-Host "[QueueTrigger]: $Get-Date finished execution $queueMessage. Total time to create $totalUpserts rows=$duration."