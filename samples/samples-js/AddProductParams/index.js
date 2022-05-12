module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const newProduct = {
        "productid": req.params?.productid ?? null,
        "name": req.params?.name ?? null,
        "cost": req.params?.cost ?? null
    };

    context.bindings.products = JSON.stringify(newProduct);

    return {
        status: 201,
        body: context.bindings.products
    };
}