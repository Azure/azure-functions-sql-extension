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
import com.function.Common.ProductWithOptionalId;

import java.util.Optional;

public class AddProductWithIdentityColumnIncluded {
    @FunctionName("AddProductWithIdentityColumnIncluded")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = { HttpMethod.GET },
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductwithidentitycolumnincluded")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "ProductsWithIdentity",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductWithOptionalId> product) {

        ProductWithOptionalId p = new ProductWithOptionalId(
            request.getQueryParameters().get("productId") == null ? null : Integer.parseInt(request.getQueryParameters().get("productId")),
            request.getQueryParameters().get("name"),
            Integer.parseInt(request.getQueryParameters().get("cost"))
        );
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
