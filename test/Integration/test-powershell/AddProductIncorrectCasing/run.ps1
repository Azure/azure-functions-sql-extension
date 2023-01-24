# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

# This output binding should throw an error since the casing of the POCO field 'ProductID'
# and table column name 'ProductId' do not match.
param($Request, $TriggerMetadata)

$req_query = @{
    ProductID=$Request.QUERY.productId;
    Datetime=Get-Date -AsUTC;
    Datetime2=Get-Date -AsUTC;
};

Push-OutputBinding -Name product -Value $req_query

Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $req_query
})