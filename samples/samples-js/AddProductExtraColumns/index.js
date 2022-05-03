module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const products = [{
        "productid": 1001,
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