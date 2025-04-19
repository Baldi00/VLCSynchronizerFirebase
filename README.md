# VLC Synchronizer using Firebase
The goal is to synchronize two or more instances of VLC video on different machines, different connections, with the same video stored locally.<br>
Something like Youtube Watch2gether or Netflix Teleparty but for local videos<br>

## Usage
Install <a href="https://builds.dotnet.microsoft.com/dotnet/Runtime/8.0.15/dotnet-runtime-8.0.15-win-x64.exe">dotnet</a> if not already installed<br>
Drag and drop the video file on the executable on each machine<br>
<b>PRESS Y</b> key on the keyboard to toggle <b>PLAY/PAUSE</b> state<br><br>
If you want to <b>SEEK</b> do<br>
`seek (with arrows) -> pause (press Y) -> play (press Y)`<br>
or<br>
`pause (press Y) -> seek (with arrows) -> play (press Y)`

## Firebase setup
Create a Realtime Database in Firebase Console as follows
### Structure
```js
{
  "vlcState": {
    "isPlaying": false,
    "currentTime": 0
  }
}
```
### Rules
```js
{
  "rules": {
    ".read": true,
    ".write": true
  }
}
```

## Dependencies (NuGet)
FirebaseDatabase.net<br>
Newtonsoft.Json<br>
SharpHook<br>
SharpHook.Reactive

## Linux
Since project uses cross platform code the project can also run on Linux<br>
In order to compile for Linux run this command in Visual Studio terminal<br>
`dotnet publish -c Release -r linux-x64 --self-contained false`
