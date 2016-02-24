# LessUACRunner

Launch windows application without UAC prompt and with admin privileges.

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->

- [About](#about)
- [Usage](#usage)
- [Download](#download)
- [Changelog](#changelog)
- [License](#license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## About

This application works by passing via a named pipe an application name to a Windows service that launches this GUI / console application into the current user's session.

Tested on :
* Windows XP Pro SP3 32-bits
* Windows Vista Pro SP1 64-bits
* Windows 7 Pro SP1 64-bits
* Windows 7 Ultimate 32-bits
* Windows 7 Ultimate 64-bits
* Windows 8 Pro 64-bits
* Windows 8.1 Pro 64-bits
* Windows 10 Pro 64-bits

## Usage

```
Usage: LessUACRunnerConsole.exe [OPTION|SHORTCUT]

Basic privileges:
  -help    : Get help
  -version : Get version infos
  -status  : Service status
  -list    : List of shortcuts (allowed applications)
  shortcut : App to execute (must be added in App.config via -configa)
             examples :
               LessUACRunnerConsole.exe shortcut
               LessUACRunnerConsole.exe shortcut "file arguments"

Administrator privileges:
  -configa   : Add shortcut, file path and arguments in App.config
               args     : [shortcut] app_path [app_args] [-console]
                 app_path : path to the executable application
                 app_args : arguments for your application
                 -console : is console application
               examples :
                 -configa app_path
                 -configa shortcut app_path
                 -configa app_path app_args
                 -configa shortcut app_path app_args
  -configd   : Delete file path (and arguments) from App.config
               args : shortcut
  -start     : Start the service
  -stop      : Stop the service
  -restart   : Stop and Start the service
  -install   : Install service
  -uninstall : Uninstall service
  -encrypt   : Encrypt App.config
  -decrypt   : Decrypt App.config
```

## Download

[![LessUACRunner 1.0.0.2 download](https://img.shields.io/badge/download-LessUACRunner%201.0.0.2-brightgreen.svg)](https://github.com/crazy-max/LessUACRunner/releases/download/v1.0.0.2/LessUACRunner-1.0.0.2.exe)

## Changelog

See ``CHANGELOG.md``.

## License

LGPL. See ``LICENSE`` for more details.<br />
Icon from [OmmoZoubayr](http://www.ommozoubayr.com/).
