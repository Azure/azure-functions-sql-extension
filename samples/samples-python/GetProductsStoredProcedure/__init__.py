# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func

# `SelectProductsCost` is the name of a procedure stored in the user's database.
# The CommandType is `StoredProcedure`. The parameter value of the `@Cost` parameter in the
# procedure is the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL.
def main(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    rows = list(map(lambda r: json.loads(r.to_json()), products))

    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
