# Ragnarok Rebuild (TCP/IP Websocket Ver.)

Server and client for a Ragnarok Online-like game. Some assembly required.

**Note**: This repository contains no game assets from Ragnarok Online. You will need to provide those yourself.

**Warning**: The code is horrifying, would not recommend reading.

## Requirements

* Unity 2022.3.62f2 or higher
* .NET 8
* Lack of sanity

## Setting things up

* Load up the server csproj project, and make sure the server is able to build successfully. You don't need to run it yet.
* In the root project directory, run "updateclient.bat". This copies server definitions and data over to the client.
* Open the client directory in Unity.
* From the Ragnarok menu, select "Copy data from client data folder".
* First you will be prompted to set a path where you have the files extracted from data.grf from an original client. For this import process to work correctly, the files will need to have been extracted with the right locale and have working korean file names.
* You then will receive a warning on how long the process will take. In testing this took about 1-2 hours to complete. If you want to process fewer maps, you can import them one at a time via the 'Ragnarok -> Import Maps' option, or you can trim the files RoRebuildServer/ServerData/Db/Maps.csv and Instances.csv to only have a small subset of maps, and then rerun the updateclient.bat.
* Once all the maps are imported, open the Lighting Manager window (Ragnarok -> Open Lighting Manager).
* Place all the scenes in the Scenes\\Maps folder into the list of scenes on the Lighting Manager window. Then, click 'bake all scenes' (not bake all). This can take several hours depending on your GPU. You can skip this step but maps will not display with any lighting until you do.
* Once lighting is baked, you can select the option 'Make Minimaps' to generate minimap images.
* Copy any of your BGM over from your ragnarok client into the Music folder.
* Finally, select 'Ragnarok -> Update Addressables' to link all the newly imported assets. Any time you add sprites or maps, you will need to do this again.
* At this point, you should be able to run the server. Make sure you set visual studio to run the server as a standalone rather than via IIS or IIS express (drop down next to the run button).
* Once the server is running, hitting play in the editor should allow you to connect.

## Special Considerations

* A number of custom monsters exist within the project for which existing sprites do not exist, but like the other RO sprites these are excluded to avoid the project holding copywritten material. If you don't do something with these monsters they will appear in game with the default poring sprite so you may want to matching sprites for these monsters, adjust the sprite they use to other monsters, or simply remove them. These monsters are defined in ServerData/Db/Monsters.csv and have an ID starting with 6000 onwards, you can change the sprite field here or remove the monster entry entirely (the server will start with ignorable warnings). Remember when making changes to the server data to run updateclient.bat afterwards so the client remains up to date.
