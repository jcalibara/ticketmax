// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using Refit;
using System.Reflection.Metadata;
using TeamsHook.NET;
using static System.Net.Mime.MediaTypeNames;

const string webhook = "";

string quantity = "1";
string section = "VIP";
string eventid = "b24d4b67-1aaf-4a79-b6fa-610728c04686";

_ = CreateCheckout("test1", "11111111111", "test@test2.com");

_ = CreateCheckout("test2", "11111111111", "test@test3.com");
_ = CreateCheckout("test3", "11111111111", "test@test4.com");

//for (int i = 5; i < 10; i++)
//{
//    Task.Run(() => { _ = CreateCheckout($"test{i}", "11111111111", $"test@test{i}.com"); });
//}

await Task.Delay(-1);

void sendWebhook(string url)
{
    var client = new TeamsHookClient();

    MessageCard card = new MessageCard()
    {
        Title = "Test",
        Summary = "Summary",
        Text = "<h1>This is the text of the card</h1>",
        PotentialAction = new List<MessageAction>()
                {
                    new OpenUriAction()
                    {
                        Name = "Checkout",
                        Targets = new HashSet<Target>()
                        {
                            new Target()
                            {
                                OS = TargetOS.@default,
                                Uri = url
                            }
                        }
                    }
                }
    };

    _ = client.PostAsync(webhook, card);
}

async Task CreateCheckout(string name, string mobile, string email)
{
    var handler = new HttpClientHandler();

    var ticketMaxClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.ticketmax.ph/") };

    var ticketMaxService = RestService.For<ITicketMaxApi>(ticketMaxClient);

    var reserveData = new Dictionary<string, string>
    {
        { "ticket_quantity", quantity },
        { "section_code", section },
        { "event_uuid", eventid }
    };

    var response = await ticketMaxService.ReserveTicket(reserveData);
    var content = await response.Content.ReadAsStringAsync();

    var cart = JsonConvert.DeserializeObject<ReserveResponse>(content);

    var addData = new Dictionary<string, string>
    {
                    { "customer_name", name },
                    { "customer_mobile", mobile },
                    { "customer_email", email },
                    { "event_uuid", eventid},
                    { "ticket_uuids[]", cart.ticket_uuids[0] },
    };

    response = await ticketMaxService.AddTransaction(addData);
    content = await response.Content.ReadAsStringAsync();

    var transaction = JsonConvert.DeserializeObject<AddTransactionResponse>(content);

    var processData = new Dictionary<string, string>
    {
        { "transaction_uuid", transaction.transaction_uuid }
    };

    response = await ticketMaxService.ProcessTransaction(processData);
    content = await response.Content.ReadAsStringAsync();

    var processResponse = JsonConvert.DeserializeObject<ProcessTransactionResponse>(content);

    var paymongoClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.paymongo.com/") };

    var paymongoService = RestService.For<IPaymongoApi>(paymongoClient);

    var payment = new PaymentRequest()
    {
        data = new Data()
        {
            attributes = new Attributes()
            {
                billing = new Billing()
                {
                    email = email,
                    name = name,
                    phone = mobile
                },
                type = "paymaya"
            }
        }
    };

    response = await paymongoService.Payment(payment);
    content = await response.Content.ReadAsStringAsync();

    var paymentResponse = JsonConvert.DeserializeObject<PaymentResponse>(content);

    var attachData = new AttachRequest()
    {
        data = new Data2()
        {
            attributes = new Attributes2()
            {
                client_key = processResponse.client_key,
                payment_method = paymentResponse.data.id,
                return_url = "https://app.ticketmax.ph/paymongo/verify"
            }
        }
    };

    response = await paymongoService.Attach(attachData, processResponse.client_key.Split("_client")[0]);
    content = await response.Content.ReadAsStringAsync();

    var attachResponse = JsonConvert.DeserializeObject<AttachResponse>(content);



    Console.WriteLine(attachResponse.data.attributes.next_action.redirect.url);

    sendWebhook(attachResponse.data.attributes.next_action.redirect.url);


}

[Headers(new string[]
{
"cache-id: 8k0GFYerqapemjpEhza0Zv1odFhD5vGc6J1j5iqr0674Jz8LUAbjgK7rJcajiMdD",
    "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
    "X-Requested-With: XMLHttpRequest",
    "Origin: https://app.ticketmax.ph",
    "Referer: https://app.ticketmax.ph/tickets/b24d4b67-1aaf-4a79-b6fa-610728c04686",
})]
public interface ITicketMaxApi
{
    [Post("/api/v1/tickets/add/guaranteed")]
    Task<HttpResponseMessage> ReserveTicket([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);

    [Post("/api/v1/transactions/add")]
    Task<HttpResponseMessage> AddTransaction([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);

    [Post("/paymongo/process")]
    Task<HttpResponseMessage> ProcessTransaction([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);
}

[Headers(new string[]
{
    "Authorization: Basic cGtfbGl2ZV9rN1AxaDZGUmt2WWJ5WHdlb0VaNFRLSks6",
})]
public interface IPaymongoApi
{
    [Post("/v1/payment_methods")]
    Task<HttpResponseMessage> Payment([Body(BodySerializationMethod.Serialized)] PaymentRequest data);

    [Post("/v1/payment_intents/{key}/attach")]
    Task<HttpResponseMessage> Attach([Body(BodySerializationMethod.Serialized)] AttachRequest data, string key);
}


public class ReserveResponse
{
    public string message { get; set; }
    public int[] ticket_ids { get; set; }
    public string[] ticket_uuids { get; set; }
    public int price { get; set; }
}

public class AddTransactionResponse
{
    public string transaction_uuid { get; set; }
}


public class ProcessTransactionResponse
{
    public string client_key { get; set; }
}


public class PaymentRequest
{
    public Data data { get; set; }
}

public class Data
{
    public Attributes attributes { get; set; }
}

public class Attributes
{
    public string type { get; set; }
    public Billing billing { get; set; }
}

public class Billing
{
    public string name { get; set; }
    public string email { get; set; }
    public string phone { get; set; }
}


public class PaymentResponse
{
    public Data1 data { get; set; }
}

public class Data1
{
    public string id { get; set; }
    public string type { get; set; }
    public Attributes1 attributes { get; set; }
}

public class Attributes1
{
    public bool livemode { get; set; }
    public string type { get; set; }
    public Billing1 billing { get; set; }
    public int created_at { get; set; }
    public int updated_at { get; set; }
    public object details { get; set; }
    public object metadata { get; set; }
}

public class Billing1
{
    public Address address { get; set; }
    public string email { get; set; }
    public string name { get; set; }
    public string phone { get; set; }
}

public class Address
{
    public object city { get; set; }
    public object country { get; set; }
    public object line1 { get; set; }
    public object line2 { get; set; }
    public object postal_code { get; set; }
    public object state { get; set; }
}


public class AttachRequest
{
    public Data2 data { get; set; }
}

public class Data2
{
    public Attributes2 attributes { get; set; }
}

public class Attributes2
{
    public string payment_method { get; set; }
    public string client_key { get; set; }
    public string return_url { get; set; }
}


public class AttachResponse
{
    public Data3 data { get; set; }
}

public class Data3
{
    public string id { get; set; }
    public string type { get; set; }
    public Attributes3 attributes { get; set; }
}

public class Attributes3
{
    public int amount { get; set; }
    public string capture_type { get; set; }
    public string client_key { get; set; }
    public string currency { get; set; }
    public string description { get; set; }
    public bool livemode { get; set; }
    public string statement_descriptor { get; set; }
    public string status { get; set; }
    public object last_payment_error { get; set; }
    public string[] payment_method_allowed { get; set; }
    public Next_Action next_action { get; set; }
    public object payment_method_options { get; set; }
    public Metadata metadata { get; set; }
    public object setup_future_usage { get; set; }
    public int created_at { get; set; }
    public int updated_at { get; set; }
}

public class Next_Action
{
    public string type { get; set; }
    public Redirect redirect { get; set; }
}

public class Redirect
{
    public string url { get; set; }
    public string return_url { get; set; }
}

public class Metadata
{
    public string reference_number { get; set; }
}












