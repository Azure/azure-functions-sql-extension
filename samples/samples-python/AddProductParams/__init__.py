# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.product import Product

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    row = func.SqlRow(Product(req.params["id"], req.params["name"], req.params["cost"]))
    product.set(row)

    return func.HttpResponse(
        body=row.to_json(),
        status_code=201,
        mimetype="application/json"
    )

