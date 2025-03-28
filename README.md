# [<img src="https://github.com/skeyll/SynicSugar/blob/main/Resources/Logo_doc.png" width="40%">](https://skeyll.github.io/SynicSugar/)
# SynicSugar
![https://github.com/skeyll/SynicSugar/blob/main/LICENSE](https://img.shields.io/github/license/skeyll/SynicSugar) ![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue) [![openupm](https://img.shields.io/npm/v/net.skeyll.synicsugar?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/net.skeyll.synicsugar/) 

SynicSugar is Unity High-Level Network Library with EpicOnlineServices.<br>
The concept is the syntax sugar of netcode. You can implement matchmaking, host migration, RPC and other basic functions for online game with SynicSugar.<br>
There is no charge for use and no CCU limits or server management thanks to EpicGames.<br>
Almost SynicSugar APIs are zero-allocation in Runtime, so the process is sonic. SynicSugar will be optimized for small-party game not covered by [Mirror](https://github.com/MirrorNetworking/Mirror).

For more details, visit [https://synicsugar.github.io/SynicSugar/](https://synicsugar.github.io/SynicSugar/).

## Why SynicSugar?
Most networking libraries are designed with dedicated servers in mind. In contrast, SynicSugar is built on a serverless P2P architecture, seamlessly supporting host migration and even providing a reconnection feature for disconnected players. 

With its focus on small-scale games and a simple API, SynicSugar enables efficient and cost-effective network development.

[![Sample(youtube)](https://github.com/synicsugar/SynicSugar/blob/main/Resources/youtube_thumbnail.png)](https://www.youtube.com/embed/hQT1yniGNi0)

## Features:
 - Mesh topology with max 64 peers
 - No Use Cost and No CCU Limit
 - High-level APIs for mobile and small-party games (MatchMaking, Large-packet, PushToTalk, and Offlinemode...)
 - Full support for Host-migration and Re-connection by library
 - Almost all RPC processes are zero-allocation
 - Free VoiceChat (Max16peers)
 - Cross-platform connection <br>
    (Windows / Linux / macOS <br>
    Android / iOS <br>
    Nintendo Switch / Xbox One / Xbox Series X / PlayStation 4 / PlayStation 5)

```csharp
using SynicSugar.P2P;
using UnityEngine;
[NetworkPlayer(true)]
public partial class Player {  
    //Sync every 1000ms(default)
    [SyncVar] public int Hp;
    //Sync every 3000ms
    [SyncVar(3000)] Skill skill;

    [Synic(0)] public string name;
    [Synic(0)] public string item;
    [Synic(1)] public Vector3 pos;

    //To sync, the local Player just calls the owner's instance's RPC from Button or other methods.
    [Rpc] 
    public void Attack(int damage){ 
        Hp -= damage;
    }
    [TargetRpc]
    public void GreetTarget(UserId id, string message){
        Debug.log(message);
    }
    [Rpc]
    public void Heal(HealInfo info){
        Debug.log($"{OwnerUserId} heal {info.target} {info.amount}");
    }

    public void SyncBasisStatus(){
        //Sync the Variables that have Synic(0) attribute at once.
        ConnectHub.Instance.SyncSynic(p2pInfo.Instance.LastDisconnectedUsersId, 0);
    }
}
```

## Requirement
- Unity 21.3 or 22.x
- [UniTask](https://github.com/Cysharp/UniTask)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)
- [eos_plugin_for_unity](https://github.com/PlayEveryWare/eos_plugin_for_unity)
- [Mono.Cecil](https://github.com/jbevain/cecil)

SynicSugar generates sync processes using Roslyn SourceGenerator and inserts these codes into your project using Mono.Cecil. These operations are automatically performed at compile based on your network attributes. What we need to sync game is just adding network attributes.<br><br>

The dependencies are for easily and performance. [EOSSDK is not thread-safe](https://dev.epicgames.com/docs/epic-online-services/eos-get-started/working-with-the-eos-sdk/conventions-and-limitations#thread-safety), so cannot use async/await or task. However, UniTask runs everything on Unity's main thread and the perform of Unitask better than standard async/await. MemoryPack is the fastest serializer available in C#. Since SynicSugar is not synchronized with the server, we can freely choose a Serializer without having to consider compatibility.<br><br>

## About Internal
In SynicSugar, User uses DeviceID of EOS's [Connect Interface](https://dev.epicgames.com/docs/ja/game-services/eos-connect-interface) to log in  EOS as an anonymous user.<br><br>
For matchmaking, SynicSugar use [Lobby Interface](https://dev.epicgames.com/docs/game-services/lobbies-and-sessions/lobbies). <br>
Host user creates lobbies for 2-64 players and Guest user seraches Lobby based on custom attributes you add. After the host closes the lobby manually or the lobby fills up with a specified number of players and the lobby is closed automatically, starts to preparation p2p connection. Host generates a random SocketID and adds the strings to Lobby attribute. Users use this One-time SocketID and UserId of EOS for actual connection.<br>
Also, at this time, the LobbyID is saved in a specified location. Once the lobby is closed, only users who know the LobbyID can join the Lobby.
In SynicSugar, all connections continue even if the host drops out. The lobby host-migration occurs automatically. Reconnection can be done easily by calling some APIs. We add the "Synic" attribute to variables essential for restart, enabling safe data restoration for reconnecting players without the risk of direct overwriting as a form of cheating.<br><br>
While there are plans to add account linking, TitleStorage, and PlayerStorage in the future, these features are not yet implemented. There are no plans to add Easy Anti-Cheat.  Load map is  [here](https://github.com/orgs/SynicSugar/projects/3).<br>


## Getting started
### 1.Install SynicSugar and depended librarys.  
The first is to import SynicSugar and dependent libraries. <br>
You can get SynicSugar from OpenUPM or [SynicSugar/Release](https://github.com/synicsugar/SynicSugar/releases)'s unitypackage.  
 .unitypackage contains Mono.Cecil and System.Runtime.CompilerServices.Unsafe.dll for MemoryPack and, in addition to SynicSugar. Therefore, you can skip some processes, but it is more convenient to download via OpenUPM for version control.  

1. Rigister some package with OpenUPM<br>

 In your unity project, select Edit/ProjectSetting/PackageManager. Then, register SynicSugar. <br>
 
 Name: OpenUPM
 
 URL: https://package.openupm.com

 Scope(s):<br>
 For OpenUPM<br>
* net.skeyll.synicsugar <br>

 For unitypackage<br>
* com.cysharp.unitask 
* com.playeveryware.eos
* com.cysharp.memorypack

           
![image](https://user-images.githubusercontent.com/50002207/230567095-04cfbfcc-f1c9-4b0d-9088-2fbfc08da8f8.png)


2. Install SynicSugar and dependecies <br>
　These packages can be imported from **Window/PackageManager/MyRegistries**. Importing SynicSugar will automatically import the other required librarys. If uses synicsugar.unitypackage, import other three packages.<br>
 If you are using another version in your project, that one will probably work. However, SynicSugar has been developed using the following:  
 * Epic Online Services Plugin for Unity: 3.3.3 
 * UniTask: 2.5.5
 * MemoryPack: 1.10.0
 
 
 3. Import the rest (Skip if downloading as unitypackage.)  <br>
Import what is not in OpenUPM.  
- Mono.Cecil  
Enter **com.unity.nuget.mono-cecil** in **Edit/ProjectSetting/PackageManager/+/Add package from git URL**.  

![image](https://user-images.githubusercontent.com/50002207/231324146-292634b7-3d42-420d-a20c-37f5fc0ad688.png)

- System.Runtime.CompilerServices.Unsafe  
MemoryPack need System.Runtime.CompilerServices.Unsafe.dll. You can get this dll from Download package in https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0 . Since this contains DLLs for multiple environments, only import packages for Unity. Unzip the downloaded file and drag and drop **lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll** into your project.  You can also get this DLL from this [repo](https://github.com/synicsugar/SynicSugar/tree/main/SynicSugar/Assets/Plugins/Runtime.CompilerServices.Unsafe)


### 2.Get some tokens for EOS.

Please check [the eos document](https://dev.epicgames.com/ja/news/how-to-set-up-epic-online-services-eos) or [the plugin page](https://github.com/PlayEveryWare/eos_plugin_for_unity). SynicSugar doesn't need EOS store brand for mainly API now. Just register and can develop online games.

About app credential, you can use Peer2Peer as ClientPolicy. The minimum is as follows.
![image](https://user-images.githubusercontent.com/50002207/230758754-4333b431-48fe-4539-aa97-20c6f86d68ae.png)


## Showcase
### [Ponolf](https://ponolf.skeyll.net/)
![Ponolf_logo_616x353v2](https://github.com/skeyll/SynicSugar/assets/50002207/c1616004-a252-4ffb-8aab-76941e3e6193)

Ponolf is a quiz-based Imposter game. It supports 2-12 players and submit user drawn image as large packet. It also implements Host-Migration and re-connection. Ponolf supports cross-play between PC, Android, and iOS platforms.

## License
 License is under the MIT.

## Contribute Guideline and Goal

SynicSugar's concept is an easy online game development for everyone. Therefore, the development is also based on this policy. We can create online game for up to 64 people supported by EOS, but the main is small-party(2-16) game. If you want to create MMO, Survival Game and Party Game, you should use [Mirror](https://github.com/MirrorNetworking/Mirror). 

The roadmap is [here](https://github.com/orgs/SynicSugar/projects/3). For the time being, add a basic function for online-game and improve the performance. If you need any necessary functions, please post it to Github Issue or give a pull request. Great thanks for all contributions!
