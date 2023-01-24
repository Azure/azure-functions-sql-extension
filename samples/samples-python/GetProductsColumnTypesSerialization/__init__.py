# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import logging

import azure.functions as func

# This function verifies that serializing an item with various data types
# works as expected.
def main(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    logging.info(json.dumps(products.__dict__, default=lambda o: o.to_json()))

    return func.HttpResponse(
        body=json.dumps(products.__dict__, default=lambda o: o.to_json()),
        status_code=200,
        mimetype="application/json"
    )
