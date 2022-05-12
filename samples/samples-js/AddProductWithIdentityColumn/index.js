module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const itemToInsert = {
        "name": "productWithIdentity",
        "cost": 100
    };

    context.bindings.products = req["body"] ?? JSON.stringify(itemToInsert);

    return {
        status: 201,
        body: context.bindings.products
    };
}