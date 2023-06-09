
$down = New-Object System.Net.WebClient

# Create restore point
Checkpoint-Computer -Description 'Before spleeter-gpu'

# Install chocolatey
iex ($down.DownloadString('https://chocolatey.org/install.ps1'));

# Download dotnet hosting bundle
Write-Host "Download dotnet core runtime & hosting bundle", $PSScriptRoot -foregroundcolor "green";
$url = 'https://download.visualstudio.microsoft.com/download/pr/19927e80-7df2-4906-badd-439502008177/cb55d49c06a3691965b4bcf934ead822/dotnet-hosting-7.0.5-win.exe';
$file = $PSScriptRoot + '\dotnet-hosting-7.0.5-win.exe';
$down.DownloadFile($url,$file);
# Install dotnet core runtime & hosting bundle
Write-Host "Install dotnet core runtime & hosting bundle", $file -foregroundcolor "green";
& $file /install /passive 

#GIT
Write-Host "Installing GIT" -foregroundcolor "green";
choco install git -y --no-progress

#ffmpeg
choco install ffmpeg -y --no-progress

#youtube-dl
choco install youtube-dl -y --no-progress

#yt-dlp
choco install yt-dlp -y --no-progress

#create eventsource name
eventcreate /ID 1 /L APPLICATION /T INFORMATION  /SO Spleeter /D "Creating event source"

#Conda
Write-Host "Installing miniconda3 (this can take some time)" -foregroundcolor "green";
choco install miniconda3 -y --no-progress

& 'C:\tools\miniconda3\shell\condabin\conda-hook.ps1'; 
conda activate 'C:\tools\miniconda3';

conda update -n base -c defaults conda -y

Write-Host "Installing spleeter (this can take some time)" -foregroundcolor "green";
pip install spleeter

conda deactivate 

#environment variables and dirs
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("MODEL_PATH", "c:\spleeter\model", "Machine")
mkdir "c:\spleeter\input"
mkdir "c:\spleeter\output"
mkdir "c:\spleeter\model"
mkdir "c:\spleeter\cache"

#Refresh PATH environment variable
refreshenv
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User") 

#git clone
mkdir "c:\git"
cd "c:\git"

git clone -q https://github.com/deezer/spleeter
git clone -q https://github.com/thepirat000/spleeter-api

#build and publish spleeter-api
cd spleeter-api
dotnet build SpleeterAPI.sln -c Release
dotnet publish SpleeterAPI.csproj -c Release
cd bin\Release\netcoreapp3.0\publish


Write-Host "Installation complete..."



