package com.function.Common;

public class ProductWithOptionalId {
    private Integer ProductID;
    private String Name;
    private int Cost;

    public ProductWithOptionalId(Integer productId, String name, int cost) {
        ProductID = productId;
        Name = name;
        Cost = cost;
    }

    public Integer getProductId() {
        return ProductID;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }
}
