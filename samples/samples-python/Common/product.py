# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class Product(collections.UserDict):
    def __getitem__(self, key):
        return collections.UserDict.__getitem__(self, key)

    def __setitem__(self, key, value):
        return collections.UserDict.__setitem__(self, key, value)

    def __init__(self, productId, name, cost):
        super().__init__()
        self['ProductId'] = productId
        self['Name'] = name
        self['Cost'] = cost