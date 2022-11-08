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
import com.fasterxml.jackson.core.JsonParseException;
import com.fasterxml.jackson.databind.JsonMappingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.function.Common.Product;

import java.io.IOException;
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
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product[]> products) throws JsonParseException, JsonMappingException, IOException {

        String json = request.getBody().get();
        ObjectMapper mapper = new ObjectMapper();
        Product[] p = mapper.readValue(json, Product[].class);
        products.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
