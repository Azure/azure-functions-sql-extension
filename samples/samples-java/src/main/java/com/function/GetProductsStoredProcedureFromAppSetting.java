package com.function;

import com.function.Common.Product;
import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.HttpResponseMessage;
import com.microsoft.azure.functions.HttpStatus;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLInput;

import java.util.Optional;

public class GetProductsStoredProcedureFromAppSetting {
    @FunctionName("GetProductsStoredProcedureFromAppSetting")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproductsbycost")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                commandText = "%Sp_SelectCost%",
                commandType = "StoredProcedure",
                parameters = "@Cost=%ProductCost%",
                connectionStringSetting = "sqlConnectionString")
                Product[] products) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}