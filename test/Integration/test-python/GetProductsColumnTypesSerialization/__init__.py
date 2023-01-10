# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json

import azure.functions as func

# This function verifies that serializing an item with various data types
# works as expected.
def main(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    rows = list(map(lambda r: json.loads(r.to_json()), products))

    return func.HttpResponse(
        body=json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
