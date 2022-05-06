# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func

def main(req: func.HttpRequest, rowStr: str) -> func.HttpResponse:
    # rowStr is a JSON representation of the returned rows. For example, if there are two returned rows,
    # rowStr could look like:
    # [{"ProductID":1,"Name":"Dress","Cost":100},{"ProductID":2,"Name":"Skirt","Cost":100}]
    return func.HttpResponse(
        rowStr,
        status_code=200,
        mimetype='application/json')