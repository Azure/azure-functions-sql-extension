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
import com.function.Common.Product;
import com.google.gson.Gson;

import java.io.IOException;
import java.util.Optional;

/**
 * This function uses two SQL output bindings to upsert the product in the request body to the
 * Products and ProductsWithIdentity tables.
 */
public class AddProductToTwoTables {
    @FunctionName("AddProductToTwoTables")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproducttotwotables")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "Products",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product> product,
            @SQLOutput(
                name = "productWithIdentity",
                commandText = "ProductsWithIdentity",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product> productWithIdentity) throws IOException {

        String json = request.getBody().get();
        Gson gson = new Gson();
        Product p = gson.fromJson(json, Product.class);
        product.setValue(p);
        productWithIdentity.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
