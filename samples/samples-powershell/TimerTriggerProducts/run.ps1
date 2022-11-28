using namespace System.Net

param($myTimer, $TriggerMetadata)
$executionNumber = 0;
$totalUpserts = 1000;

# Write to the Azure Functions log stream.
$start = Get-Date
Write-Host "[QueueTrigger]: $start starting execution $executionNumber. Rows to generate=$totalUpserts."

$products = @()
for ($i = 0; $i -lt $totalUpserts; $i++) {
    $products += [PSCustomObject]@{
        productId = $i;
        name = "test";
        cost = 100 * $i;
    }
}
$end = Get-Date
$duration = New-TimeSpan -Start $start -End $end

# Assign the value we want to pass to the SQL Output binding. 
# The -Name value corresponds to the name property in the function.json for the binding
Push-OutputBinding -Name products -Value $products

Write-Host "[QueueTrigger]: $end finished execution $queueMessage. Total time to create $totalUpserts rows=$duration."
$executionNumber += 1;