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
import com.function.Common.ProductWithDefaultPK;
import com.google.gson.Gson;

import java.io.IOException;
import java.util.Optional;

public class AddProductWithDefaultPK {
    @FunctionName("AddProductWithDefaultPK")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductwithdefaultpk")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "dbo.ProductsWithDefaultPK",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductWithDefaultPK> product) throws IOException {

        String json = request.getBody().get();
        Gson gson = new Gson();
        ProductWithDefaultPK p = gson.fromJson(json, ProductWithDefaultPK.class);
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
