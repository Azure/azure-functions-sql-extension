# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductWithoutId(collections.UserDict):
    def __init__(self, name, cost):
        super().__init__()
        self['Name'] = name
        self['Cost'] = cost
        