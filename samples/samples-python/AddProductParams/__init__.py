# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from urllib.parse import parse_qs, urlparse

import azure.functions as func
from Common.product import Product

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    # The Python worker discards empty query parameters so use parse_qs as a workaround.
    params = parse_qs(urlparse(req.url).query, keep_blank_values=True)
    row = func.SqlRow(Product(params["productId"][0], params["name"][0], params["cost"][0]))
    product.set(row)

    return func.HttpResponse(
        body=row.to_json(),
        status_code=201,
        mimetype="application/json"
    )
