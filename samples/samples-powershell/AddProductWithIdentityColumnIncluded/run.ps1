# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

# Trigger binding data passed in via param block
param($Request, $TriggerMetadata)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# KOutput bindings require the [ordered] attribute. See https://github.com/Azure/azure-functions-sql-extension#output-bindings for more details.
$req_query = [ordered]@{
    ProductId= if($Request.QUERY.productId) { [int]$Request.QUERY.productId } else { $null };
    Name=$Request.QUERY.name;
    Cost=[int]$Request.QUERY.cost;
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