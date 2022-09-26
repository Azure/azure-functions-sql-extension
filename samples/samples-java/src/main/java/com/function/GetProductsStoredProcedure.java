package com.function;

import com.function.Common.Product;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.HttpResponseMessage;
import com.microsoft.azure.functions.HttpStatus;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLInput;

import java.util.Optional;

public class GetProductsStoredProcedure {
    @FunctionName("GetProductsStoredProcedure")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-storedprocedure/{cost}")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                commandText = "SelectProductsCost",
                commandType = "StoredProcedure",
                parameters = "@Cost={cost}",
                connectionStringSetting = "sqlConnectionString") Product[] products,
            final ExecutionContext context) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
