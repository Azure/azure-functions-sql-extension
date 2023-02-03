/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import com.fasterxml.jackson.annotation.JsonProperty;

public class ProductUnsupportedTypes {
    @JsonProperty("ProductId")
    private int ProductId;
    @JsonProperty("Text")
    private String Text;
    @JsonProperty("Ntext")
    private String Ntext;
    @JsonProperty("Image")
    private String Image;

    public ProductUnsupportedTypes() {
    }

    public ProductUnsupportedTypes(int productId, String text, String ntext, String image) {
        ProductId = productId;
        Text = text;
        Ntext = ntext;
        Image = image;
    }

    public int getProductId() {
        return ProductId;
    }

    public void setProductId(int productId) {
        ProductId = productId;
    }

    public String getText() {
        return Text;
    }

    public void setText(String text) {
        Text = text;
    }

    public String getNtext() {
        return Ntext;
    }

    public void setNtext(String ntext) {
        Ntext = ntext;
    }

    public String getImage() {
        return Image;
    }

    public void setImage(String image) {
        Image = image;
    }
}