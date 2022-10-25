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
import java.sql.Date;
import java.sql.Time;
import java.sql.Timestamp;
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
                commandText = "dbo.ProductsColumnTypes",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<ProductColumnTypes> product) {

        ProductColumnTypes p = new ProductColumnTypes(0, Long.MAX_VALUE, false, new BigDecimal(1.2345),
            new BigDecimal(1.2345), new BigDecimal(1.2345), (short)0, new BigDecimal(1.2345), (short)0, 1.2345,
            1.2345f, new Date(System.currentTimeMillis()), new Timestamp(System.currentTimeMillis()),
            new Timestamp(System.currentTimeMillis()), new Date(System.currentTimeMillis()),
            new Timestamp(System.currentTimeMillis()), new Time(System.currentTimeMillis()), "test", "test",
            "test", "test", "test");//, new Byte[]{1,2,3,4,5}, new Byte[]{1,2,3,4,5});
        product.setValue(p);

        // Items were inserted successfully so return success, an exception would be thrown if there
        // was any issues
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
}
