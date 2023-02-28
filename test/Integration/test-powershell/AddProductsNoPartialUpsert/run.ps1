# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

# Trigger binding data passed in via param block
param($Request, $TriggerMetadata)
$totalUpserts = 1000;

$products = @()
for($i = 0; $i -lt $totalUpserts; $i++) {
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