package com.function;

import com.function.Common.ProductName;
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

public class GetProductNamesView {
    @FunctionName("GetProductNamesView")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproduct-namesview")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                commandText = "SELECT * FROM ProductNames",
                commandType = "Text",
                connectionStringSetting = "sqlConnectionString") ProductName[] products,
            final ExecutionContext context) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
