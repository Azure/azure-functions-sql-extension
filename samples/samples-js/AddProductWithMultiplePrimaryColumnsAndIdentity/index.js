module.exports = async function (context, request) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "externalId": req.query?.externalId,
        "name": req.query?.name,
        "cost": req.query?.cost
    };

    context.bindings.product = JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}