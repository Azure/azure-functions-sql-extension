using namespace System.Net

# This function uses a SQL input binding to get products from the Products table
# and upsert those products to the ProductsWithIdentity table.

param($Request, $TriggerMetadata, $products)

Push-OutputBinding -Name productsWithIdentity -Value $products

Push-OutputBinding -Name response -Value ([HttpResponseContext]@{
    StatusCode = [System.Net.HttpStatusCode]::OK
    Body = $products
})