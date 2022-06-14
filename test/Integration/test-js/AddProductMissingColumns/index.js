// This output binding should successfully add the productMissingColumns object
// to the SQL table.
module.exports = async function (context) {
    const productMissingColumns = [{
        "productId": 1,
        "name": "MissingCost"
    }];

    context.bindings.products = JSON.stringify(productMissingColumns);

    return {
        status: 201,
        body: productMissingColumns
    };
}