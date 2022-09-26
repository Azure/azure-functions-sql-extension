package com.function;

import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.OutputBinding;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.QueueTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLOutput;
import com.function.Common.Product;

public class QueueTriggerProducts {
    @FunctionName("QueueTriggerProducts")
    public void run(
            @QueueTrigger(
                name = "msg",
                queueName = "testqueue",
                connection = "AzureWebJobsStorage"
            ) String queueMessage,
            @SQLOutput(
                commandText = "Products",
                connectionStringSetting = "sqlConnectionString") OutputBinding<Product[]> products,
            final ExecutionContext context) {

        int totalUpserts = 100;
        Product[] p = new Product[totalUpserts];
        for (int i = 0; i < totalUpserts; i++) {
            p[i] = new Product(i, "test", 100);
        }

        products.setValue(p);
    }
}
