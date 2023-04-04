# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import azure.functions as func
from azure.functions.decorators.core import DataType

app = func.FunctionApp()

# Learn more at aka.ms/pythonprogrammingmodel

# The input binding executes the `SELECT * FROM Products WHERE Cost = @Cost` query.
# The Parameters argument passes the `{cost}` specified in the URL that triggers the function,
# `getproducts/{cost}`, as the value of the `@Cost` parameter in the query.
# CommandType is set to `Text`, since the constructor argument of the binding is a raw query.
@app.function_name(name="GetProducts")
@app.route(route="getproducts/{cost}")
@app.generic_input_binding(arg_name="products", type="sql",
                        CommandText="SELECT * FROM Products WHERE Cost = @Cost",
                        Parameters="@Cost={cost}",
                        ConnectionStringSetting="SqlConnectionString",
                        data_type=DataType.STRING)
def get_products(req: func.HttpRequest, products: func.SqlRowList) -> func.HttpResponse:
    rows = list(map(lambda r: json.loads(r.to_json()), products))

    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )

# The output binding upserts the product, which will insert it into the Products table if
# the primary key (ProductId) for that item doesn't exist. If it does then update it to
# have the new name and cost.
@app.function_name(name="AddProduct")
@app.route(route="addproduct")
@app.generic_output_binding(arg_name="product", type="sql",
                        CommandText="[dbo].[Products]",
                        ConnectionStringSetting="SqlConnectionString",
                        data_type=DataType.STRING)
def add_product(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    body = json.loads(req.get_body())
    row = func.SqlRow.from_dict(body)
    product.set(row)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )
