# TCP Message Controller (MAUI)

A lightweight .NET MAUI application designed to send TCP commands to remote devices. It allows users to create a custom dashboard of buttons, each configured with a specific IP address, port, and payload.

---

### Key Features

* **Dynamic Command Creation**: Add, edit, and delete custom TCP command buttons.
* **Visual Feedback**: Buttons change color (Green/Red) based on the success or failure of the TCP transmission.
* **Persistence**: Your configured commands are saved locally using `Preferences`.
* **Cross-Platform**: Built with .NET MAUI for compatibility across Android, iOS, and Windows.
* **Asynchronous Communication**: Non-blocking TCP requests with a 3-second timeout.

---

### Technical Specifications

* **Framework**: .NET MAUI
* **Protocol**: TCP (Transmission Control Protocol)
* **Data Format**: UTF8-encoded strings (with `\n` terminator)
* **Storage**: JSON serialization via `System.Text.Json`

---

### How It Works

1.  **Add a Command**: Open the input panel and specify a Label, IP Address, Port, and the Command string.
2.  **Send**: Tap the generated button. The app attempts a socket connection and sends the data.
3.  **Manage**: Use the edit (✎) or delete (X) buttons to modify your dashboard on the fly.

---

### ⚠️ Development Note (Work in Progress)

**Current Status: Debug/Beta**

This project is currently in a "draft" state and is primarily intended for use in a **debug environment**. While the core TCP functionality is operational, please consider the following before using it in a production scenario:

* **Error Handling**: The current implementation provides basic visual feedback (color changes), but lacks exhaustive exception handling for complex network edge cases.
* **Connection Stability**: The TCP client uses a fixed 3-second timeout. Depending on your network infrastructure, this may require fine-tuning to prevent premature connection drops.
* **Security**: No encryption or authentication protocols are currently implemented for the TCP stream.
* **Optimization**: The UI management (dynamic grid generation) is functional but may require further optimization for very large sets of command buttons.

**Fine-tuning is highly recommended** before deploying this application for critical production tasks.

---

### Installation

1.  Clone the repository.
2.  Open the solution in **Visual Studio 2022** (with MAUI workload installed).
3.  Restore NuGet packages.
4.  Build and Run on your preferred target (Android, iOS, or Windows).

---

### Usage Example
Perfect for controlling IoT devices, ESP32/Arduino servers, or remote terminal applications that listen for simple TCP string commands.
