module.exports = async function (context) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const ringProduct =[{"productid": 11, "name": "ball", "cost": "5"
}, {"productid": 12, "name": "milk", "cost": "5"}];
    const responseMessage = ringProduct.name
        ? "Hello, This HTTP triggered function executed successfully."
        : "This HTTP triggered function executed successfully. Pass product(s) in the query string or in the request body for a personalized response.";

    if (ringProduct) {
        context.bindings.product = JSON.stringify(ringProduct);
    }

    return { status: 201, body: ringProduct };
}