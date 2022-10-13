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
import com.function.Common.ProductExtraColumns;

import java.util.Optional;

public class AddProductExtraColumns {
    @FunctionName("AddProductExtraColumns")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-extracolumns")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "dbo.Products",
                connectionStringSetting = "sqlConnectionString")
                OutputBinding<ProductExtraColumns> product) {

        ProductExtraColumns p = new ProductExtraColumns(0, "test", 0, 0, "test");
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
