# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductMissingColumns(collections.UserDict):
    def __init__(self, productId, name):
        super().__init__()
        self['ProductId'] = productId
        self['Name'] = name