# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func
from Common.productunsupportedtypes import ProductUnsupportedTypes

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    """This output binding should throw an exception because the target table has unsupported column types.
    """

    row = func.SqlRow(ProductUnsupportedTypes(
        0,
        "test",
        "test",
        "dGVzdA=="
    ))
    product.set(row)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )
