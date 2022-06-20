// This output binding should throw an Exception because the ProductExtraColumns object has 
// two properties that do not exist as columns in the SQL table (ExtraInt and ExtraString).
module.exports = async function (context) {
    const products = [{
        "productId": 1001,
        "name": "Shoes",
        "cost": "100",
        "extraint": 1,
        "extrastring": "test"
    }];

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}