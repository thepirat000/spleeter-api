# Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Bypass -Force;

param(
[Parameter()][String]$type="",
[Parameter()][String]$iis=""
) 

Set-ExecutionPolicy Bypass -Scope Process -Force; 

if (!$type) {
    $type = Read-Host -Prompt 'Enter the installation type (cpu/gpu)';
}
if ($type -ne "cpu" -and $type -ne "gpu") {
    return;
}
if (!$iis) {
    $iis = Read-Host -Prompt 'Install IIS components (y/n)'
}
if ($iis -ne "y" -and $iis -ne "n") {
    return;
}

$down = New-Object System.Net.WebClient

# Create restore point
Checkpoint-Computer -Description 'Before spleeter-gpu'

# Install chocolatey
iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'));

# Download dotnet core runtime & hosting bundle
Write-Host "Download dotnet core runtime & hosting bundle", $PSScriptRoot -foregroundcolor "green";
$url  = 'https://download.visualstudio.microsoft.com/download/pr/bf608208-38aa-4a40-9b71-ae3b251e110a/bc1cecb14f75cc83dcd4bbc3309f7086/dotnet-hosting-3.0.0-win.exe';
$file = $PSScriptRoot + '\dotnet-hosting-3.0.0-win.exe';
$down.DownloadFile($url,$file);
# Install dotnet core runtime & hosting bundle
Write-Host "Install dotnet core runtime & hosting bundle", $file -foregroundcolor "green";
& $file /install /passive 

#Enable IIS
if ($iis -eq "y") {
    Write-Host "Installing IIS features" -foregroundcolor "green";
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
}

# Install dotnet core SDK
Write-Host "Install dotnet core SDK", $PSScriptRoot -foregroundcolor "green";
$url  = 'https://download.visualstudio.microsoft.com/download/pr/66adfd75-9c1d-4e44-8d9c-cdc0cbc41104/5288b628601e30b0fa10d64fdaf64287/dotnet-sdk-3.0.101-win-x64.exe';
$file = $PSScriptRoot + '\dotnet-sdk-3.0.101-win-x64.exe';
$down.DownloadFile($url,$file);
& $file /install /passive

#GIT
Write-Host "Installing GIT" -foregroundcolor "green";
choco install git -y --no-progress

#ffmpeg
choco install ffmpeg -y --no-progress

if ($type -eq "gpu") {
    #CUDA drivers
    Write-Host "Installing CUDA drivers (this can take some time)" -foregroundcolor "green";
    choco install cuda --ignore-checksums -y --no-progress
}

#Conda
Write-Host "Installing miniconda3 (this can take some time)" -foregroundcolor "green";
choco install miniconda3 -y --no-progress

& 'C:\tools\miniconda3\shell\condabin\conda-hook.ps1'; 
conda activate 'C:\tools\miniconda3';

if ($type -eq "gpu") {
    Write-Host "Installing tensorflow spleeter-gpu (this can take some time)" -foregroundcolor "green";
    conda install -c conda-forge spleeter-gpu -y
} 
else {
    Write-Host "Installing spleeter-cpu (this can take some time)" -foregroundcolor "green";
    conda install -c conda-forge spleeter -y
}

conda deactivate 

#youtube-dl
choco install youtube-dl -y --no-progress

#create eventsource name
eventcreate /ID 1 /L APPLICATION /T INFORMATION  /SO Spleeter /D "Creating event source"

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

if ($type -eq "gpu") {
    copy "C:\git\spleeter-api\lib\nvcuda.dll" "c:\windows\system32\nvcuda.dll"
}

#build and publish spleeter-api
cd spleeter-api
dotnet build SpleeterAPI.sln -c Release
dotnet publish SpleeterAPI.csproj -c Release
cd bin\Release\netcoreapp3.0\publish


Write-Host "Installation complete..."
Write-Host ""
Write-Host "You can run the server in Kestrel with command: " -foregroundcolor "green";
Write-Host "dotnet C:\git\spleeter-api\bin\Release\netcoreapp3.0\publish\SpleeterAPI.dll --https_enabled false" -foregroundcolor "cyan";
Write-Host ""
Write-Host "It's recommended that you restart the machine" -foregroundcolor "green";
