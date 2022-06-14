// This output binding should throw an error since the ProductsNameNotNull table does not 
// allows rows without a Name value. No rows should be upserted to the Sql table.
module.exports = async function (context) {
    var products = [];
    for(let i = 0; i < 1000; i++) {
        products.add({"productId": i,
        "name": "test",
        "cost": 100 * i});
    }
    const invalidProduct = {
        "productid": 1000,
        "name": null,
        "cost": 1000
    };

    products.add(invalidProduct);

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}