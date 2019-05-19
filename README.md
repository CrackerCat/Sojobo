# Sojobo - A binary analysis framework

_Sojobo_ is an emulator for the <a href="https://b2r2.org/" target="_blank">B2R2</a> framework. It was created to easier the analysis of potentially malicious files. It is totally developed in .NET so you don't need to install or compile any other external libraries (the project is self contained).

With _Sojobo_ you can:
* Emulate a (32 bit) PE binary
* Inspect the memory of the emulated process
* Read the process state
* Display a disassembly of the executed code
* Emulate functions in a managed language (C# || F#)

## Download

 - [Source code][1]
 - [Download binary][2]

## Using Sojobo

_Sojobo_ is intended to be used as a framework to create program analysis utilities. However, various <a href="https://github.com/enkomio/Sojobo/tree/master/Src/Examples"><strong>sample utilities</strong></a> were created in order to show how to use the framework in a profitable way. 

## Compile

In order to compile Sojobo you need .NET Core to be installed and Visual Studio. To compile just run **build.bat**.

## License

Copyright (C) 2019 Antonio Parata - <a href="https://twitter.com/s4tan">@s4tan</a>

_Sojobo_ is licensed under the [Creative Commons](LICENSE.md).

  [1]: https://github.com/enkomio/sojobo/tree/master/Src
  [2]: https://github.com/enkomio/sojobo/releases/latest
