# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import logging

def main(changes):
    logging.info("Trigger1 Changes: %s", json.loads(changes))
