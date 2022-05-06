# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
import json

def main(req: func.HttpRequest, rows: func.Out[func.SqlRowList]) -> func.HttpResponse:
    output = [
        func.SqlRow({
            "ProductId": 1,
            "Name": "Cup",
            "Cost": 2
        }),
        func.SqlRow({
            "ProductId": 2,
            "Name": "Glasses",
            "Cost": 12
        })
    ]
    rows.set(output)
    output_str = json.dumps(list(map(lambda r: json.loads(r.to_json()), output)))
    return func.HttpResponse(
        body=output_str,
        status_code=201,
        mimetype="application/json"
    )