module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const products = [{
        "productId": 1,
        "name": "MissingCost"
    }];

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}