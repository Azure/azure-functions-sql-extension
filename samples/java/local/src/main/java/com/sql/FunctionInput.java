
package com.sql;

import java.util.*;
import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import com.microsoft.azure.functions.sql.annotation.*;
import com.microsoft.azure.functions.sql.*;


import java.util.Optional;

public class FunctionInput {
    @FunctionName("SqlInput-Java")
    public HttpResponseMessage input(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @SQLInput(sqlQuery = "select * from Products where cost = 100",
            authentication = "User ID=sophiatev;Password=sherbet-reads25L",
            connectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")  Product input,
            final ExecutionContext context) {
        context.getLogger().info("Java HTTP trigger processed a request.");

        return request.createResponseBuilder(HttpStatus.OK).body(input).build();
    }

    public static class Product
    {
        private int productID;
        private String name;
        private int cost;
        
        public int getProductID() { return this.productID; }
        public String getName() { return this.name; }
        public int getCost() { return this.cost; }
    }
}