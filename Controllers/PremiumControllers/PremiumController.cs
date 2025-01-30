using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StackExchange.Redis.KeyspaceIsolation;
using Stripe;
using Stripe.Checkout;
using TED.Additions;
using TED.Services;

namespace TED.API.Controllers.PremiumControllers
{

    [ApiController]
    [Route("premium")]
    public class PremiumController : BaseController
    {
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
                            var data = stripeEvent.Data.Object as Session;
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
                            var data = stripeEvent.Data.Object as Invoice;

                            var gs = await PremiumMethods.GetOrCreatePremiumAsync(guildid, data.SubscriptionId);
                            
                            // just for safety, set premium to true and set cusid
                            // again even tho they should 100% be the same
                            await gs.ModifyAsync(x =>
                            {
                                x.Premium.CustomerId = data.CustomerId;
                                x.Premium.IsPremium = true;
                                x.Premium.TotalPaid += double.Parse(data.AmountTotal / 100);
                                x.Premium.LastPaid = DateTime.UtcNow;
                            });
                            
                            var premium = await DatabaseExtensions.GetPremiumBySubscriptionIdAsync(data.Subscription.Id);
                            await DatabaseExtensions.IncrementPaymentAsync(premium.GuildId, data.AmountPaid / 100);
                            Console.WriteLine($"PAYMENT SUCCEEDED from {premium.GuildId}");
                            break;
                        }
                        case Events.CustomerSubscriptionPaused:
                        {
                            var data = stripeEvent.Data.Object as Subscription;

                            var premium = MongoService.GuildSettings.Find(x => x.Premium.SubscriptionId == data.Id)
                                .FirstOrDefault();
                            await DatabaseExtensions.SetPremiumStatusAsync(premium.GuildId, false, data.Id);
                            Console.WriteLine($"PREMIUM PAUSED for {premium.GuildId}");
                            break;
                        }
                        case Events.CustomerSubscriptionDeleted:
                        {
                            var data = stripeEvent.Data.Object as Subscription;

                            var premium = MongoService.GuildSettings.Find(x => x.Premium.SubscriptionId == data.Id)
                                .FirstOrDefault();
                            await DatabaseExtensions.SetPremiumStatusAsync(premium.GuildId, false, data.Id);
                            Console.WriteLine($"PREMIUM DELETED for {premium.GuildId}");
                            break;
                        }
                        default:
                            // Unexpected event type
                            Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
                            return NotFound();
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization error: {ex.Message}");
                    return BadRequest("Bad request (1)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled exception: {ex.Message}");
                    return BadRequest("Bad request (2)");
                }
            }

            return Ok();
        }
        
        [HttpGet("oauth")]
        public async Task<IActionResult> Get(string? code)
        {
            // if code is null, redirect to discord oauth2
            if (code == null)
            {
                return Redirect("https://discord.com/api/oauth2/authorize?client_id=879360985738117120&response_type=code&redirect_uri=https%3A%2F%2Fpremium.liege.dev&scope=email+identify+guilds");
            }
            // else do discord oauth
            else
            {
                // oauth data for discord
                var oauthData = new Dictionary<string, string>
                {
                    { "client_id", "879360985738117120" },
                    { "client_secret", "u7b_QJ4PWne6AvC0wtOFg04sO_1XPI38" },
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", "https://premium.liege.dev" },
                };

                // encode content and post to discord oauth2
                var content = new FormUrlEncodedContent(oauthData);
                var result = await _client.PostAsync("https://discord.com/api/v10/oauth2/token", content);

                // if post request is successful, get user data
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        // do discord oauth2 stuff
                        _client.CancelPendingRequests();
                        var postData = JsonNode.Parse(await result.Content.ReadAsStringAsync());
                        var token = postData["access_token"];
                        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                        var getResult = await _client.GetAsync("https://discord.com/api/v10/users/@me");

                        var getData = JsonNode.Parse(await getResult.Content.ReadAsStringAsync());
                        var guilds = await DiscordExtensions.GetPermissionUserGuildsAsync(_client);
                        var userid = Convert.ToUInt64(getData["id"].ToString());

                        //StripeConfiguration.ApiKey = "sk_test_51ORN0JDSx6HgmfF4ZC2zrN2dLCfsx8JHklRa8AXJeOn6MLpqNsP2AivW67AaX6oaO4qGj1YbPRrysfO3nIlGI4lG00OE1VasdA";
                        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

                        var options = new CustomerCreateOptions
                        {
                            Name = getData["username"].ToString(),
                            Description = $"{userid}",
                            Email = getData["email"].ToString(),
                        };

                        var service = new CustomerService();
                        var cus = service.Create(options);
                        var prodid = Environment.GetEnvironmentVariable("STRIPE_PRODUCT_ID");

                        var link = CheckoutExtensions.CreateCheckoutSessionLink(cus, prodid, guilds, userid);

                        return Redirect(link);
                    }
                    catch
                    {
                        return BadRequest("Discord OAuth failed! (1)");
                    }
                }
                else
                {
                    return BadRequest("Discord OAuth failed! (2)");
                }
            }
        }
    }
}