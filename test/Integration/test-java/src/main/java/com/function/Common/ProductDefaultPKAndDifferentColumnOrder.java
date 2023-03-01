/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class ProductDefaultPKAndDifferentColumnOrder {
    private int Cost;
    private String Name;

    public ProductDefaultPKAndDifferentColumnOrder(int cost,String name) {
        Cost = cost;
        Name = name;
    }

    public int getCost() {
        return Cost;
    }

    public String getName() {
        return Name;
    }

}