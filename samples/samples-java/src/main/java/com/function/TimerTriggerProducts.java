/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.microsoft.azure.functions.OutputBinding;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.annotation.TimerTrigger;
import com.microsoft.azure.functions.sql.annotation.SQLOutput;
import com.function.Common.Product;

public class TimerTriggerProducts {
    @FunctionName("TimerTriggerProducts")
    public void run(
            @TimerTrigger(
                name = "keepAliveTrigger",
                schedule = "*/5 * * * * *")
                String timerInfo,
            @SQLOutput(
                name = "products",
                commandText = "Products",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product[]> products) {

        int totalUpserts = 1000;
        Product[] p = new Product[totalUpserts];
        for (int i = 0; i < totalUpserts; i++) {
            p[i] = new Product(i, "test", 100);
        }

        products.setValue(p);
    }
}
