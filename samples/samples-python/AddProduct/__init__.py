# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    """Upsert the product, which will insert it into the Products table if the primary key
    (ProductId) for that item doesn't exist. If it does then update it to have the new name
    and cost.
    """

    # Note that this expects the body to be a JSON object which
    # have a property matching each of the columns in the table to upsert to.
    body = json.loads(req.get_body())
    row = func.SqlRow.from_dict(body)
    product.set(row)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )
    