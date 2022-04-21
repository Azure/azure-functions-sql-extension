module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const ringProduct = [{
        "productid": 11,
        "name": "ball",
        "cost": "5"
    }, {
        "productid": 12,
        "name": "milk",
        "cost": "5"
    }];

    if (ringProduct) {
        context.bindings.product = JSON.stringify(ringProduct);
    }

    return {
        status: 201,
        body: ringProduct
    };
}