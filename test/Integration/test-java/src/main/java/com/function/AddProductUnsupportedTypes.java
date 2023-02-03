/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

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
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonMappingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.function.Common.ProductUnsupportedTypes;

import java.util.Optional;


public class AddProductUnsupportedTypes {
    // This output binding should throw an exception because the target table has a column of type
    // TEXT, which is not supported.
    @FunctionName("AddProductUnsupportedTypes")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-unsupportedtypes")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "dbo.ProductsUnsupportedTypes",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductUnsupportedTypes> product) throws JsonMappingException, JsonProcessingException {

        String json = request.getBody().get();
        ObjectMapper mapper = new ObjectMapper();
        ProductUnsupportedTypes p = mapper.readValue(json, ProductUnsupportedTypes.class);
        product.setValue(p);
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
