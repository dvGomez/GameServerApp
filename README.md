<div align="center">

# 🎮 GameServerApp

**Gerencie seus servidores de jogo de forma simples, centralizada e sem complicação.**

Crie, configure, monitore e controle múltiplos servidores em uma única interface —<br>
com console em tempo real, edição de arquivos e configurações, tudo sem precisar tocar no terminal.

[![Release](https://img.shields.io/github/v/release/dvGomez/GameServerApp?style=for-the-badge&color=0EA5E9)](https://github.com/dvGomez/GameServerApp/releases)
[![License](https://img.shields.io/github/license/dvGomez/GameServerApp?style=for-the-badge&color=22C55E)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-888888?style=for-the-badge)](https://github.com/dvGomez/GameServerApp/releases)

</div>

---

## ✨ Features

- 🖥️ **Interface desktop moderna** — Gerencie tudo visualmente, sem precisar de terminal
- ⚡ **Console em tempo real** — Veja logs, envie comandos e acompanhe o status do servidor ao vivo
- 📝 **Edição de configurações** — Altere porta, modo de jogo, dificuldade e mais com formulários intuitivos
- 📁 **Navegador de arquivos** — Explore e edite arquivos do servidor direto pela interface
- 🔄 **Atualizações reativas** — Qualquer alteração reflete automaticamente em toda a UI
- 📋 **Log persistente** — Troque entre servidores sem perder os logs do console
- 🧩 **Sistema de plugins** — Extensível para qualquer jogo (Minecraft, PaperMC, FiveM e Project Zomboid inclusos)
- 🚀 **Setup automático** — Download do servidor e dependências (Java, SteamCMD, FXServer) gerenciados automaticamente
- 🌍 **Cross-platform** — Funciona em Windows, Linux e macOS

---

## 📦 Instalação

### Download (recomendado)

1. Acesse a [página de releases](https://github.com/dvGomez/GameServerApp/releases)
2. Baixe o arquivo `.zip` correspondente ao seu sistema operacional:
   | Sistema | Arquivo |
   |---------|---------|
   | Windows | `GameServerApp-windows-x64.zip` |
   | Linux   | `GameServerApp-linux-x64.zip` |
   | macOS   | `GameServerApp-macos-x64.zip` |
3. Extraia o conteúdo do `.zip`
4. Execute o aplicativo:
   - **Windows:** execute `GameServerApp.UI.exe`
   - **Linux/macOS:** execute `./GameServerApp.UI` (pode ser necessário dar permissão com `chmod +x GameServerApp.UI`)

> **Nota:** O executável é **self-contained** — não é necessário instalar .NET ou Java. O Java é baixado automaticamente na criação do primeiro servidor.

### Build a partir do código fonte

Caso prefira compilar manualmente:

```bash
# Pré-requisito: .NET 9 SDK
# https://dotnet.microsoft.com/download/dotnet/9.0

# Clone o repositório
git clone https://github.com/dvGomez/GameServerApp.git
cd GameServerApp

# Execute em modo desenvolvimento
dotnet run --project src/GameServerApp.UI

# Ou gere um build de produção
dotnet publish src/GameServerApp.UI/GameServerApp.UI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish
```

---

## 🚀 Primeiros passos

1. Abra o GameServerApp
2. Clique em **"+ New Server"** na sidebar
3. Selecione o jogo (ex: Minecraft Java Edition)
4. Dê um nome ao servidor e aguarde o download automático
5. Clique em **"Start"** e pronto — seu servidor está rodando!

A partir daí você pode:
- Enviar comandos pelo console integrado
- Alterar configurações em **Settings**
- Editar arquivos do servidor na aba **Files**
- Acompanhar jogadores online em tempo real

---

## 🧩 Jogos suportados

| Jogo | Plugin | Status |
|------|--------|--------|
| Minecraft Java Edition | `GameServerApp.Plugins.Minecraft` | ✅ Disponível |
| Minecraft PaperMC | `GameServerApp.Plugins.PaperMC` | ✅ Disponível |
| FiveM (GTA V) | `GameServerApp.Plugins.FiveM` | ✅ Disponível |
| Project Zomboid | `GameServerApp.Plugins.Zomboid` | ✅ Disponível |
| *Seu jogo favorito* | Crie um plugin! | 🔧 Extensível |

### Criando um plugin

Para adicionar suporte a um novo jogo, implemente a interface `IGameServerPlugin`:

```csharp
public interface IGameServerPlugin
{
    string GameId { get; }
    string DisplayName { get; }

    // Schema de configuração (define os campos que aparecem na UI)
    IReadOnlyList<ConfigField> GetConfigSchema();

    // Como iniciar e parar o servidor
    ProcessStartInfo BuildStartInfo(ServerConfig config);
    string? GetGracefulStopCommand();

    // Download e versionamento do servidor
    Task<string> GetLatestVersionAsync(CancellationToken ct = default);
    Task DownloadServerAsync(string version, string targetDirectory, ...);

    // Leitura e escrita da configuração do jogo
    Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues, ...);
    Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory, ...);
}
```

Basta criar um novo projeto, implementar a interface e registrá-lo no DI container do `App.axaml.cs`.

---

## 🏗️ Arquitetura

```
GameServerApp/
├── src/
│   ├── GameServerApp.Core/          # Lógica de negócio, interfaces e modelos
│   │   ├── Interfaces/              # IServerManager, IGameServerPlugin, etc.
│   │   ├── Models/                  # ServerConfig, ServerInstance, etc.
│   │   ├── Services/                # ServerManager, ProcessManager
│   │   └── Events/                  # Eventos de estado, config, console
│   │
│   ├── GameServerApp.UI/            # Interface desktop (Avalonia UI + MVVM)
│   │   ├── ViewModels/              # MainWindow, Console, Config, etc.
│   │   ├── Views/                   # XAML das telas
│   │   └── Converters/              # Conversores de dados para UI
│   │
│   ├── GameServerApp.Plugins.Minecraft/  # Plugin do Minecraft Java
│   │   ├── MinecraftPlugin.cs        # Implementação do IGameServerPlugin
│   │   ├── JavaManager.cs            # Download e gerenciamento do Java
│   │   └── MinecraftServerProperties.cs  # Parser do server.properties
│   │
│   ├── GameServerApp.Plugins.PaperMC/    # Plugin do PaperMC
│   │   └── PaperPlugin.cs            # PaperMC com suporte a plugins Bukkit/Spigot
│   │
│   ├── GameServerApp.Plugins.FiveM/      # Plugin do FiveM (GTA V)
│   │   ├── FiveMPlugin.cs             # Download via cfx.re, config server.cfg
│   │   ├── FiveMConsoleParser.cs       # Parser de logs do FXServer
│   │   └── FiveMServerConfig.cs        # Leitura/escrita do server.cfg
│   │
│   └── GameServerApp.Plugins.Zomboid/    # Plugin do Project Zomboid
│       ├── ZomboidPlugin.cs            # Download via SteamCMD, suporte Steam/Non-Steam
│       ├── ZomboidConsoleParser.cs      # Parser de logs do PZ server
│       └── ZomboidIniConfig.cs          # Leitura/escrita do .ini
```

O projeto segue o padrão **MVVM** com separação clara entre Core (lógica), UI (interface) e Plugins (jogos).

---

## 🤝 Contribuindo

Contribuições são bem-vindas! Sinta-se livre para:

1. Fazer um **fork** do repositório
2. Criar uma **branch** para sua feature (`git checkout -b feature/minha-feature`)
3. Fazer **commit** das suas alterações (`git commit -m 'feat: minha feature'`)
4. Fazer **push** para a branch (`git push origin feature/minha-feature`)
5. Abrir um **Pull Request**

### Ideias para contribuir

- 🎮 Plugins para novos jogos (Terraria, Factorio, Valheim, CS2, Rust, etc.)
- 🌐 Suporte a i18n (internacionalização)
- 📊 Dashboard com métricas de uso (CPU, RAM, uptime)
- 🔔 Sistema de notificações
- 🎨 Temas personalizáveis

---

## 📄 Licença

Este projeto é distribuído sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE) para mais detalhes.

---

<div align="center">

Feito com ❤️ por [dvGomez](https://github.com/dvGomez)

</div>
