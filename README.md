# SupportBot

SupportBot is a Level 1 support chatbot built using ChatGPT and WinUI. It helps users resolve common IT issues through a conversational interface.

## 🚀 Features

- ChatGPT-powered responses for basic IT support
- WinUI-based desktop interface
- Handles common L1 tasks like:
  - Password resets
  - Login issues
  - Software installation guidance
- Escalation logic for L2/L3 support

## 🛠️ Technologies

- WinUI 3
- [OpenAI GPT API](https://openai.com/api/)

## 📦 Installation

1. Clone the repo:
```bash
git clone https://github.com/TadeasBurda/supprot-bot.git
```
2. Open the solution in Visual Studio.
3. Add your OpenAI API key to:
 ```bash 
setx OPENAI_API_KEY "your_api_key_here"
```
4. Build and run the project.

## 🧠 How it works

- The chatbot uses GPT to interpret user queries and respond with helpful suggestions. It includes a fallback mechanism to escalate tickets if the issue is too complex.

## 📄 License

- MIT

## 🤝 Contributing

- Pull requests are welcome! For major changes, please open an issue first to discuss what you would like to change.