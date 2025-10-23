# 🧐 ETWPilot — Windows ETW + AI

**ETWPilot** is a modern toolkit that bridges **Windows Event Tracing for Windows (ETW)** with **AI-powered analysis**.  
It enables real-time inspection, collection, and intelligent correlation of ETW telemetry for system monitoring, debugging, and behavioral modeling.

## ✨ Features
- 🔍 **ETW Capture & Subscription:** Subscribe to key Windows providers (Kernel, Network, TLS, DNS, etc.)
- ⚙️ **AI-Assisted Log Analysis:** Use local or remote LLMs (e.g., Ollama, OpenAI, or Azure) for summarization and anomaly detection
- 📈 **Structured Data Models:** Built-in schemas for ETW event normalization
- 🧩 **Extensible Architecture:** Add new event parsers, AI backends, and visualization modules
- 🫰 **WPF UI:** Desktop interface for visual exploration of event streams

## 🧰 Architecture Overview
```
+----------------------------+
|  Windows ETW Providers     |
+------------+---------------+
             |
             v
+----------------------------+
|  ETWPilot Collector Layer  |
|  - EventParser             |
|  - Model/OllamaConfigModel |
+------------+---------------+
             |
             v
+----------------------------+
|  AI Processing Layer       |
|  - LLM integration         |
|  - Context summarization   |
+------------+---------------+
             |
             v
+----------------------------+
|  UI + Settings + Controls  |
+----------------------------+
```

## ⚡️ Quick Start
```bash
# Clone
git clone https://github.com/yew011/etwpilot.git
cd etwpilot

# Build
dotnet build EtwPilot.sln

# Run (from Visual Studio or CLI)
dotnet run --project EtwPilot
```

## 🧩 Configuration
The sample configuration file is under:
```
Samples/Settings/sample-settings.json
```
You can specify:
- `OllamaModelPath` or remote endpoint URL  
- Enabled ETW providers  
- Output log directory  

## 🧐 AI Integration
Supported AI integrations:
- 🧉 **Ollama** (local models, e.g. Mistral, Llama 3, Qwen)
- ☁️ **OpenAI / Azure OpenAI** (remote inference)
- 🔧 Configurable via `Model/OllamaConfigModel.cs`

## 🎜 License
Apache 2.0 — see [`LICENSE`](./LICENSE)

## 🤝 Contributing
PRs welcome!  
Please fork the repo, create a feature branch, and open a pull request.
