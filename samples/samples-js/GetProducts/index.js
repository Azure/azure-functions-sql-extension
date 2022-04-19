module.exports = async function (context, req, product) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.log(context.bindings);
    if (product.length === 0)
    {
        context.log("Product not found");
    }
    else
    {
        context.log("Found Product item, Name=" + product);
    }
    const name = (product?.Name || (req.body && req.body.name));
    const responseMessage = name
        ? "Hello, " + name + ". This HTTP triggered function executed successfully."
        : "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";
    return { status: 201, body: product };
}

