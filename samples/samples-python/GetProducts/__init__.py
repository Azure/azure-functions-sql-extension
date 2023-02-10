# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

# The input binding executes the `SELECT * FROM Products WHERE Cost = @Cost` query.
# The Parameters argument passes the `{cost}` specified in the URL that triggers the function,
# `getproducts/{cost}`, as the value of the `@Cost` parameter in the query.
# CommandType is set to `Text`, since the constructor argument of the binding is a raw query.
def main(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    rows = list(map(lambda r: json.loads(r.to_json()), products))

    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
