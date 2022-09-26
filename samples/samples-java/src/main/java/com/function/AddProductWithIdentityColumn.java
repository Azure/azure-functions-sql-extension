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
import com.function.Common.ProductWithoutId;
import java.util.Optional;

public class AddProductWithIdentityColumn {
    @FunctionName("AddProductWithIdentityColumn")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductwithidentitycolumn")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "ProductsWithIdentity",
                connectionStringSetting = "sqlConnectionString") OutputBinding<ProductWithoutId> product,
            final ExecutionContext context) {

        ProductWithoutId p = new ProductWithoutId(
            request.getQueryParameters().get("name"),
            Integer.parseInt(request.getQueryParameters().get("cost"))
        );
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
