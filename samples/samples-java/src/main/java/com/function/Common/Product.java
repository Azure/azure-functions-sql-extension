/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import com.fasterxml.jackson.annotation.JsonProperty;

public class Product {
    @JsonProperty("ProductId")
    private int ProductId;
    @JsonProperty("Name")
    private String Name;
    @JsonProperty("Cost")
    private int Cost;

    public Product() {
    }

    public Product(int productId, String name, int cost) {
        ProductId = productId;
        Name = name;
        Cost = cost;
    }

    public int getProductId() {
        return ProductId;
    }

    public void setProductId(int productId) {
        this.ProductId = productId;
    }

    public String getName() {
        return Name;
    }

    public void setName(String name) {
        this.Name = name;
    }

    public int getCost() {
        return Cost;
    }

    public void setCost(int cost) {
        this.Cost = cost;
    }
}
