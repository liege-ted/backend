using Stripe;
using Stripe.Checkout;
using TED.API.Models;

namespace TED.API.Extensions
{
    public class CheckoutExtensions
    {
        public static string CreateCheckoutSessionLink(Customer customer, string prodId, List<Guild> guilds, ulong userid)
        {
            var prodService = new ProductService();
            var product = prodService.Get(prodId);
            Console.WriteLine($"Product: " + product.Id);

            var priceService = new PriceService();
            var price = priceService.Get(product.DefaultPriceId);
            Console.WriteLine($"Price: " + price.Id);

            Console.WriteLine($"Customer: " + customer.Id);
            Console.WriteLine($"User Id: " + userid);

            var options = new SessionCreateOptions
            {
                SuccessUrl = "https://discord.gg/uuDZzBsNvA",
                CancelUrl = "https://discord.gg/uuDZzBsNvA",
                Mode = "subscription",
                //Customer = customer.Id,
                CustomerEmail = customer.Email,
                PaymentMethodTypes = new List<string>
                {
                    "card",
                    "cashapp",
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        Price = price.Id,
                    },
                },
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Description = "You will be charged $2.99 every month until you cancel your subscription.",
                },
                AllowPromotionCodes = true,
                CustomFields = new List<SessionCustomFieldOptions>
                {
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
                },
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

        private static List<SessionCustomFieldDropdownOptionOptions> StringToDropDownOptions(List<Guild> options, ulong userid)
        {
            return options.Select(option => 
                new SessionCustomFieldDropdownOptionOptions 
                    { Label = $"{option.Name} - ({option.Id})", Value = $"{option.Id}S{userid}" }
            ).ToList();
        }
    }
}
