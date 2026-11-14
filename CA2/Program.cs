using MQTTnet;
using System.Globalization;
using System.Text.Json;

// CA2 Dam Valve Monitor
// The MQTT connection below follows the SAME approach and settings as the
// working CA2_Dam_MQTT_Console_Test program. This project simply adds a page.

const string broker = "192.168.101.59";
const int port = 1883;
const string username = "mqtt@default";
const string password = "12345678";
const string topic = "beaver-iot/mqtt@default/mqtt-device/plc047";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5188");
var app = builder.Build();

// Store the latest valid PLC message for the dashboard to read.
var gate = new object();
var reading = new PlcReading();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/reading", () =>
{
    lock (gate)
        return Results.Json(reading);
});

// Original console MQTTnet setup: ConnectAsync + SubscribeAsync + message event.
var mqttFactory = new MqttClientFactory();
using var mqttClient = mqttFactory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(broker, port)
    .WithClientId("ca2-web-" + Guid.NewGuid())
    .WithCredentials(username, password)
    .Build();

mqttClient.ApplicationMessageReceivedAsync += e =>
{
    string payload = e.ApplicationMessage.ConvertPayloadToString();
    try
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a JSON object from the PLC.");

        lock (gate)
        {
            reading = reading with
            {
                DeviceId = Text(root, "device_id") ?? "plc047",
                SensorPnp = Integer(root, "sensor_pnp"),
                ConfirmButton = Integer(root, "confirm_button"),
                AnalogReading = Number(root, "voltage"),
                WaterVolumeM3 = Number(root, "water_volume_m3"),
                ConfirmationMessage = Text(root, "confirmation_message"),
                ConfirmationStatus = Text(root, "confirmation_status"),
                SystemStatus = Text(root, "system_status"),
                WarningMessage = Text(root, "warning_message"),
                MaintenanceMessage = Text(root, "maintenance_message"),
                LastUpdated = DateTimeOffset.Now,
                MessagesReceived = reading.MessagesReceived + 1,
                Error = null
            };
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] MESSAGE #{reading.MessagesReceived} " +
                $"Sensor={reading.SensorPnp} Button={reading.ConfirmButton} " +
                $"Volume={reading.WaterVolumeM3} Status={reading.SystemStatus}");
        }
    }
    catch (JsonException ex)
    {
        Console.WriteLine("[INVALID JSON] " + ex.Message);
        lock (gate) reading = reading with { Error = "Invalid PLC JSON received" };
    }
    return Task.CompletedTask;
};

mqttClient.DisconnectedAsync += _ =>
{
    Console.WriteLine("[MQTT DISCONNECTED] Trying to reconnect...");
    lock (gate) reading = reading with { Connected = false };
    return Task.CompletedTask;
};

Console.Title = "CA2 Dam Valve Monitor";
Console.WriteLine("=== CA2 Dam Valve Monitor ===");
Console.WriteLine($"MQTT broker: {broker}:{port}");
Console.WriteLine($"MQTT topic : {topic}");
Console.WriteLine("Dashboard  : http://localhost:5188");
Console.WriteLine("Press Ctrl+C to stop.\n");

// Keep the same console connection flow, and reconnect if Wi-Fi/MQTT drops.
async Task ListenForMqtt(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            if (!mqttClient.IsConnected)
            {
                Console.WriteLine("Connecting to MQTT broker...");
                await mqttClient.ConnectAsync(options, cancellationToken);

                var subOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build();
                var result = await mqttClient.SubscribeAsync(subOptions, cancellationToken);
                Console.WriteLine("[CONNECTED] MQTT broker connection successful.");
                Console.WriteLine("[SUBSCRIBE RESULT] " +
                    string.Join(", ", result.Items.Select(item => item.ResultCode.ToString())));

                lock (gate) reading = reading with { Connected = true, Error = null };
            }
            await Task.Delay(2000, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MQTT ERROR] " + ex.Message);
            lock (gate) reading = reading with { Connected = false, Error = ex.Message };
            try { await Task.Delay(5000, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }
}

// Start listening immediately, while the web server runs in the same console.
var mqttTask = ListenForMqtt(app.Lifetime.ApplicationStopping);
try
{
    await app.RunAsync();
}
finally
{
    if (mqttClient.IsConnected)
        await mqttClient.DisconnectAsync();
    await mqttTask;
}

// Read the JSON keys EXACTLY as published by your WAGO PLC.
static string? Text(JsonElement root, string key) =>
    root.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null
        ? value.ToString() : null;

static int? Integer(JsonElement root, string key) =>
    int.TryParse(Text(root, key), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var number) ? number : null;

static double? Number(JsonElement root, string key) =>
    double.TryParse(Text(root, key), NumberStyles.Float,
        CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;

// Only values received in MQTT JSON are displayed; no guessed sensor states.
public sealed record PlcReading
{
    public bool Connected { get; init; }
    public long MessagesReceived { get; init; }
    public string? DeviceId { get; init; }
    public int? SensorPnp { get; init; }
    public int? ConfirmButton { get; init; }
    public double? AnalogReading { get; init; }
    public double? WaterVolumeM3 { get; init; }
    public string? ConfirmationMessage { get; init; }
    public string? ConfirmationStatus { get; init; }
    public string? SystemStatus { get; init; }
    public string? WarningMessage { get; init; }
    public string? MaintenanceMessage { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public string? Error { get; init; }
}
