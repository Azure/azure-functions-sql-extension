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
 import com.function.Common.ProductIncorrectCasing;

 import java.util.Optional;

 // This output binding should throw an error since the casing of the POCO field 'ProductID' and
 // table column name 'ProductId' do not match.
 public class AddProductIncorrectCasing {
     @FunctionName("AddProductIncorrectCasing")
     public HttpResponseMessage run(
             @HttpTrigger(
                 name = "req",
                 methods = {HttpMethod.GET},
                 authLevel = AuthorizationLevel.ANONYMOUS,
                 route = "addproduct-incorrectcasing")
                 HttpRequestMessage<Optional<String>> request,
             @SQLOutput(
                 name = "product",
                 commandText = "dbo.Products",
                 connectionStringSetting = "SqlConnectionString")
                 OutputBinding<ProductIncorrectCasing> product) {

         ProductIncorrectCasing p = new ProductIncorrectCasing(
             0,
             "test",
             100);
         product.setValue(p);

         return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
     }
 }