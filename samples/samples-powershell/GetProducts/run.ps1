# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

#  The input binding executes the `select * from Products where Cost = @Cost` query, returning the result as json object in the body.
#  The *parameters* argument passes the `{cost}` specified in the URL that triggers the function,
#  `getproducts/{cost}`, as the value of the `@Cost` parameter in the query.
#  *commandType* is set to `Text`, since the constructor argument of the binding is a raw query.
using namespace System.Net

# Trigger and input binding data are passed in via the param block.
param($Request, $TriggerMetadata, $products)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Input Binding processed a request."

# Assign the value to return as the HTTP response. 
# The -Name value matches the name property in the function.json for the binding
Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [System.Net.HttpStatusCode]::OK
    Body = $products
})