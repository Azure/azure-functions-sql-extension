# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductDefaultPKAndDifferentColumnOrder(collections.UserDict):
    def __init__(self, cost, name):
        super().__init__()
        self['Cost'] = cost
        self['Name'] = name
