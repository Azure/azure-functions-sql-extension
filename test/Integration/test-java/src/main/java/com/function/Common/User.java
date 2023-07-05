/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class User {
    private int UserId;
    private String UserName;
    private String FullName;

    public User(int userId, String userName, String fullName) {
        UserId = userId;
        UserName = userName;
        FullName = fullName;
    }

    public int getUserId() {
        return UserId;
    }

    public String getUserName() {
        return UserName;
    }

    public String getFullName() {
        return FullName;
    }
}
