# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class MultiplePrimaryKeyProductWithoutId(collections.UserDict):
    def __init__(self, externalId, name, cost):
        super().__init__()
        self['ExternalId'] = externalId
        self['Name'] = name
        self['Cost'] = cost
        