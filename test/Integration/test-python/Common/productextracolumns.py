# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductExtraColumns(collections.UserDict):
    def __init__(self, productId, name, cost, extraInt, extraString):
        super().__init__()
        self['ProductId'] = productId
        self['Name'] = name
        self['Cost'] = cost
        self['ExtraInt'] = extraInt
        self['ExtraString'] = extraString