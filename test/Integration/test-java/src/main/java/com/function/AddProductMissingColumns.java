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
import com.function.Common.ProductMissingColumns;

import java.util.Optional;

public class AddProductMissingColumns {
    @FunctionName("AddProductMissingColumns")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-missingcolumns")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "dbo.Products",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductMissingColumns> product) {

        ProductMissingColumns p = new ProductMissingColumns(0, "test");
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
