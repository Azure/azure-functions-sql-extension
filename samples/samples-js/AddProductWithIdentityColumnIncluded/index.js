module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const itemToInsert = {
        "productId" : req.params?.productId,
        "name": req.params?.name ?? "productWithoutIdentity",
        "cost": req.params?.cost ?? 100
    };

    context.bindings.product = JSON.stringify(itemToInsert);

    return {
        status: 201,
        body: context.bindings.products
    };
}