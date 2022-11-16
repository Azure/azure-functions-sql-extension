package com.function;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.function.Common.ProductColumnTypes;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.HttpResponseMessage;
import com.microsoft.azure.functions.HttpStatus;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLInput;

import java.text.SimpleDateFormat;
import java.util.Optional;
import java.util.logging.Level;

public class GetProductsColumnTypesSerialization {
    @FunctionName("GetProductsColumnTypesSerialization")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-columntypesserialization")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "SELECT * FROM [dbo].[ProductsColumnTypes]",
                commandType = "Text",
                connectionStringSetting = "SqlConnectionString")
                ProductColumnTypes[] products,
            ExecutionContext context) throws JsonProcessingException {

        ObjectMapper mapper = new ObjectMapper();
        SimpleDateFormat df = new SimpleDateFormat("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'SSSXXX");
        mapper.setDateFormat(df);
        for (ProductColumnTypes product : products) {
            context.getLogger().log(Level.INFO, mapper.writeValueAsString(product));
        }
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(mapper.writeValueAsString(products)).build();
    }
}
