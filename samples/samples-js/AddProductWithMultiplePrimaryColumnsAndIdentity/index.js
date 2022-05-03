module.exports = async function (context, request) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "name": "TestItem",
        "cost": "100"
    };

    context.bindings.product = request["body"].item ?? JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}