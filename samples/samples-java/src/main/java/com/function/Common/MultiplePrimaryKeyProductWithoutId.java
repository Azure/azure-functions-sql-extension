package com.function.Common;

public class MultiplePrimaryKeyProductWithoutId {
    private int ExternalId;
    private String Name;
    private int Cost;

    public MultiplePrimaryKeyProductWithoutId(int externalId, String name, int cost) {
        ExternalId = externalId;
        Name = name;
        Cost = cost;
    }

    public int getExternalId() {
        return ExternalId;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }
}
