# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.multiplePrimaryKeyProductWithoutId import MultiplePrimaryKeyProductWithoutId

def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    """This shows an example of a SQL Output binding where the target table has a primary key
    which is comprised of multiple columns, with one of them being an identity column. In
    such a case the identity column is not required to be in the object used by the binding
    - it will insert a row with the other values and the ID will be generated upon insert.
    All other primary key columns are required to be in the object.
    """

    row_obj = func.SqlRow(MultiplePrimaryKeyProductWithoutId(req.params["externalId"],
        req.params["name"], req.params["cost"]))
    product.set(row_obj)

    return func.HttpResponse(
        body=row_obj.to_json(),
        status_code=201,
        mimetype="application/json"
    )
    