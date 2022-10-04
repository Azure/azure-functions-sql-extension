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
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;
import java.util.ArrayList;
import java.util.Optional;

public class AddProductsArray {
    @FunctionName("AddProductsArray")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproducts-array")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "Products",
                connectionStringSetting = "sqlConnectionString") OutputBinding<ArrayList<Product>> products,
            final ExecutionContext context) {

        String json = request.getBody().get();
        Type listType = new TypeToken<ArrayList<Product>>() {}.getType();
        ArrayList<Product> p = new Gson().fromJson(json , listType);
        products.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
