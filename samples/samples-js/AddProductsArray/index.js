module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const products = [{
        "productid": 1,
        "name": "Cup",
        "cost": 2
    }, {
        "productid": 2,
        "name": "Glasses",
        "cost": 12
    }];

    context.bindings.products = req["body"] ?? JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}