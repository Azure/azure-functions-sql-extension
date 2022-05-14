module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const newProduct = {
        "productId": req.params?.productId ?? 123,
        "name": req.params?.name ?? 'new',
        "cost": req.params?.cost ?? 100
    };

    context.bindings.products = JSON.stringify(newProduct);

    return {
        status: 201,
        body: context.bindings.products
    };
}