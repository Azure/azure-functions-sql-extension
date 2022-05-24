# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

def main(req: func.HttpRequest, products: func.Out[func.SqlRowList]) -> func.HttpResponse:
    """This function upserts the products, which will insert them into the Products table if
    the primary key (ProductId) for that item doesn't exist. If it does then update it to have
    the new name and cost.
    """

    # Note that this expects the body to be an array of JSON objects which
    # have a property matching each of the columns in the table to upsert to.
    body = json.loads(req.get_body())
    rows = func.SqlRowList(map(lambda r: func.SqlRow.from_dict(r), body))
    products.set(rows)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )
