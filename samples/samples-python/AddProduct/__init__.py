# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func


def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    row = func.SqlRow({"ProductId": req.params["id"], "Name": req.params["name"], "Cost": req.params["cost"]})
    product.set(row)
    return func.HttpResponse(
        row.to_json(),
        status_code=201,
        mimetype="application/json"
    )
