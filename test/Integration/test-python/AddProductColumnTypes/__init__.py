# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import datetime

import azure.functions as func
from Common.productcolumntypes import ProductColumnTypes

# This function is used to test compatibility with converting various data types to their respective
# SQL server types.
def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    productColumnTypes = func.SqlRow(ProductColumnTypes(req.params["productId"], datetime.datetime.utcnow().isoformat("T", "milliseconds"), datetime.datetime.utcnow().isoformat("T", "milliseconds")))
    product.set(productColumnTypes)

    return func.HttpResponse(
        body=productColumnTypes.to_json(),
        status_code=201,
        mimetype="application/json"
    )
