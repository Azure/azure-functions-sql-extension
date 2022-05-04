module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "ProductId": 123,
        "Name": "TestItem",
        "Cost": 100
    };

    context.bindings.products = req["body"]?.item ?? JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.products
    };
}