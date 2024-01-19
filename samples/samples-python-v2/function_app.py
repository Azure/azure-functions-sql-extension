# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import logging
import azure.functions as func
from azure.functions.decorators.core import DataType

app = func.FunctionApp()

# Learn more at aka.ms/pythonprogrammingmodel

# The input binding executes the `SELECT * FROM Products WHERE Cost = @Cost` query.
# The parameters argument passes the `{cost}` specified in the URL that triggers the function,
# `getproducts/{cost}`, as the value of the `@Cost` parameter in the query.
# command_type is set to `Text`, since the constructor argument of the binding is a raw query.
@app.function_name(name="GetProducts")
@app.route(route="getproducts/{cost}")
@app.sql_input(arg_name="products",
                        command_text="SELECT * FROM Products WHERE Cost = @Cost",
                        command_type="Text",
                        parameters="@Cost={cost}",
                        connection_string_setting="SqlConnectionString")
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
@app.sql_output(arg_name="product",
                        command_text="[dbo].[Products]",
                        connection_string_setting="SqlConnectionString")
def add_product(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    body = json.loads(req.get_body())
    row = func.SqlRow.from_dict(body)
    product.set(row)

    return func.HttpResponse(
        body=req.get_body(),
        status_code=201,
        mimetype="application/json"
    )

# The function gets triggered when a change (Insert, Update, or Delete)
# is made to the Products table.
@app.function_name(name="ProductsTrigger")
@app.sql_trigger(arg_name="products",
                        table_name="Products",
                        connection_string_setting="SqlConnectionString")
def products_trigger(products: str) -> None:
    logging.info("SQL Changes: %s", json.loads(products))