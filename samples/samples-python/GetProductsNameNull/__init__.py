# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

# If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null", the
# query returns all rows for which the Name column is `NULL`. Otherwise, it returns
# all rows for which the value of the Name column matches the string passed in `{name}`
def main(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    rows = list(map(lambda r: json.loads(r.to_json()), products))

    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
