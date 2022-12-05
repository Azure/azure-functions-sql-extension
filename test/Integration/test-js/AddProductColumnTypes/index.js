/**
 * This function is used to test compatability with converting various data types to their respective
 * SQL server types.
 */
module.exports = async function (context, req) {
    const product = {
        "ProductId": req.query.productId,
        "Datetime": new Date().toISOString(),
        "Datetime2": new Date().toISOString()
    };

    context.bindings.product = JSON.stringify(product);

    return {
        status: 201,
        body: product
    };
}