# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductUnsupportedTypes(collections.UserDict):
    def __init__(self, productId, text, ntext, image):
        super().__init__()
        self['ProductId'] = productId
        self['Text'] = text
        self['Ntext'] = ntext
        self['Image'] = image