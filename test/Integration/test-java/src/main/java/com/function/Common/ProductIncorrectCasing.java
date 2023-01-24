/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

 package com.function.Common;

 public class ProductIncorrectCasing {
     private int ProductID;
     private String Name;
     private int Cost;

     public ProductIncorrectCasing(int productID, String name, int cost) {
         ProductID = productID;
         Name = name;
         Cost = cost;
     }

     public int getProductID() {
         return ProductID;
     }

     public String getName() {
         return Name;
     }

     public int getCost() {
         return Cost;
     }
 }