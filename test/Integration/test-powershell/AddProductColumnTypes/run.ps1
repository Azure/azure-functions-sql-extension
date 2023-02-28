# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

# Trigger binding data passed in via param block
param($Request, $TriggerMetadata)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# Note that this expects the body to be a JSON object or array of objects
# which have a property matching each of the columns in the table to upsert to.
$req_query = @{
    ProductId=$Request.QUERY.productId;
    BigInt=999;
    Bit=$true;
    DecimalType=1.2345;
    Money=1.2345;
    Numeric=1.2345;
    SmallInt=1;
    SmallMoney=1.2345;
    TinyInt=1;
    FloatType=0.1;
    Real=0.1;
    Date=Get-Date -AsUTC -Format "yyyy-MM-dd";
    Datetime=Get-Date -AsUTC -Format "yyyy-MM-ddTHH:mm:ss";
    Datetime2=Get-Date -AsUTC -Format "yyyy-MM-ddTHH:mm:ss";
    DatetimeOffset=Get-Date -AsUTC -Format "yyyy-MM-ddTHH:mm:sszzz";
    SmallDatetime=Get-Date -AsUTC -Format "yyyy-MM-ddTHH:mm:ss";
    Time=Get-Date -AsUTC -Format "HH:mm:ss";
    CharType="test";
    Varchar="test";
    Nchar="\u2649";
    Nvarchar="\u2649";
    Binary="dGVzdA==";
    Varbinary="dGVzdA==";
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