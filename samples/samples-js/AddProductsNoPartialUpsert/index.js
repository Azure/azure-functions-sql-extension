module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    var products = [];
    for(let i = 0; i < 1000; i++) {
        products.push(...{"productid": i,
        "name": "test"+i,
        "cost": i});
    }
    const invalidProduct = {
        "productid": 1000,
        "name": null,
        "cost": 1000
    };

    products.push(...invalidProduct);

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}