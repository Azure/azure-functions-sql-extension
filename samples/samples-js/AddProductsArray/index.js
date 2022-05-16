module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.bindings.products = req.body;

    return {
        status: 201,
        body: products
    };
}