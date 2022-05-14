module.exports = async function (context, request) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "externalid": 101,
        "name": "TestItem",
        "cost": "100"
    };

    context.bindings.product = request.body ?? JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}