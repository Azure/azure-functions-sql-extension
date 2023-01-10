/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import com.fasterxml.jackson.annotation.JsonProperty;

public class ProductWithDefaultPK {
    @JsonProperty("Name")
    private String Name;
    @JsonProperty("Cost")
    private int Cost;

    public ProductWithDefaultPK() {
    }

    public ProductWithDefaultPK(String name, int cost) {
        Name = name;
        Cost = cost;
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
