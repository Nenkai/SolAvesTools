# SolAvesTools

Sets of tools/libraries for dealing with the SOL-AVES (or Phoenix) Engine created by Bandai Namco Studios.

This engine is used in:

* Kirby Air Riders (NS2) (Internally: `Wind`)

## Usage

* `extract-all -i <folder>` - Extract all files out of a folder containing `.cat/.dat` files.
* `list-files  -i <folder>` - Lists all files in a folder containing `.cat/.dat` files.
* `extract-movies -i <path to C93975A2MV.bin>` - Extracts all movies (webm) out of a movie container.
* `crypt-config -i <path to config.bin/config.json>` - Decrypts or encrypts the config file.
* `crypt-startup -i <path to startup.dat/startup.json>` - Decrypts or encrypts the startup file.

## Engine Reverse-Engineering Notes

## Assets

The vast majority of the engine's and game's strings are hashed with a custom variant of XXH3 by design. Paths, strings, everything. Please refer to this [CEDEC lecture](https://cedil.cesa.or.jp/cedil_sessions/view/3389).

This makes navigating the game files very tedious. Bear that in mind.

The vast majority of the extracted game assets makes uses of FlatBuffers.

## PC Compatibility

The game has a clear design in being multiplatform. Many PC features and editor features are referenced within the config file and command line arguments.

## Glossary

* `Aurora` - Renderer?
* `Nitro` - Asset Build System
* `Sygnus` - Sound Engine, superseeding NuSound
* `Ruby` - ? - Text or GUI related.

## TODO

* [ ] Sound file decryption.
* [ ] Font file decryption.
* [ ] Save file decryption.
* [ ] Release path hashes.

## Building & Licensing

MIT, however AI usage is unwelcome.

## Credits

* mvit, for doing the bulk of the initial reversing of the hash algorithm, and hash finding.
