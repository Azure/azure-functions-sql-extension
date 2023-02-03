# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    """This output binding should throw an exception because the target table has unsupported column types.
    """

    body = json.loads(req.get_body())
    row = func.SqlRow.from_dict(body)
    product.set(row)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )
