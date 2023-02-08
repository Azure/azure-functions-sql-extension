# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductUnsupportedTypes(collections.UserDict):
    def __init__(self, productId, textCol, ntextCol, imageCol):
        super().__init__()
        self['ProductId'] = productId
        self['TextCol'] = textCol
        self['NtextCol'] = ntextCol
        self['ImageCol'] = imageCol