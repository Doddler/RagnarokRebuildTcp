# Ragnarok Rebuild (TCP/IP Websocket Ver.)

Server and client for a Ragnarok Online-like game. Some assembly required.

**Note**: This repository contains no game assets from Ragnarok Online.

**Warning**: The code is horrifying, would not recommend reading.

## Requirements

- Unity 2019.3.2f1 or higher
- .NET Core 3.1
- Lack of sanity

## Setting things up

- In the unity editor, using the ragnarok menu, select the "Set Ragnarok Data Directory". Specify the folder that contains files extracted from data.grf.
- Before you do anything, copy everything from the wav folder into the Assets/Sound directory. Other importing may break if these aren't present.
- To import maps, select Import Maps from the ragnarok menu. You can multi-select files here, and will save the scenes to the Assets/Scenes/Maps/ folder. This is very slow, don't import too many at once or you will be very sad.
- You will need to bake lights for each scene or the maps will look like butts. You can use the bake manager and the RagnarokLightingConfiguration preset to bake multiple scenes at once. This is also slow, especially if GPU baking fails for you.
- You'll need to copy over sprite .spr and .act pairs manually.
- Character sprites go in the Assets/Sprites/Characters/ folder. Use the subfolders BodyFemale, BodyMale, HeadFemale, and HeadMale for those sprites.
- The character sprite paths are specified in the headdata.json and playerclass.json file in the case that you want to use different folders. You can refer to these files to find the sprite names you'll need to import too.
- Monster sprites need to be placed in the Assets/Sprites/Monsters/ folder. This path is hardcoded somewhere.
- Monster sprite names are specified in the server config that gets copied over to the client using the update utility.
- For maps and sprites to load when you start the game, you will need to mark their imported scenes and spr assets as Addressables. You can do this using the 'Ragnarok\Update Addressables' menu option.
- Server config files are csv files in the RoRebuild\RebuildZoneServer\Data\ folder.
- The server config specifies all maps the server attempts to load, monsters, their spawns, and map connectors. A map needs to be imported first on the unity side, or the pathfinding data won't exist for the server to use.
- If you change monsters in the server data csv, run the updateclient.bat to copy settings over to the client.
- The server will copy configuration files on startup, but you should have the server stopped when making changes as visual studio may not recognize the file has changed.
- It will probably not work first time, and I probably missed important things on this list. Good luck!