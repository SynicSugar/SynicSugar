+++
title = "SynicSugar"

# The homepage contents
[extra]

# Menu items
[[extra.menu.main]]

[[extra.list]]

+++
# What is SynicSugar?
SynicSugar is Unity High-Level Network Library with EpicOnlineServices.<br>
The concept is the syntax sugar of netcode. You can implement matchmaking, host migration, RPC and other basic functions for online game with SynicSugar.<br>
There is no charge for use and no CCU limits or server management thanks to EpicGames.<br>
Almost SynicSugar APIs are zero-allocation in Runtime, so the process is sonic. SynicSugar will be optimized for small-party game not covered by [Mirror](https://github.com/MirrorNetworking/Mirror). Can get it from [Github](https://github.com/SynicSugar/SynicSugar).


```cs
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



## Requirement
- Unity 21.3 or 22.43 (-ver0.8.0), later (ver0.8.1)
- [UniTask](https://github.com/Cysharp/UniTask)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)
- [eos_plugin_for_unity](https://github.com/PlayEveryWare/eos_plugin_for_unity)
- [Mono.Cecil](https://github.com/jbevain/cecil)

SynicSugar generates sync processes using Roslyn SourceGenerator and inserts these codes into your project using Mono.Cecil. These operations are automatically performed at compile based on your network attributes. What we need to sync game is just adding network attributes.<br><br>

The dependencies are for easily and performance. [EOSSDK is not thread-safe](https://dev.epicgames.com/docs/epic-online-services/eos-get-started/working-with-the-eos-sdk/conventions-and-limitations#thread-safety), so cannot use async/await or task. However, UniTask runs everything on Unity's main thread and the perform of Unitask better than standard async/await. MemoryPack is the fastest serializer available in C#. Since SynicSugar is not synchronized with the server, we can freely choose a Serializer without having to consider compatibility.<br><br>

## Contribution Guideline
SynicSugar's concept is an easy online game development for everyone. Therefore, the development is also based on this policy. We can create online game for up to 64 people supported by EOS, but the main is small-party(2-16) game. If you want to create MMO, Survival Game and Party Game, you should use [Mirror](https://github.com/MirrorNetworking/Mirror). 

The roadmap is [here](https://github.com/orgs/SynicSugar/projects/3). For the time being, add a basic function for online-game and improve the performance. If you need any necessary functions, please post it to Github Issue or give a pull request. Great thanks for all contributions!