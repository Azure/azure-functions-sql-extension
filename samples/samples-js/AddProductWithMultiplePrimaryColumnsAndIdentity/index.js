module.exports = async function (context, request) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "externalId": req.params?.externalId,
        "name": req.params?.name,
        "cost": req.params?.cost
    };

    context.bindings.product = JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}