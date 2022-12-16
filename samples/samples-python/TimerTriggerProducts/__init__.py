# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import datetime
import logging

import azure.functions as func
from Common.product import Product

def main(myTimer: func.TimerRequest, products: func.Out[func.SqlRowList]):
    totalUpserts = 1000
    logging.info("%s starting execution. Rows to generate=%s", str(datetime.datetime.now()),
        str(totalUpserts))

    start = datetime.datetime.now()
    rows = func.SqlRowList()
    for i in range(totalUpserts):
        row = func.SqlRow(Product(i, "test", 100 * i))
        rows.append(row)
    products.set(rows)
    duration = datetime.datetime.now() - start

    logging.info("%s finished execution. Total time to create %s rows=%s",
        str(datetime.datetime.now()), str(totalUpserts), str(duration))
