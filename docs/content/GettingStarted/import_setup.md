+++
title = "Import and Set up"
weight = 1
+++
## Import and Set up

### 1.Install SynicSugar and depended librarys.  
 The first is to import SynicSugar and dependent libraries.　You can get SynicSugar from OpenUPM or [SynicSugar/Release](https://github.com/SynicSugar/SynicSugar/releases)'s unitypackage.  
 .unitypackage contains Mono.Cecil and System.Runtime.CompilerServices.Unsafe.dll for MemoryPack and, in addition to SynicSugar. Therefore, you can skip some processes, but it is more convenient to download via OpenUPM for version control.  

1. Rigister some package with OpenUPM  

 In your unity project, select Edit/ProjectSetting/PackageManager. Then, register some librarys.
 
 Name: OpenUPM
 
 URL: https://package.openupm.com
 
 Scope(s):
* net.skeyll.synicsugar (Skip if downloading as unitypackage)
* com.cysharp.unitask
* com.playeveryware.eos
* com.cysharp.memorypack
           
![image](https://user-images.githubusercontent.com/50002207/230567095-04cfbfcc-f1c9-4b0d-9088-2fbfc08da8f8.png)


2. Install these packages  
　These packages can be imported from **Window/PackageManager/MyRegistries**. Importing SynicSugar will automatically import the other required librarys. If you are using another version in your project, that one will probably work. However, SynicSugar has been developed using the following:  
 * Epic Online Services Plugin for Unity: 2.2.0  
 * UniTask: 2.3.3
 * MemoryPack: 1.9.13  
 
 
3. Import the rest (Skip if downloading as unitypackage.)  
Import what is not in OpenUPM.  
- Mono.Cecil  
Enter **com.unity.nuget.mono-cecil** in **Edit/ProjectSetting/PackageManager/+/Add package from git URL**.  

![image](https://user-images.githubusercontent.com/50002207/231324146-292634b7-3d42-420d-a20c-37f5fc0ad688.png)

- System.Runtime.CompilerServices.Unsafe  
MemoryPack need System.Runtime.CompilerServices.Unsafe.dll. You can get this dll from Download package in https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0 . Since this contains DLLs for multiple environments, only import packages for Unity. Unzip the downloaded file and drag and drop **lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll** into your project.


### 2.Get some tokens for EOS.

Please check [the eos document](https://dev.epicgames.com/ja/news/how-to-set-up-epic-online-services-eos) or [the plugin page](https://github.com/PlayEveryWare/eos_plugin_for_unity). SynicSugar doesn't need EOS store brand. Just register and can use server.

About app credential, you can use Peer2Peer as ClientPolicy. The minimum is as follows.
![image](https://user-images.githubusercontent.com/50002207/230758754-4333b431-48fe-4539-aa97-20c6f86d68ae.png)

