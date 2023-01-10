# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductColumnTypes(collections.UserDict):
    def __init__(self, productId, datetime, datetime2):
        super().__init__()
        self['ProductId'] = productId
        self['Datetime'] = datetime
        self['Datetime2'] = datetime2
