# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import datetime
import logging

import azure.functions as func
from Common.product import Product

def main(queueMessage: func.QueueMessage, products: func.Out[func.SqlRowList]):
    totalUpserts = 100
    logging.info("[QueueTrigger]: %s starting execution %s. Rows to generate=%s",
        str(datetime.datetime.now()), queueMessage.get_body().decode('utf-8'), str(totalUpserts))

    start = datetime.datetime.now()
    rows = func.SqlRowList()
    for i in range(totalUpserts):
        row = func.SqlRow(Product(i, "test", 100 * i))
        rows.append(row)
    products.set(rows)
    duration = datetime.datetime.now() - start

    logging.info("[QueueTrigger]: %s finished execution %s. Total time to create %s rows=%s",
        str(datetime.datetime.now()), queueMessage.get_body().decode('utf-8'), str(totalUpserts),
        str(duration))
