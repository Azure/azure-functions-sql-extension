/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.function.Common.ProductColumnTypes;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.HttpMethod;
import com.microsoft.azure.functions.HttpRequestMessage;
import com.microsoft.azure.functions.HttpResponseMessage;
import com.microsoft.azure.functions.HttpStatus;
import com.microsoft.azure.functions.annotation.AuthorizationLevel;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.HttpTrigger;
import com.microsoft.azure.functions.sql.annotation.CommandType;
import com.microsoft.azure.functions.sql.annotation.SQLInput;

import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.Optional;
import java.util.logging.Level;
import java.util.Calendar;
import java.sql.Timestamp;

public class GetProductsColumnTypesSerialization {
    @FunctionName("GetProductsColumnTypesSerialization")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-columntypesserialization")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "SELECT * FROM [dbo].[ProductsColumnTypes]",
                commandType = CommandType.Text,
                connectionStringSetting = "SqlConnectionString")
                ProductColumnTypes[] products,
            ExecutionContext context) throws ParseException {

        Gson gson = new GsonBuilder().setDateFormat("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'SSSXXX").create();
        SimpleDateFormat df = new SimpleDateFormat("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'SSSXXX");
        for (ProductColumnTypes product : products) {
            // Convert the datetimes to UTC (Java worker returns the datetimes in local timezone)
            long date = df.parse(product.getDate()).getTime();
            long datetime = df.parse(product.getDatetime()).getTime();
            long datetime2 = df.parse(product.getDatetime2()).getTime();
            long smallDateTime = df.parse(product.getSmallDatetime()).getTime();
            int offset = Calendar.getInstance().getTimeZone().getOffset(df.parse(product.getDatetime()).getTime());
            product.setDate(new Timestamp(date - offset).toString());
            product.setDatetime(new Timestamp(datetime - offset).toString());
            product.setDatetime2(new Timestamp(datetime2 - offset).toString());
            product.setSmallDatetime(new Timestamp(smallDateTime - offset).toString());
            context.getLogger().log(Level.INFO, gson.toJson(product));
        }
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(gson.toJson(products)).build();
    }
}
