/**
 * This function verifies that serializing an item with various data types
 * works when the language is set to a non-enUS language.
 */
module.exports = async function (context, req, products) {
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}