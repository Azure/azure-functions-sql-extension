# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

def main(req: func.HttpRequest, products: func.SqlRowList, productsWithIdentity: func.Out[func.SqlRowList]) -> func.HttpResponse:
    """This function uses a SQL input binding to get products from the Products table
    and upsert those products to the ProductsWithIdentity table.
    """

    productsWithIdentity.set(products)

    rows = list(map(lambda r: json.loads(r.to_json()), products))
    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
