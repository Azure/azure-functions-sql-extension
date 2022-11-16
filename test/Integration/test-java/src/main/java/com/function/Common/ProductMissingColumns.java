/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class ProductMissingColumns {
    private int ProductId;
    private String Name;

    public ProductMissingColumns(int productId, String name) {
        ProductId = productId;
        Name = name;
    }

    public int getProductId() {
        return ProductId;
    }

    public String getName() {
        return Name;
    }
}
