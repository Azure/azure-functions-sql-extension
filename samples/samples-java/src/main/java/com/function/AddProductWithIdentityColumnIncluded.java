package com.function;

import com.microsoft.azure.functions.ExecutionContext;
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

public class AddProductWithIdentityColumnIncluded {
    @FunctionName("AddProductWithIdentityColumnIncluded")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductwithidentitycolumnincluded")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "ProductsWithIdentity",
                connectionStringSetting = "sqlConnectionString") OutputBinding<Product> product,
            final ExecutionContext context) {

        Product p = new Product(
            request.getQueryParameters().get("productId") == null ? null : Integer.parseInt(request.getQueryParameters().get("productId")),
            request.getQueryParameters().get("name"),
            Integer.parseInt(request.getQueryParameters().get("cost"))
        );
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
