# spleeter-api <img src="https://app.pixelencounter.com/api/basic/monsters/random" alt="drawing" width="20"/>

Audio separation API using Spleeter (CPU or GPU) from Deezer Research.

> [Spleeter](https://github.com/deezer/spleeter) is A Fast And State-of-the Art Music Source Separation Tool With Pre-trained Models.
> Authors: Romain Hennequin, Anis Khlif, Felix Voituret and Manuel Moussallam

This tool allows to split the audio of a youtube video or any .mp3:

- Enter a YouTube URL and get isolated mp3s for each part (i.e. Bass.mp3, Drums.mp3, Vocals.mp3, etc), or get an .mp4 with the original video plus the audio mix including the subtitles (if any), and more.
- Upload your .mp3's and split

### Live demo: 

- https://thepirat000.github.io/spleeter-api
- https://spleeter-api.canadacentral.cloudapp.azure.com

[![alt text](https://user-images.githubusercontent.com/9836380/146712887-c5f20616-f216-465e-87cb-c54390c99a55.png "Spleeter")](https://thepirat000.github.io/spleeter-api/)

## Install

### Local installation on Windows

Install the dependencies and pre-requisites with PowerShell setup script: [SetupNew.ps1](https://github.com/thepirat000/spleeter-api/blob/master/SetupNew.ps1)

`powershell "IEX(New-Object Net.WebClient).downloadString('https://raw.githubusercontent.com/thepirat000/spleeter-api/master/SetupNew.ps1')"`

Tested on Azure VM of size "Standard D2 (2 vcpus, 7 GiB memory)" 

UDP Logging port: 2223




