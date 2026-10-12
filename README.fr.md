<p align="center"><img src="assets/afk-realm.png" width="160" alt="AFK Realm logo"></p>

<p align="center"><a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <b>Français</b> · <a href="README.es.md">Español</a> · <a href="README.pt-BR.md">Português (Brasil)</a> · <a href="README.ru.md">Русский</a> · <a href="README.zh-Hans.md">简体中文</a></p>

# AFK Realm

*Passez AFK, revenez à votre propre serveur.*

> [!WARNING]
> **En cours de développement et très expérimental.** AFK Realm n’est pas terminé et change sans cesse. Chaque version et chaque nouvelle fonction est à considérer comme expérimentale. Gardez des sauvegardes de ce qui compte pour vous (AFK Realm en fait une avant chaque mise à jour et chaque changement de module) et signalez les problèmes avec `logs\install.log`.

Compilez, lancez et peaufinez votre propre serveur **Conquest of Azeroth** (un fork d’AzerothCore avec Playerbots) sous Windows 10/11 – compilé directement depuis les sources, le tout en quelques clics.

![](docs/screenshots/8-server-management.png)

Ceci est une version courte. La description complète se trouve dans le [README anglais](README.md), le guide d’utilisation dans [docs/MANUAL.md](docs/MANUAL.md) (en anglais). Le programme lui-même est en français : il démarre dans la langue de Windows, et la langue se change en haut à droite.

## Ce que fait AFK Realm

- **Installation guidée** : choisissez un dossier et un mot de passe de base de données, cliquez sur *Installer* – AFK Realm récupère lui-même Git, CMake, les Build Tools de Visual Studio, OpenSSL, Boost, MySQL et Python
- **Toujours à jour** : chaque installation compile le dernier noyau CoA et Playerbots ; mises à jour avec sauvegarde préalable
- **Le serveur en main** : démarrer, arrêter, redémarrer, redémarrages planifiés, les deux consoles du serveur dans une seule fenêtre
- **Réglages** de tous les fichiers de configuration, avec recherche, description et valeur par défaut
- **Bots, comptes et outils de MJ** : vue d’ensemble des bots aléatoires, création et transfert de comptes, réparation de quêtes, aide aux personnages
- **Modules** ajoutés et retirés en un clic, avec leurs modifications de la base de données – dont World Journey (le monde comme un seul voyage de 1 à 60), les bots de l’hôtel des ventes, des races supplémentaires et les classes classiques
- **Jouer avec d’autres** : adresse du royaume, add-ons et patch client en ZIP pour vos joueurs
- **Dans votre langue** : français, anglais, allemand, espagnol, portugais (Brésil), russe et chinois simplifié ; d’autres langues peuvent s’ajouter sous forme de fichier

## Démarrer

1. Téléchargez `AFK-Realm.exe` depuis la page [Releases](../../releases) et lancez-le. Windows SmartScreen peut avertir d’un éditeur inconnu, car l’exe n’est pas signé ; il est compilé par GitHub Actions depuis ce dépôt.
2. **Installer un nouveau serveur** → choisissez un dossier, p. ex. `C:\CoA-Server` → définissez un mot de passe de base de données → **Installer**. Cela peut durer longtemps et demande environ 40 Go d’espace disque.
3. Dans la gestion du serveur : *Créer les données de carte depuis le client du jeu* (une fois), *Créer un compte*, *Démarrer le serveur*.
4. Mettez `set realmlist 127.0.0.1` dans le `realmlist.wtf` de votre client et connectez-vous.

Le client de jeu CoA, vous l’apportez vous-même ; il ne fait pas partie de ce projet.

## Plus

- [Guide d’utilisation](docs/MANUAL.md) – chaque fenêtre expliquée (en anglais)
- [Changements](CHANGELOG.md) et [notes de version](RELEASE_NOTES.md) (en anglais)
- Problèmes, idées ou simplement un merci : volontiers en [issue](../../issues), en anglais si possible

## Mentions

Un outil communautaire non officiel, sans lien avec Blizzard Entertainment, Project Ascension, le projet AzerothCore ou les auteurs des forks qu’il compile. World of Warcraft est une marque de Blizzard Entertainment. Licence : MIT (voir [LICENSE](LICENSE)).
