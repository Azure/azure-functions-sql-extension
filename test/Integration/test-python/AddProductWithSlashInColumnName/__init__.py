# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func
from Common.productWithSlash import ProductWithSlash

# This output binding should successfully add the productMissingColumns object
# to the SQL table.
def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    productWithSlash = func.SqlRow.from_dict(json.loads(req.get_body())) if req.get_body() else func.SqlRow(ProductWithSlash(1, "Test", 1))
    product.set(productWithSlash)

    return func.HttpResponse(
        body=productWithSlash.to_json(),
        status_code=201,
        mimetype="application/json"
    )
