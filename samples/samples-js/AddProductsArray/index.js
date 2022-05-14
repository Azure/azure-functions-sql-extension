module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const products = [{
        "productId": 1,
        "name": "Cup",
        "cost": 2
    }, {
        "productId": 2,
        "name": "Glasses",
        "cost": 12
    }];

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}