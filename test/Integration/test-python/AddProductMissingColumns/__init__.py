# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.productmissingcolumns import ProductMissingColumns

# This output binding should successfully add the productMissingColumns object
# to the SQL table.
def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    productMissingColumns = func.SqlRow(ProductMissingColumns(1, "test"))
    product.set(productMissingColumns)

    return func.HttpResponse(
        body=productMissingColumns.to_json(),
        status_code=201,
        mimetype="application/json"
    )
