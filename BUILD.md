# Building Exifind

Exifind is licensed under the MIT License. Third-party components retain their
own licenses; see `THIRD-PARTY-NOTICES.txt`.

Requirements:

- Windows with .NET Framework 4.x C# compiler
- `Exifind.Runtime-v13.zip`
- `exifind.ico`

Compile `LumixMeta.cs` as a Windows executable, embed
`Exifind.Runtime-v13.zip` with the logical resource name
`Exifind.Runtime.zip`, and use `exifind.ico` as the Win32 icon. Reference:

- System.Windows.Forms.dll
- System.Drawing.dll
- System.Web.Extensions.dll
- System.Core.dll
- System.IO.Compression.dll
- System.IO.Compression.FileSystem.dll

The embedded runtime contains an unmodified ExifTool Windows package,
Spoqa Han Sans Neo fonts, and the applicable license notices.
