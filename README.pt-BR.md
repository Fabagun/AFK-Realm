<p align="center"><img src="assets/afk-realm.png" width="160" alt="AFK Realm logo"></p>

<p align="center"><a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <b>Português (Brasil)</b> · <a href="README.ru.md">Русский</a> · <a href="README.zh-Hans.md">简体中文</a></p>

# AFK Realm

*Fique AFK, volte para o seu próprio servidor.*

> [!WARNING]
> **Em desenvolvimento e muito experimental.** O AFK Realm não está pronto e muda o tempo todo. Toda versão e toda função nova devem ser vistas como experimentais. Mantenha backups do que importa para você (o AFK Realm faz um antes de cada atualização e de cada mudança de módulos) e relate problemas anexando `logs\install.log`.

Compile, rode e ajuste o seu próprio servidor de **Conquest of Azeroth** (um fork do AzerothCore com Playerbots) no Windows 10/11, compilado direto do código-fonte, tudo com poucos cliques.

![](docs/screenshots/8-server-management.png)

Esta é uma versão curta. A descrição completa está no [README em inglês](README.md) e o guia de uso em [docs/MANUAL.md](docs/MANUAL.md) (em inglês). O programa em si está em português: ele inicia no idioma do Windows, e o idioma pode ser trocado no canto superior direito.

## O que o AFK Realm faz

- **Instalação guiada**: escolha uma pasta e uma senha de banco de dados e clique em *Instalar* – o AFK Realm baixa sozinho Git, CMake, Build Tools do Visual Studio, OpenSSL, Boost, MySQL e Python
- **Sempre atualizado**: toda instalação compila o núcleo do CoA e o Playerbots mais recentes; atualizações com backup antes
- **O servidor sob controle**: iniciar, parar, reiniciar, reinícios agendados e os dois consoles do servidor numa só janela
- **Configurações** de todos os arquivos de configuração, com busca, descrição e valor padrão
- **Bots, contas e ferramentas de GM**: visão geral dos bots aleatórios, criar e transferir contas, consertar missões, ajudar personagens
- **Módulos** adicionados e removidos com um clique, junto com as mudanças no banco de dados – entre eles World Journey (o mundo como uma só jornada do 1 ao 60), bots da casa de leilões, raças novas e classes clássicas
- **Jogar com outros**: endereço do reino, add-ons e patch de cliente num ZIP para seus jogadores
- **No seu idioma**: português (Brasil), inglês, alemão, francês, espanhol, russo e chinês simplificado; mais idiomas podem ser adicionados como arquivo

## Começando

1. Baixe o `AFK-Realm.exe` na página de [Releases](../../releases) e execute. O Windows SmartScreen pode avisar sobre um editor desconhecido porque o exe não é assinado; ele é compilado pelo GitHub Actions a partir deste repositório.
2. **Instalar um servidor novo** → escolha uma pasta, por ex. `C:\CoA-Server` → defina uma senha de banco de dados → **Instalar**. Pode demorar bastante e precisa de cerca de 40 GB de espaço.
3. No gerenciamento do servidor: *Criar dados de mapa a partir do cliente do jogo* (uma vez), *Criar conta*, *Iniciar servidor*.
4. Coloque `set realmlist 127.0.0.1` no `realmlist.wtf` do seu cliente e entre.

O cliente do jogo do CoA é por sua conta; ele não faz parte deste projeto.

## Mais

- [Guia de uso](docs/MANUAL.md) – cada janela explicada (em inglês)
- [Mudanças](CHANGELOG.md) e [notas da versão](RELEASE_NOTES.md) (em inglês)
- Problemas, ideias ou só um obrigado: fique à vontade para abrir uma [issue](../../issues), de preferência em inglês

## Aviso

Uma ferramenta não oficial da comunidade, sem ligação com a Blizzard Entertainment, o Project Ascension, o projeto AzerothCore ou os autores dos forks que ela compila. World of Warcraft é uma marca da Blizzard Entertainment. Licença: MIT (veja [LICENSE](LICENSE)).
