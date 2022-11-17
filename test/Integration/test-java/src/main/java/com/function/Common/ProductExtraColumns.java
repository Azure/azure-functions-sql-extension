/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class ProductExtraColumns {
    private int ProductId;
    private String Name;
    private int Cost;
    private int ExtraInt;
    private String ExtraString;

    public ProductExtraColumns(int productId, String name, int cost, int extraInt, String extraString) {
        ProductId = productId;
        Name = name;
        Cost = cost;
        ExtraInt = extraInt;
        ExtraString = extraString;
    }

    public int getProductId() {
        return ProductId;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }

    public int getExtraInt() {
        return ExtraInt;
    }

    public String getExtraString() {
        return ExtraString;
    }
}
