package com.function.Common;

public class ProductWithoutId {
    private String Name;
    private int Cost;

    public ProductWithoutId(String name, int cost) {
        Name = name;
        Cost = cost;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }
}
