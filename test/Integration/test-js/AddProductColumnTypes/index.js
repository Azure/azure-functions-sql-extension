/**
 * This function is used to test compatability with converting various data types to their respective
 * SQL server types.
 */
module.exports = async function (context, req) {
    const product = {
        "productId": req.query.productId,
        "datetime": Date.now(),
        "datetime2": Date.now()
    };

    context.bindings.product = JSON.stringify(product);

    return {
        status: 201,
        body: product
    };
}