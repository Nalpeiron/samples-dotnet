To make the sample working with the  C++ `Zentitle2Core library`, you need to download the core library from
the [Zentitle2Core Library Release Notes](https://docs.zentitle.io/developers/the-zentitle2core-library-c++/release-notes), unzip it and paste the `Zentitle2Core.dll` 
file from the `Windows_x86_64` folder to the `Net48.Sdk.Sample` directory.

This example demonstrates the approach from 
[Zentitle2Core Library Integration](https://docs.zentitle.io/developers/the-licensing-client-.net/zentitle2core-library#zentitle2core-library-integration),
where the `Zentitle2Core.dll` is placed in the project directory, without the need to write a 
[Custom DllImportResolver](https://docs.zentitle.io/developers/the-licensing-client-.net/zentitle2core-library#custom-dllimportresolver).
