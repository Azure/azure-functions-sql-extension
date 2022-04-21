module.exports = async function (context, req, product) {
    context.log('JavaScript HTTP trigger function processed a request.');
    if (product.length === 0) {
        context.log("Product not found");
    } else {
        context.log("Found Product(s), Count=" + product.length + " -> " + JSON.stringify(product));
    }
    return {
        status: 201,
        body: product
    };
}