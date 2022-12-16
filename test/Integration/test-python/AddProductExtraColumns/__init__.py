# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.productextracolumns import ProductExtraColumns

# This output binding should throw an Exception because the ProductExtraColumns object has
# two properties that do not exist as columns in the SQL table (ExtraInt and ExtraString).
def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    productExtraColumns = func.SqlRow(ProductExtraColumns(1, "test", 100, 1, "test"))
    product.set(productExtraColumns)

    return func.HttpResponse(
        body=productExtraColumns.to_json(),
        status_code=201,
        mimetype="application/json"
    )
