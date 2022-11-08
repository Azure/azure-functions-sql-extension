/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class ProductWithOptionalId {
    private Integer ProductId;
    private String Name;
    private int Cost;

    public ProductWithOptionalId(Integer productId, String name, int cost) {
        ProductId = productId;
        Name = name;
        Cost = cost;
    }

    public Integer getProductId() {
        return ProductId;
    }

    public void setProductId(Integer productId) {
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
