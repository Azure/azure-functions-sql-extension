import azure.functions as func
import json

def main(req: func.HttpRequest, rowList: func.SqlRowList) -> func.HttpResponse:
    rows = []

    for row in rowList:
        row_json = {
           'id': row['ProductId'],
           'name': row['Name'],
           'cost': row['Cost']
        }
        rows.append(row_json)

    return func.HttpResponse(
        json.dumps(rows),
        status_code=200,
        mimetype="application/json"
    )
