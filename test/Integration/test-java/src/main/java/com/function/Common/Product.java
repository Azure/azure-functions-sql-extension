package com.function.Common;

public class Product {
    private int ProductID;
    private String Name;
    private int Cost;

    public Product(int productId, String name, int cost) {
        ProductID = productId;
        Name = name;
        Cost = cost;
    }

    public int getProductId() {
        return ProductID;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }
}
