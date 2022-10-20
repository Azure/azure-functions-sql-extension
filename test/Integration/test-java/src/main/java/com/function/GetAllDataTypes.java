package com.function;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.function.Common.AllDataTypes;
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
import java.util.logging.Level;

public class GetAllDataTypes {
    @FunctionName("GetAllDataTypes")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getalldatatypes")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                commandText = "SELECT * FROM [dbo].[AllDataTypes]",
                commandType = "Text",
                connectionStringSetting = "SqlConnectionString")
                AllDataTypes[] rows,
            ExecutionContext context) throws JsonProcessingException {

        ObjectMapper mapper = new ObjectMapper();
        for (AllDataTypes row : rows) {
            context.getLogger().log(Level.INFO, mapper.writeValueAsString(row));
        }
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(rows).build();
    }
}
