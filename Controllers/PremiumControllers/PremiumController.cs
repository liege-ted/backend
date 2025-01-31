using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using TED.Additions;
using TED.API.Extensions;

namespace TED.API.Controllers.PremiumControllers
{
    [ApiController]
    [Route("premium")]
    public class PremiumController(HttpClient httpClient) : BaseController
    {
        private readonly string _clientId = Environment.GetEnvironmentVariable("CLIENT_ID")!;
        private readonly string _clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET")!;
        
        [HttpPost("action")]
        public async Task<IActionResult> HandlePremiumActionAsync()
        {
            using (var reader = new StreamReader(Request.Body))
            {
                var json = await reader.ReadToEndAsync();
                try
                {
                    var stripeEvent = EventUtility.ParseEvent(json);

                    switch (stripeEvent.Type)
                    {
                        case Events.CheckoutSessionCompleted:
                        {
                            // make the user and guild premium
                            if (stripeEvent.Data.Object is not Session data) break;
                            
                            // get guild id from custom dropdown field; 'S' is the separator
                            var selection = data.CustomFields[0].Dropdown.Value;
                            var guildid = selection.Split("S")[0];

                            var gs = await PremiumMethods.GetOrCreatePremiumAsync(guildid, data.SubscriptionId);
                            
                            await gs.ModifyAsync(x =>
                            {
                                x.Premium.CustomerId = data.CustomerId;
                                x.Premium.IsPremium = true;
                                x.Premium.TotalPaid += (data.AmountTotal / 100) ?? 2.99;
                                x.Premium.LastPaid = DateTime.UtcNow;
                            });
                            
                            break;
                        }
                        case Events.InvoicePaymentSucceeded:
                        {
                            if (stripeEvent.Data.Object is not Invoice data) break;

                            var gs = await PremiumMethods.GetPremiumFromSubscriptionAsync(data.SubscriptionId);
                            if (gs is null) break;
                            
                            await gs.ModifyAsync(x =>
                            {
                                x.Premium!.CustomerId = data.CustomerId;
                                x.Premium.IsPremium = true;
                                x.Premium.TotalPaid += data.AmountPaid / 100;
                                x.Premium.LastPaid = DateTime.UtcNow;
                            });
                            
                            break;
                        }
                        case Events.CustomerSubscriptionPaused:
                        {
                            if (stripeEvent.Data.Object is not Subscription data) break;

                            var gs = await PremiumMethods.GetPremiumFromSubscriptionAsync(data.Id);
                            if (gs is null) break;
                            
                            await gs.ModifyAsync(x =>
                            {
                                x.Premium.IsPremium = false;
                            });
                            
                            break;
                        }
                        case Events.CustomerSubscriptionDeleted:
                        {
                            if (stripeEvent.Data.Object is not Subscription data) break;
                            
                            var gs = await PremiumMethods.GetPremiumFromSubscriptionAsync(data.Id);
                            if (gs is null) break;
                            
                            await gs.ModifyAsync(x =>
                            {
                                x.Premium.IsPremium = false;
                                x.Premium.SubscriptionId = ""; // this needs to be done when a subscription is deleted!!
                            });
                            
                            break;
                        }
                        default:
                            // Unexpected event type
                            Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
                            return JsonNotFound();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled exception: {ex.Message}");
                    return JsonBadRequest();
                }
            }

            return Ok();
        }
        
        [HttpGet("oauth")]
        public async Task<IActionResult> HttpGetOAuthAsync(string? code)
        {
            // if code is null, redirect to discord oauth2
            if (code == null)
            {
                return Redirect("https://discord.com/api/oauth2/authorize?client_id=879360985738117120&response_type=code&redirect_uri=https%3A%2F%2Fpremium.liege.dev&scope=email+identify+guilds");
            }
            
            // oauth data for discord
            var oauthData = new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", "https://api.liege.dev/premium" },
            };

            // encode content and post to discord oauth2
            var content = new FormUrlEncodedContent(oauthData);
            var result = await httpClient.PostAsync("https://discord.com/api/v10/oauth2/token", content);

            if (result.StatusCode != HttpStatusCode.OK) return BadRequest("Discord OAuth failed! (2)");
            
            // if post request is successful, get user data
            try
            {
                // do discord oauth2 stuff
                var postData = JsonNode.Parse(await result.Content.ReadAsStringAsync());
                if (postData is null) return JsonBadRequest("Failed to parse data!");
                    
                var token = postData["access_token"];
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var getResult = await httpClient.GetAsync("https://discord.com/api/v10/users/@me");

                var getData = JsonNode.Parse(await getResult.Content.ReadAsStringAsync());
                if (getData?["id"] is null) return JsonBadRequest("Failed to parse data!");
                    
                // get guilds the user has permission to manage
                var guilds = await httpClient.GetPermissionGuildsAsync();
                if (guilds is null) return JsonBadRequest("Failed to get guilds!");
                    
                // get user id from Discord response
                var userid = getData["id"]!.ToString();

                StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

                // create a new customer
                var options = new CustomerCreateOptions
                {
                    Name = getData["username"]?.ToString(),
                    Description = $"{userid}",
                    Email = getData["email"]?.ToString(),
                };

                // create a new customer and get the link to the checkout session
                var service = new CustomerService();
                var cus = await service.CreateAsync(options);
                var prodid = Environment.GetEnvironmentVariable("STRIPE_PRODUCT_ID");

                var link = CheckoutExtensions.CreateCheckoutSessionLink(cus, prodid!, guilds, userid);

                return Redirect(link);
            }
            catch
            {
                return JsonInternalServerError("Discord OAuth failed!");
            }
        }
    }
}