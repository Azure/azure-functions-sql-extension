module.exports = async function (context, request) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "externalId": req.params?.externalId ?? 101,
        "name": req.params?.name ?? "TestItem",
        "cost": req.params?.cost ?? "100"
    };

    context.bindings.product = request.body ?? JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}