# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from datetime import datetime
import logging
import azure.functions as func
from Common.product import Product

executionCount = 1

def main(timer: func.TimerRequest, rowList: func.Out[func.SqlRowList]) -> None:
    global executionCount
    totalUpserts = 1000
    startTime = datetime.now()
    logging.info('%s starting execution #%d. Rows to generate=%d', startTime, executionCount, totalUpserts)
    products = []
    for i in range(totalUpserts):
        products.append(func.SqlRow(Product(i, 'test', 100 * i)))
    rowList.set(products)
    endTime = datetime.now()
    executionTimeMs = (endTime - startTime).total_seconds() * 1000
    logging.info('%s finished execution #%d. Total time to create %s rows=%s', endTime, executionCount, totalUpserts, executionTimeMs)
    executionCount += 1
