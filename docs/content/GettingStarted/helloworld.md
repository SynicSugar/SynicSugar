+++
title = "Hello World"
weight = 2
+++
## Hello World

### 1. Sign in EOS
At first, we put EOSManager.prefab to the first scene to Log-in EOS. To generate EOSManager, right-click on the Hierarchy and click SynicSugar/EOSManager.<br>
SynicSugar connects, matchmaking and others on EpicOnlineServices with this EOSManager. 

![Image](https://github.com/SynicSugar/SynicSugar/assets/50002207/621edac1-d607-4f3f-9da5-f60323a9849e)

To singin EOS, we use EOSConenction.LoginWithDeviceID. This has two return value type of bool for just result and string for result details.

SynicSugar is written in (UniTask's) Async/Await. After successful login, can get true or "Success".
```cs
using Cysharp.Threading.Tasks;
using SynicSugar.Login;
using UnityEngine;

public class SynicSugarLogin : MonoBehaviour {
    async UniTaskVoid Start(){
        //(bool isSuccess, Result detail)'s tuple
        var result = await EOSConnect.LoginWithDeviceID();

        if(result.isSuccess){
            Debug.Log("SUCCESS.");
            return;
        }
        
        Debug.LogErrorFormat($"FAILURE {0}", {result.detail});
    }
}
```     
We can also write without Async/Await processing.

This is so for other all parts. If we can use task harder, we can write codes as usual or with .Forget(). in the last of the functions.  However, in this case, the next process is started without waiting for the SynicSugar asynchronous processing.
```cs
// using Cysharp.Threading.Tasks;
using SynicSugar.Login;
using UnityEngine;

public class YourLoginClass : MonoBehaviour {
    public void LoginWithDeviceID(){
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        
        EOSConnect.LoginWithDeviceID();
        // or
        // EOSConnect.LoginWithDeviceID().Forget();
        
        //Continue to process not to wait for login.
        // ...
    }
 }
```      

### 2. Matchmaking

We need NetwrokManager for MatchMaking and P2P. To generate NetwrokManager, right-click on the Hierarchy of matchmaking scene and click SynicSugar/NetworkManager. This object is a singleton and won't be destroyed until explicitly disposed of it.<br>

>If you want to use a single scene for online and offline mode, you generate this from Prefab only for online mode. You can get this object from Packages/SynicSugar/NetwrokManager/Prefabs/NetworkManager. 
>```cs
>using UnityEngine;
>using SynicSugar.MatchMake;
>public class MatchMaker : MonoBehaviour {
>    //Attach on Editor from SynicSugar/Runtime/Prefabs/ConnectManager
>    [SerializeField] GameObject ConnectManagerPrefab;
>    void Awake(){
>        if(isOnlineMode){ //Flag on your game
>            Instantiate(ConnectManagerPrefab);
>        }
>    }
>}
>```
><br>

NetwrokManager has some compornent for matchmaking and p2p connection. We can set several configs on Unity Editor. 

![image](https://user-images.githubusercontent.com/50002207/230761023-3754a4fc-46ae-4d33-8f86-9439ce1846c0.png)

For matchmaking, we just call MatchMakeManager.Instance.SearchAndCreateLobby(Lobby condition). 

At first, let's make a condition for matchmaking. We need LobbyObject for it. The object is created by MatchMakeManager.Instance.GenerateLobbyObject(string[] bucket) which returns *Lobby*. This bucket on arg is important section for matching like as Region, Mode, Map and so on for a match. (More detail is [Lobby Interface | Epic Online Services Developer](https://dev.epicgames.com/docs/en-US/game-services/lobbies).) SynicSugar use this bucket as just one serarch condition. Pass some condition as string[].Of course, we can specify others after generate. (Up to 99.)<br>

```cs ConditionSample
using SynicSugar.MatchMake;

public class MatchMakeConditions : MonoBehaviour {
    public Lobby GetLobbyCondition(){
        //Create lobbyObject with basis conditions
        Lobby lobbyCondition = MatchMakeManager.GenerateLobbyObject(new string[3]{"NA", "Rank", "Sea"});
        
        lobbyCondition.MaxLobbyMembers = 2; //2-64
        
        //Add conditions as attributes.
        //The limit of conditions is 100.
        //Need "Key", "Value", and "ComparisonOption"
        LobbyAttribute attribute = new LobbyAttribute();
        attribute.Key = "Level";
        attribute.SetValue(1); //Can use int, string, double, bool
        attribute.ComparisonOperator = Epic.OnlineServices.ComparisonOp.Equal; // https://dev.epicgames.com/docs/en-US/game-services/lobbies#comparison-operators
        lobbyCondition.Attributes.Add(attribute);
        
        //attribute = new LobbyAttribute();
        //attribute.Key = "SeaMap";
        //...
        
        return lobbyCondition;
    }
}
```
In addition creating conditions, we can set other several configs during matchmaking, such as the behavior of the buttons in matchmaking. See MatchMake.cs in Samples for details.<br>

Now, start matchmakinging with the conditions we set. Just call MatchMakeManager.Instance.SearchAndCreateLobby() with the LobbyObject.<br>
This process searches lobby on EOS server, then If there is a Lobby that meets the requirements, will try to join it. If not, creates new lobby as Lobby's Host.
In both pattern, wait until the Lobby is full. If matchmaking is successful, the information necessary for p2p is automatically exchanged.<br>
If get true, matchmaking is successful and p2p conennction is established.

```cs
using Cysharp.Threading.Tasks;
using SynicSugar.MatchMake;
public class MatchMaker : MonoBehaviour {
    async UniTask StartMatching(){
        bool isSuccess = await MatchMakeManager.Instance.SearchAndCreateLobby(matchConditions.GetLobbyCondition());
        
        if(isSuccess){
            //Success
            //Move to game scene or un-display the UIs for matchmaking.
            return;
        }
        //Failure
    }
}
```

### 3. p2p connection

We have already connect with other peers. To send and receive packets, all that remains is to create components fo that.<br>
SynicSugar has two type network objects: "NetworkPlayer" and "NetworkCommons". NetworkPlayer is like NetworkBehavior in other libraries. This each instance has UserID and, the instance is synchronized with the instance owner's process over the network. On the other hand, NetworkCommons has no UserID. All peers can call a process in this class to synchronize with others. (Or just lobby Host can force to synchronize a process with others.)  We can use NetworkCommons for game system such as game time, NPC HP, item drops and so on.<br>
For both types, we need to create a class with the "public **partial** class" keyword and "NetworkPlayer" or "NetworkCommons" attribute. 
```cs
using SynicSugar.P2P;
[NetworkCommons]
public partial class SampleClass {
    ///
}

using SynicSugar.P2P;
[NetworkPlayer]
public partial class SampleClass {
    ///
}
```
In this tutorial, we implement a function to send HelloWorld on debug log of other peers.<br>
We choice NetworkPlaye to identify who sent it and use Rpc to send it to the others.<br>
We add Rpc attribute and public modify to a function to synchronize the process.

```cs
using UnityEngine;
using SynicSugar.P2P;
[NetworkPlayer]
public partial class PlayerClass : MonoBehaviour {
    [Rpc]
    public void DebugHelloWorld(){
        Debug.Log($"Hello World");
    }
}
```
A function can send a message together with 1st arg. We can hard-code "HelloWorld" in the RPC procsess directly, but let's send HelloWorld as args here. <br>
When this is invoked in the local, this is invoked in other peers by the instance that has the same UserID as OwnerUserID.<br>
To display message and the sender ID on Debug.Log, we pass "Hello World" 1st arg and, then use OwnerUseID in each local. 

```cs
using UnityEngine;
using SynicSugar.P2P;
[NetworkPlayer]
public partial class PlayerClass : MonoBehaviour {
    void Start(){
        CallSendMessage();
    }

    //Call this from Unity Button, Start, or so on.
    //To send message as args need to pass it.
    public void CallSendMessage(){
        SendMessage("Hello World");
    }

    [Rpc]
    public void SendMessage(string message){
        //SynicSugar SourceGenerator genereates OwnerUserID in all NetworkPlayer.
        Debug.Log($"{message} from {OwnerUserID}");
    }
}
```

In gaming, we instantiate this component. This instances need UserID to be gave by EOS. SynicSugar have various way to set a user id to the instance. The simplest way is to call SynicObject.AllSpawn(Gameobject prefab). This function generates an prefab object with NetworkPlayer components, then set UserID to the all components.

```cs
using UnityEngine;
using SynicSugar.P2P;

public class ManagementClass : MonoBehaviour {
    // Attach components on Editor that has [NetworkPlayer] like upon PlayerClass.
    [SerializeField] GameObject playerPrefab; 
    void Start(){
        SynicObject.AllSpawn(playerPrefab);
    }
}
```

>For only two players, we can set UserID manually in Start.
>```cs
>using UnityEngine;
>using SynicSugar.P2P;
>[NetworkPlayer]
>public partial class PlayerClass : MonoBehaviour {
>    //Set this value on Editor
>    public bool isLocalPlayer;
>    void Start(){
>        if(isLocalPlayer){ 
>            SetOwnerID(p2pInfo.Instance.LocalUserId);
>        }else{
>            SetOwnerID(p2pInfo.Instance.RemoteUserIds[0]);
>        }
>        
>        CallSendMessage();
>    }
>    ///
>}
>```

Registering UserID automatically registers the instance with ConnectHub.  ConnectHub is for sending and receiving class. In other words, this is the end of the preparation for synchronization. <br>
Call ConnectHub.Instance.StartPacketReceiver() to get packets from receiving buffer.

```cs
using UnityEngine;
using SynicSugar.P2P;

public class ManagementClass : MonoBehaviour {
    [SerializeField] GameObject playerPrefab; 
    void Start(){
        SynicObject.AllSpawn(playerPrefab);

        //After generating the instances for receive in the local, start Packet Receiver.
        ConnectHub.Instance.StartPacketReceiver();

        //PlayerClass call CallSendMessage() on Start ofter this process.
    }
}
```
If we send the HelloWorld to other peers and can display the message on other device, we have succeeded.<br>
However, we only want to display the message in other peers' DebugLog, don't need in sender's log. In such case, we can use isLocal to separate the processes.

```cs
using UnityEngine;
using SynicSugar.P2P;
[NetworkPlayer]
public partial class PlayerClass : MonoBehaviour {
    void Start(){
        CallSendMessage();
    }
    
    void CallSendMessage(){
        SendMessage("Hello World");
    }


    [Rpc]
    public void SendMessage(string message){
        //This "isLocal" is generated automatically by SynicSugarSourceGenerator to judge local or remote.
        if(isLocal){ 
            Debug.Log("Send message");
            return;
        }

        Debug.Log($"{message} from {OwnerUserID}");
    }
}
```
If we want to stop connection, call ConnectHub's CloseSession. <br>
p2p is stopped and user leaves the Lobby that user joined in matchmaking.
```cs
using UnityEngine;
using SynicSugar.P2P;
[NetworkPlayer]
public partial class PlayerClass : MonoBehaviour {
    public async void CloseSession(){
        await ConnectHub.Instance.CloseSession();
        //Back to main menu
    }
}
```
If you want to synchronize a structure, you need to serialize it with [MemoryPack](https://github.com/Cysharp/MemoryPack). See Sample for more details.