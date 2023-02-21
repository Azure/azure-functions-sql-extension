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
import com.function.Common.ProductColumnTypes;

import java.math.BigDecimal;
import java.sql.Timestamp;
import java.sql.Time;
import java.util.Optional;

public class AddProductColumnTypes {
    @FunctionName("AddProductColumnTypes")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-columntypes")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "dbo.ProductsColumnTypes",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductColumnTypes> product) {

        ProductColumnTypes p = new ProductColumnTypes(
            Integer.parseInt(request.getQueryParameters().get("productId")),
            (long)999,
            true,
            new BigDecimal("1.2345"),
            new BigDecimal("1.2345"),
            new BigDecimal("1.2345"),
            (short)1,
            new BigDecimal("1.2345"),
            (short)1,
            0.1,
            0.1,
            new Timestamp(System.currentTimeMillis()).toString(),
            new Timestamp(System.currentTimeMillis()).toString(),
            new Timestamp(System.currentTimeMillis()).toString(),
            new Timestamp(System.currentTimeMillis()).toString(),
            new Timestamp(System.currentTimeMillis()).toString(),
            new Time(System.currentTimeMillis()).toString(),
            "test",
            "test",
            "\u2649",
            "\u2649",
            "dGVzdA==",
            "dGVzdA==");
        product.setValue(p);

        // Items were inserted successfully so return success, an exception would be thrown if there
        // was any issues
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
