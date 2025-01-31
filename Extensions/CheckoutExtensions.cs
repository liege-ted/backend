using Stripe;
using Stripe.Checkout;
using TED.API.Models;

namespace TED.API.Extensions
{
    public static class CheckoutExtensions
    {
        /// <summary>
        /// Creates a stripe checkout session link for the user to purchase a subscription
        /// </summary>
        /// <param name="customer">A stripe customer; to be created when authorized</param>
        /// <param name="prodId">The stripe product id of the product to be sold</param>
        /// <param name="guilds">An array of Guild objects that the user has permission to manage</param>
        /// <param name="userid">The id of the user associated with the stripe customer</param>
        /// <returns>A stripe checkout session link</returns>
        public static string CreateCheckoutSessionLink(Customer customer, string prodId, Guild[] guilds, string userid)
        {
            var prodService = new ProductService();
            var product = prodService.Get(prodId);

            var priceService = new PriceService();
            var price = priceService.Get(product.DefaultPriceId);

            var options = new SessionCreateOptions
            {
                SuccessUrl = "https://discord.gg/uuDZzBsNvA",
                CancelUrl = "https://discord.gg/uuDZzBsNvA",
                Mode = "subscription",
                //Customer = customer.Id,
                CustomerEmail = customer.Email,
                PaymentMethodTypes =
                [
                    "card",
                    "cashapp"
                ],
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        Price = price.Id,
                    }
                ],
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Description = "You will be charged $2.99 every month until you cancel your subscription.",
                },
                AllowPromotionCodes = true,
                CustomFields =
                [
                    new SessionCustomFieldOptions
                    {
                        Key = "guild",
                        Label = new SessionCustomFieldLabelOptions
                        {
                            Custom = "Guild",
                            Type = "custom"
                        },
                        Type = "dropdown",
                        Dropdown = new SessionCustomFieldDropdownOptions
                        {
                            Options = StringToDropDownOptions(guilds, userid)
                        }
                    }
                ],
                CustomText = new SessionCustomTextOptions
                {
                    Submit = new SessionCustomTextSubmitOptions
                    {
                        Message = "If the provided Guild ID is invalid, your purchase may not be refunded."
                    },
                },
            };
            var service = new SessionService();
            var create = service.Create(options);

            return create.Url;
        }

        /// <summary>
        /// Builds the stripe dropdown options from the guilds the user has permission to manage
        /// </summary>
        /// <returns>A list of stripes dropdown option objects</returns>
        private static List<SessionCustomFieldDropdownOptionOptions> StringToDropDownOptions(Guild[] options, string userid)
        {
            return options.Select(option => 
                new SessionCustomFieldDropdownOptionOptions 
                    { Label = $"{option.Name} - ({option.Id})", Value = $"{option.Id}S{userid}" }
            ).ToList();
        }
    }
}
