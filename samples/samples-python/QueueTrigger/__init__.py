# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging
from datetime import datetime
import azure.functions as func
from Common.product import Product

def main(msg: func.QueueMessage, rowList: func.Out[func.SqlRowList]) -> None:
    logging.info('Python queue trigger function processed a queue item: %s',
                 msg.get_body().decode('utf-8'))

    totalUpserts = 100
    logging.info('[QueueTrigger]: %s starting execution %s. Rows to generate=%d.', datetime.now(), msg.get_body().decode('utf-8'), totalUpserts)

    startTime = datetime.now()
    products = []
    for i in range(totalUpserts):
        products.append(func.SqlRow(Product(i, 'test', 100 * i)))
    rowList.set(products)

    endTime = datetime.now()
    executionTime = (endTime - startTime).total_seconds()
    logging.info('[QueueTrigger]: %s finished execution %s. Total time to create %d rows=%dms.', endTime, msg.get_body().decode('utf-8'), totalUpserts, executionTime)
