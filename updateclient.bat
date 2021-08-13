cd RoRebuild\RebuildData.Shared
dotnet build -c Release
cd ../..
xcopy "RoRebuild\RebuildData.Shared\bin\Release\netstandard2.0\RebuildData.Shared.dll" "RebuildClient\Assets\Data\RebuildData.Shared.dll" /s /y
cd RoRebuild\DataToClientUtility
dotnet build -c Release
cd "bin\Release\netcoreapp3.1\"
DataToClientUtility.exe
pause