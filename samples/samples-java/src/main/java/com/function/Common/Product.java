package com.function.Common;

public class Product {
    private int ProductId;
    private String Name;
    private int Cost;

    public Product(int productId, String name, int cost) {
        ProductId = productId;
        Name = name;
        Cost = cost;
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
}
