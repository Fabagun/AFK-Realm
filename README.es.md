<p align="center"><img src="assets/afk-realm.png" width="160" alt="AFK Realm logo"></p>

<p align="center"><a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <b>Español</b> · <a href="README.pt-BR.md">Português (Brasil)</a> · <a href="README.ru.md">Русский</a> · <a href="README.zh-Hans.md">简体中文</a></p>

# AFK Realm

*Ponte AFK, vuelve a tu propio servidor.*

> [!WARNING]
> **En desarrollo y muy experimental.** AFK Realm no está terminado y cambia continuamente. Cada versión y cada función nueva debe considerarse experimental. Guarda copias de seguridad de lo que te importe (AFK Realm hace una antes de cada actualización y cada cambio de módulos) e informa de los problemas adjuntando `logs\install.log`.

Compila, ejecuta y ajusta tu propio servidor de **Conquest of Azeroth** (un fork de AzerothCore con Playerbots) en Windows 10/11, compilado directamente desde el código fuente y todo con unos pocos clics.

![](docs/screenshots/8-server-management.png)

Esta es una versión breve. La descripción completa está en el [README en inglés](README.md) y la guía de uso en [docs/MANUAL.md](docs/MANUAL.md) (en inglés). El programa en sí está en español: arranca en el idioma de Windows y el idioma se cambia arriba a la derecha.

## Qué hace AFK Realm

- **Instalación guiada**: elige una carpeta y una contraseña de base de datos y haz clic en *Instalar*; AFK Realm consigue por sí mismo Git, CMake, las Build Tools de Visual Studio, OpenSSL, Boost, MySQL y Python
- **Siempre al día**: cada instalación compila el núcleo de CoA y Playerbots más recientes; actualizaciones con copia de seguridad previa
- **El servidor bajo control**: iniciar, detener, reiniciar, reinicios programados y las dos consolas del servidor en una sola ventana
- **Configuración** de todos los archivos de configuración, con búsqueda, descripción y valor predeterminado
- **Bots, cuentas y herramientas de MJ**: resumen de los bots aleatorios, crear y transferir cuentas, reparar misiones, ayudar a personajes
- **Módulos** que se añaden y se quitan con un clic, junto con sus cambios en la base de datos; entre ellos World Journey (el mundo como un solo viaje de 1 a 60), bots de la casa de subastas, razas nuevas y clases clásicas
- **Jugar con otros**: dirección del reino, add-ons y parche de cliente en un ZIP para tus jugadores
- **En tu idioma**: español, inglés, alemán, francés, portugués (Brasil), ruso y chino simplificado; se pueden añadir más idiomas como archivo

## Primeros pasos

1. Descarga `AFK-Realm.exe` de la página de [Releases](../../releases) y ejecútalo. Windows SmartScreen puede avisar de un editor desconocido porque el exe no está firmado; lo compila GitHub Actions a partir de este repositorio.
2. **Instalar un servidor nuevo** → elige una carpeta, p. ej. `C:\CoA-Server` → define una contraseña de base de datos → **Instalar**. Puede tardar mucho y necesita unos 40 GB de espacio.
3. En la gestión del servidor: *Crear datos de mapa desde el cliente del juego* (una vez), *Crear cuenta*, *Iniciar servidor*.
4. Pon `set realmlist 127.0.0.1` en el `realmlist.wtf` de tu cliente e inicia sesión.

El cliente del juego de CoA lo pones tú; no forma parte de este proyecto.

## Más

- [Guía de uso](docs/MANUAL.md): cada ventana explicada (en inglés)
- [Cambios](CHANGELOG.md) y [notas de la versión](RELEASE_NOTES.md) (en inglés)
- Problemas, ideas o simplemente las gracias: con gusto como [issue](../../issues), en inglés si puedes

## Aviso

Una herramienta comunitaria no oficial, sin relación con Blizzard Entertainment, Project Ascension, el proyecto AzerothCore ni los autores de los forks que compila. World of Warcraft es una marca de Blizzard Entertainment. Licencia: MIT (ver [LICENSE](LICENSE)).
