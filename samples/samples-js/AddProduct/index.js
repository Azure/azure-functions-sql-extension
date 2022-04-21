module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const ringProduct = [{
        "productid": 1001,
        "name": "Shoes",
        "cost": "100"
    }, {
        "productid": 1002,
        "name": "Flowers",
        "cost": "500"
    }];

    if (ringProduct) {
        context.bindings.product = JSON.stringify(ringProduct);
    }

    return {
        status: 201,
        body: ringProduct
    };
}