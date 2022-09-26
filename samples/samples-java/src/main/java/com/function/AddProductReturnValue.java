package com.function;

import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLOutput;
import com.function.Common.Product;
import com.google.gson.Gson;
import java.util.Optional;

public class AddProductReturnValue {
    @FunctionName("AddProductReturnValue")
    @SQLOutput(commandText = "Products",
        connectionStringSetting = "sqlConnectionString")
    public Product run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-returnvalue")
                HttpRequestMessage<Optional<String>> request,
                final ExecutionContext context) {

        String json = request.getBody().get();
        Gson gson = new Gson();
        Product product = gson.fromJson(json, Product.class);
        return product;
    }
}
