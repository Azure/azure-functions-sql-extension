module.exports = async function (context, req, products) {
    context.log('JavaScript HTTP trigger function processed a request.');
    if (products.length === 0) {
        context.log("Product(s) not found");
    } else {
        context.log("Found Product(s), Count=" + products.length + " -> " + JSON.stringify(products));
    }
    return {
        status: 201,
        body: products
    };
}