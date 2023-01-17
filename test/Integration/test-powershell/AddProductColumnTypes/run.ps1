using namespace System.Net

# Trigger binding data passed in via param block
param($Request, $TriggerMetadata)

# Write to the Azure Functions log stream.
Write-Host "PowerShell function with SQL Output Binding processed a request."

# Update req_body with the body of the request
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
    Date=Get-Date -AsUTC;
    Datetime=Get-Date -AsUTC;
    Datetime2=Get-Date -AsUTC;
    DatetimeOffSet=Get-Date -AsUTC;
    SmallDatetime=Get-Date -AsUTC;
    Time=Get-Date -AsUTC;
    CharType="test";
    Varchar="test";
    Nchar="test";
    Nvarchar="test;
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