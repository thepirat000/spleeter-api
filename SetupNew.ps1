$down = New-Object System.Net.WebClient

# Create restore point
Checkpoint-Computer -Description 'Before spleeter'

# Install chocolatey
iex ($down.DownloadString('https://chocolatey.org/install.ps1'));

# Install IIS
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-WebServer
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-CommonHttpFeatures
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HttpErrors
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HttpRedirect
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ApplicationDevelopment
Enable-WindowsOptionalFeature -online -norestart -FeatureName NetFx4Extended-ASPNET45
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-NetFxExtensibility45
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HealthAndDiagnostics
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HttpLogging
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-LoggingLibraries
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-RequestMonitor
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HttpTracing
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-Security
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-RequestFiltering
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-Performance
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-WebServerManagementTools
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-IIS6ManagementCompatibility
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-Metabase
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ManagementConsole
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-BasicAuthentication
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-WindowsAuthentication
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-StaticContent
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-DefaultDocument
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-WebSockets
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ApplicationInit
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ISAPIExtensions
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ISAPIFilter
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-HttpCompressionStatic
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ASPNET45
Enable-WindowsOptionalFeature -Online -norestart -FeatureName IIS-ManagementService
net start WMSvc
choco install webdeploy -y --no-progress
choco install urlrewrite -y --no-progress

# Download dotnet hosting bundle
Write-Host "Download dotnet core runtime & hosting bundle", $PSScriptRoot -foregroundcolor "green";
$url = 'https://download.visualstudio.microsoft.com/download/pr/19927e80-7df2-4906-badd-439502008177/cb55d49c06a3691965b4bcf934ead822/dotnet-hosting-7.0.5-win.exe';
$file = $PSScriptRoot + '\dotnet-hosting-7.0.5-win.exe';
$down.DownloadFile($url,$file);
# Install dotnet core runtime & hosting bundle
Write-Host "Install dotnet core runtime & hosting bundle", $file -foregroundcolor "green";
& $file /install /passive 

# Install dotnet core SDK
Write-Host "Install dotnet core SDK", $PSScriptRoot -foregroundcolor "green";
$url  = 'https://download.visualstudio.microsoft.com/download/pr/974313ac-3d89-4c51-a6e8-338d864cf907/6ed5d4933878cada1b194dd1084a7e12/dotnet-sdk-7.0.302-win-x64.exe';
$file = $PSScriptRoot + '\dotnet-sdk-7.0.302-win-x64.exe';
$down.DownloadFile($url,$file);
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

Write-Host "Installation complete..."

#git clone
mkdir "c:\git"
cd "c:\git"

git clone -q https://github.com/deezer/spleeter
git clone -q https://github.com/thepirat000/spleeter-api

#build and publish spleeter-api
cd "c:\git\spleeter-api"
git pull
dotnet build SpleeterAPI.sln -c Release
dotnet publish SpleeterAPI.csproj -c Release

cd bin\Release\net7.0\publish
dotnet SpleeterAPI.dll --launch-profile https --urls "http://*:80;https://*:443"

