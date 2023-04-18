# KZB Explorer
A program to explore and extract the contents of Kanzi Studio kzb files.

## Features
- Previews data in built-in hex viewer
- Extracts Mesh Data files as Wavefront OBJ
  - includes normals and texture coordinates (UVs)

<img alt="img1" src="https://github.com/theawesomecoder61/KZB-Explorer/raw/master/img1.jpg">
Folders are shaded in green.

## Compatibility
When researching the formats, I did not have access to Kanzi Studio. I only had access to a few kzb files. Therefore, compatibility is limited.

*If you face a problem, please create an issue and attach the raw data of the file.*

### Supported types
- Mesh Data

## Building
- Requires .NET 5.0 or later
- Clone or download as ZIP, open in VS 2019 or later, ensure NuGet downloads the required packages, build

## Credits
- Be.HexEditor by Bernhard Elbl, under the MIT license
