+++
title = "GetPacketQueueInfo"
weight = 9
+++
## GetPacketQueueInfo
<small>*Namespace: SynicSugar.P2P* <br>
*Class: p2pInfo* </small>

public Result GetPacketQueueInfo(out PacketQueueInformation packetQueueInformation)


### Description
Get the information related to the current state of the packet queues. 


```cs
using Cysharp.Threading.Tasks;
using UnityEngine;
using SynicSugar;
using SynicSugar.P2P;

public class p2pSample : MonoBehaviour {
    public void GetPacketQueueInfo(){
        Result result = GetPacketQueueInfo(out PacketQueueInformation packetQueueInformation);

        if(result == Result.Success)
        {
            //Incoming Packets
            if(packetQueueInformation.IncomingPacketQueueCurrentPacketCount != prevIncomingPacketSize)
            {
                prevIncomingPacketSize = packetQueueInformation.IncomingPacketQueueCurrentPacketCount;
                Debug.Log($"PacketQueueInfo(Incoming): IncomingPacketQueueCurrentSizeBytes: {packetQueueInformation.IncomingPacketQueueCurrentSizeBytes} / IncomingPacketQueueCurrentPacketCount: {packetQueueInformation.IncomingPacketQueueCurrentPacketCount} / IncomingPacketQueueMaxSizeBytes: {packetQueueInformation.IncomingPacketQueueMaxSizeBytes}");   
            }
            // Outgoing Packets
            if(packetQueueInformation.OutgoingPacketQueueCurrentPacketCount != prevOutgoingPacketSize)
            {
                prevOutgoingPacketSize = packetQueueInformation.OutgoingPacketQueueCurrentPacketCount;
                Debug.Log($"PacketQueueInfo(Outgoing): OutgoingPacketQueueCurrentSizeBytes: {packetQueueInformation.OutgoingPacketQueueCurrentSizeBytes} / OutgoingPacketQueueCurrentPacketCount: {packetQueueInformation.OutgoingPacketQueueCurrentPacketCount} / OutgoingPacketQueueMaxSizeBytes: {packetQueueInformation.OutgoingPacketQueueMaxSizeBytes}");
            }
        }
    }
}
```