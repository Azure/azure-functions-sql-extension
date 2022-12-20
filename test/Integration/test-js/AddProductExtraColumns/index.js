// This output binding should throw an Exception because the ProductExtraColumns object has 
// two properties that do not exist as columns in the SQL table (ExtraInt and ExtraString).
module.exports = async function (context) {
    const products = [{
        "ProductId": 1001,
        "Name": "Shoes",
        "Cost": "100",
        "Extraint": 1,
        "Extrastring": "test"
    }];

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}