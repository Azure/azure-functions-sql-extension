# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import azure.functions as func
from Common.product import Product
import datetime
import logging

def main(queueMessage: func.QueueMessage, products: func.Out[func.SqlRowList]):
    totalUpserts = 100
    logging.info("[QueueTrigger]: " + str(datetime.datetime.now()) + " starting execution " + queueMessage.get_body().decode('utf-8')+ ". Rows to generate=" + str(totalUpserts))

    start = datetime.datetime.now()
    rows = func.SqlRowList()
    for i in range(totalUpserts):
        row = func.SqlRow(Product(i, "test", 100 * i))
        rows.append(row)
    products.set(rows)
    duration = datetime.datetime.now() - start

    logging.info("[QueueTrigger]: " + str(datetime.datetime.now()) + " finished execution " + queueMessage.get_body().decode('utf-8') + ". Total time to create " + str(totalUpserts) + " rows=" + str(duration))