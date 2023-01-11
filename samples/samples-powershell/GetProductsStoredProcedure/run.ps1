# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

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