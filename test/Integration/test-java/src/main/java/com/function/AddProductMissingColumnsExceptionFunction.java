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


public class AddProductMissingColumnsExceptionFunction {
    // This output binding should throw an error since the ProductsCostNotNull table does not
    // allows rows without a Cost value.
    @FunctionName("AddProductMissingColumnsExceptionFunction")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct-missingcolumnsexception")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "dbo.ProductsCostNotNull",
                connectionStringSetting = "sqlConnectionString")
                OutputBinding<ProductMissingColumns> product) {

        ProductMissingColumns p = new ProductMissingColumns(0, "test");
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
