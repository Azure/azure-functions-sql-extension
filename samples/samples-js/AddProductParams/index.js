module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const newProduct = {
        "productId": req.params?.productId,
        "name": req.params?.name,
        "cost": req.params?.cost
    };

    context.bindings.products = JSON.stringify(newProduct);

    return {
        status: 201,
        body: context.bindings.products
    };
}