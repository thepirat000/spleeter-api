@Echo off
dotnet publish SpleeterAPI.csproj -c Release
ECHO RUN:
ECHO cd bin\Release\netcoreapp3.0\publish
ECHO dotnet splitterapi.dll
