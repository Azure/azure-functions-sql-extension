# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductWithSlash(collections.UserDict):
    def __init__(self, productId, name, cost):
        super().__init__()
        self['ProductId'] = productId
        self['Name/Test'] = name
        self['Cost\Test'] = cost
