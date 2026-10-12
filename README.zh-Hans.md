<p align="center"><img src="assets/afk-realm.png" width="160" alt="AFK Realm logo"></p>

<p align="center"><a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt-BR.md">Português (Brasil)</a> · <a href="README.ru.md">Русский</a> · <b>简体中文</b></p>

# AFK Realm

*挂机离开，回来就是你自己的服务器。*

> [!WARNING]
> **仍在开发中，高度实验性。** AFK Realm 尚未完成，并且一直在变化。每个版本和每项新功能都应视为实验性的。请备份你在意的一切（AFK Realm 会在每次更新和更改模块前自动备份），遇到问题请附上 `logs\install.log` 反馈。

在 Windows 10/11 上构建、运行并调整你自己的 **Conquest of Azeroth** 服务器（带 Playerbots 的 AzerothCore 分支）——直接从源代码编译，全程点点鼠标即可。

![](docs/screenshots/8-server-management.png)

这是简短版。完整说明见[英文 README](README.md)，用户指南见 [docs/MANUAL.md](docs/MANUAL.md)（英文）。程序本身提供简体中文：它会以 Windows 的语言启动，也可以在右上角切换语言。

## AFK Realm 能做什么

- **引导式安装**：选择文件夹和数据库密码，点击*安装*——Git、CMake、Visual Studio 生成工具、OpenSSL、Boost、MySQL 和 Python 都由 AFK Realm 自动获取
- **始终最新**：每次安装都会编译最新的 CoA 核心和 Playerbots；更新前会先备份
- **掌控服务器**：启动、停止、重启、定时重启，两个服务器控制台合并在一个窗口中
- **设置**：所有配置文件的选项，可搜索，附说明和默认值
- **机器人、账号和游戏管理员工具**：随机机器人概览、创建和转移账号、修复任务、帮助角色
- **模块**一键添加和移除，连同其数据库更改——其中包括 World Journey（整个世界是一段 1 到 60 级的旅程）、拍卖行机器人、新种族和经典职业
- **与他人一起玩**：设置服务器地址，把插件和客户端补丁打包成 ZIP 发给其他玩家
- **你的语言**：简体中文、英语、德语、法语、西班牙语、葡萄牙语（巴西）和俄语；还可以通过文件添加更多语言

## 开始使用

1. 从 [Releases](../../releases) 页面下载 `AFK-Realm.exe` 并运行。由于 exe 没有代码签名，Windows SmartScreen 可能会提示发布者未知；它由 GitHub Actions 从本仓库构建。
2. **安装新服务器** → 选择文件夹，例如 `C:\CoA-Server` → 设置数据库密码 → **安装**。安装可能需要很长时间，并需要约 40 GB 磁盘空间。
3. 在服务器管理中：*从游戏客户端生成地图数据*（仅一次）、*创建账号*、*启动服务器*。
4. 在客户端的 `realmlist.wtf` 中写入 `set realmlist 127.0.0.1` 并登录。

CoA 游戏客户端需要你自备，它不是本项目的一部分。

## 更多

- [用户指南](docs/MANUAL.md)——每个窗口的说明（英文）
- [更改记录](CHANGELOG.md)和[版本说明](RELEASE_NOTES.md)（英文）
- 遇到问题、有想法或只是想说声谢谢：欢迎提交 [issue](../../issues)，最好用英文

## 声明

这是一个非官方的社区工具，与 Blizzard Entertainment、Project Ascension、AzerothCore 项目或其所构建分支的作者均无关联。World of Warcraft 是 Blizzard Entertainment 的商标。许可证：MIT（见 [LICENSE](LICENSE)）。
