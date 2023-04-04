/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

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

/**
 * This function uses two SQL input bindings to get products from the Products and ProductsWithIdentity tables
 * and returns the combined results as a JSON array in the body of the HttpResponseMessage.
 */
public class GetProductsFromTwoTables {
    @FunctionName("GetProductsFromTwoTables")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproductsfromtwotables/{cost}")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "SELECT * FROM Products WHERE Cost = @Cost",
                parameters = "@Cost={cost}",
                connectionStringSetting = "SqlConnectionString")
                Product[] products,
            @SQLInput(
                name = "productsWithIdentity",
                commandText = "SELECT * FROM ProductsWithIdentity WHERE Cost = @Cost",
                parameters = "@Cost={cost}",
                connectionStringSetting = "SqlConnectionString")
                Product[] productsWithIdentity) {

        Product[] allProducts = new Product[products.length + productsWithIdentity.length];
        System.arraycopy(products, 0, allProducts, 0, products.length);
        System.arraycopy(productsWithIdentity, 0, allProducts, products.length, productsWithIdentity.length);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(allProducts).build();
    }
}
