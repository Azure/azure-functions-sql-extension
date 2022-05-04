module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "productid": 123,
        "name": "TestItem",
        "cost": 100
    };

    context.bindings.products = req["body"]?.item ?? JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.products
    };
}