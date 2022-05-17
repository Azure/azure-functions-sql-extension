module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const itemToInsert = {
        "name": req.query?.name,
        "cost": req.query?.cost
    };
    context.bindings.product =  JSON.stringify(itemToInsert);

    return {
        status: 201,
        body: context.bindings.product
    };
}