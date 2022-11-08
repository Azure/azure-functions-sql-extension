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
import com.function.Common.ProductWithoutId;

import java.util.Optional;

public class AddProductsWithIdentityColumnArray {
    @FunctionName("AddProductsWithIdentityColumnArray")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = { HttpMethod.GET },
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductswithidentitycolumnarray") HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "dbo.ProductsWithIdentity",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductWithoutId[]> products) {

        ProductWithoutId[] p = new ProductWithoutId[] {
            new ProductWithoutId("Cup", 2),
            new ProductWithoutId("Glasses", 12)
        };
        products.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
}
