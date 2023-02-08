# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

# This output binding should throw an exception because the target table has unsupported column types.
param($Request)

$req_body = [ordered]@{
    ProductId=0;
    TextCol="test";
    NtextCol="test";
    ImageCol="dGVzdA==";
}

Push-OutputBinding -Name product -Value $req_body

Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $req_body
})