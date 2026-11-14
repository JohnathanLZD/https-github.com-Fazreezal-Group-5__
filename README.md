# CA2 Dam Valve Monitor (built from the working CMD console)

This is the same MQTT subscription flow that worked in `CA2_Dam_MQTT_Console_Test`, with a basic local dark-mode web page added. There is **no database, Blazor, MudBlazor, or other frontend framework**.

## Run

1. Extract the ZIP on your Windows computer.
2. Open `CA2.slnx` with Visual Studio 2022 (with .NET 10 SDK and ASP.NET Core support) and run **CA2**. Your browser opens automatically.
3. Alternatively, double-click `Run_CA2_Dashboard.bat`, then manually open **http://localhost:5188**.
4. Keep the application running. The console prints `[CONNECTED]`, `[SUBSCRIBE RESULT]`, and incoming MQTT messages.
5. If the page says `MQTT disconnected`, look at the console for `[MQTT ERROR]`. Your computer must be connected to the same network that let your earlier console test reach the MQTT broker.

## Contents of the dashboard

- **Sensor (DI2)**: red lamp when `sensor_pnp` is `1`; otherwise dark.
- **Button (DI1)**: green lamp when `confirm_button` is `1`; otherwise dark.
- **Water volume**: animated tank and the original numeric `water_volume_m3`, displayed on a 0–1,000,000 m3 visual scale, threshold 700,000 m3.
- **Messages**: `system_status`, `confirmation_status`, `confirmation_message`, `warning_message`, and `maintenance_message`.
- **Connection**: a simple MQTT status and last message time.

The reading is updated every second in the browser. On the PLC, its existing MQTT publishing interval determines how often the values actually change. Very short button presses may occur between PLC messages and be missed.

## MQTT connection

- Same MQTTnet package/version as your working console project: MQTTnet `5.2.0.1603`.
- Same broker, username, password and topic as the successful `CA2_Dam_MQTT_Console_Test` app.
- Connection settings are at the top of `CA2/Program.cs`. **Keep this project private** while it contains credentials.
- Topic `beaver-iot/mqtt@default/mqtt-device/plc047`.

The app does not recalculate PLC volume or change your requested `/ 10.0` CODESYS scaling. Values outside the 0–1,000,000 m3 display scale are shown numerically, with the tank visually capped.

## How to check it's working

1. Open the terminal: check for `[CONNECTED] MQTT broker connection successful.` and subscription result.
2. Wait for `[HH:mm:ss] MESSAGE #1 ...` from the PLC.
3. In the browser, you should see `MQTT connected`, two lamps, water level, and the exact PLC message strings.
4. If the console receives messages but the dashboard stays empty, open `http://localhost:5188/api/reading`. It should display the latest JSON reading.

No physical valve is operated by this read-only dashboard.
