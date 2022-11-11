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
import com.function.Common.MultiplePrimaryKeyProductWithoutId;
import java.util.Optional;

public class AddProductWithMultiplePrimaryColumnsAndIdentity {
    @FunctionName("AddProductWithMultiplePrimaryColumnsAndIdentity")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproductwithmultipleprimarycolumnsandidentity")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "ProductsWithMultiplePrimaryColumnsAndIdentity",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<MultiplePrimaryKeyProductWithoutId> product) {

        MultiplePrimaryKeyProductWithoutId p = new MultiplePrimaryKeyProductWithoutId(
            Integer.parseInt(request.getQueryParameters().get("externalId")),
            request.getQueryParameters().get("name"),
            Integer.parseInt(request.getQueryParameters().get("cost"))
        );
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
