package com.function;

import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.HttpResponseMessage;
import com.microsoft.azure.functions.HttpStatus;
import com.microsoft.azure.functions.OutputBinding;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLOutput;
import com.function.Common.Product;

import java.util.Optional;

public class AddProductsNoPartialUpsert {
    @FunctionName("AddProductsNoPartialUpsert")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-nopartialupsert")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "products",
                commandText = "dbo.ProductsNameNotNull",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product[]> products) {

        Product validProduct = new Product(0, "test", 100);
        Product invalidProduct = new Product(1, null, 100);
        Product[] p = new Product[]{validProduct, invalidProduct};
        products.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
