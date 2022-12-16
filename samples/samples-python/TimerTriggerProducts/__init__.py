# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.product import Product
import datetime
import logging

def main(myTimer: func.TimerRequest, products: func.Out[func.SqlRowList]) -> func.HttpResponse:
    totalUpserts = 1000
    logging.info(f"{str(datetime.datetime.now())} starting execution. Rows to generate={totalUpserts}")

    start = datetime.datetime.now()

    rows = func.SqlRowList()
    for i in range(totalUpserts):
        row = func.SqlRow(Product(i, "test", 100 * i))
        rows.append(row)

    duration = datetime.datetime.now() - start
    products.set(rows)

    logging.info(f"{str(datetime.datetime.now())} finished execution. Total time to create {totalUpserts} rows={duration}")