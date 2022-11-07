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
