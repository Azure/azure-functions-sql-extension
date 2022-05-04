import azure.functions as func


def main(req: func.HttpRequest, product: func.Out[func.SqlRow]) -> func.HttpResponse:
    product.set(func.SqlRow({"productid": req.params["id"], "name": req.params["name"], "cost": req.params["cost"]}))
    return func.HttpResponse()
